using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Pysar.Elements;
using Pysar.Skia;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;

// Report elements and Avalonia controls share several names; the report side is the one aliased
// away here because this file is an Avalonia control first.
using Image = Avalonia.Controls.Image;

namespace Pysar.Avalonia;

/// <summary>
///     Shows a built report as scrollable, zoomable pages, rasterising only what is on screen so the
///     text stays sharp at any zoom without the memory a whole zoomed page would cost.
/// </summary>
/// <remarks>
///     Scrolling is a real <see cref="ScrollViewer"/> over a <see cref="Canvas"/> the size of the
///     document, so the platform keeps its wheel, trackpad and scrollbars, and moves the page and
///     tile views itself while scrolling instead of the application repainting them - a self-painted
///     canvas was tried for this in the MAUI package and could not keep pace with a gesture.
///
///     The arithmetic - what zoom a mode resolves to, where a page sits, which cells are worth
///     drawing - lives in <see cref="ReportViewPresenter"/>, framework-neutral and unit-tested. This
///     type is the <see cref="IReportViewHost"/> it draws through: the view tree and the bindable
///     properties. Input (Task 5 of the implementation plan) is a separate file.
/// </remarks>
public partial class ReportView : UserControl, IReportViewHost, IReportViewSurface
{
    /// <summary>The line around a page unless the host asks for another one.</summary>
    private static readonly Color DefaultPageBorderColor = Color.Parse(ReportViewDefaults.PageBorderColorHex);

    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    // The pages and cells themselves: real views inside the scroll viewer, so the platform moves
    // them while scrolling instead of the application repainting them. Top/left when the canvas is
    // somehow still shorter than the viewport - ScrollViewer centres undersized content by default,
    // which is not how a document viewer lays pages out.
    private readonly Canvas _canvas = new()
    {
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Top
    };

    private readonly Dictionary<int, Border> _pageViews = [];
    private readonly Dictionary<TileKey, TileView> _tileViews = [];

    private readonly ReportViewPresenter _presenter;

    private readonly ReportViewSession _reportSession;

    /// <summary>
    ///     Set while <see cref="CurrentPage"/> is being written from the presenter's own report of
    ///     where the scroll landed, so that write is not mistaken for a request to scroll there.
    /// </summary>
    private bool _reportingCurrentPage;

    /// <summary>
    ///     The order this control follows between an input and the pixels. Shared with the other
    ///     hosts; what stays here is only what Avalonia itself has to do, through
    ///     <see cref="IReportViewSurface"/>.
    /// </summary>
    private readonly ReportViewController _controller;

#if DEBUG
    /// <summary>Pinch-commit stopwatch; null when no sample is running.</summary>
    private Stopwatch? _pinchCommitPerf;

    /// <summary>Plan keys at commit (centre-first). Null until the first post-commit request.</summary>
    private TileKey[]? _pinchCommitWanted;

    private bool _pinchCommitFirstLogged;
    private bool _pinchCommitFullLogged;
#endif

    public ReportView()
    {
        _presenter = new ReportViewPresenter(this) { UnitsPerPoint = ReportViewDefaults.UnitsPerPoint };
        _pinch = new PinchSession(_presenter);
        _zoomPublisher = new ZoomPublisher(new Sink(this));

        _reportSession = new ReportViewSession(_presenter, this, new TaskRunScheduler(), TilePixels.Bgra);
        _reportSession.Invalidated += RefreshVisuals;
        _reportSession.Failed += exception => RenderFailed?.Invoke(this, exception);
        _reportSession.Cleared += () =>
        {
            PageCount = 0;
            ClearVisuals();
        };
        _reportSession.Loaded += () =>
        {
            PageCount = _presenter.PageCount;
            _presenter.ViewportChanged();
            AfterPresenterUpdate(immediate: false);
        };

        _controller = new ReportViewController(_presenter, _reportSession, this);
        _controller.Failed += exception => RenderFailed?.Invoke(this, exception);
        _controller.TilesRequested += plan =>
        {
            CapturePinchCommitPlan(plan.Requests);
            SamplePinchCommitPerf();
        };

        // The canvas paints the surface behind the pages itself, rather than leaving it to whatever
        // ancestor happens to have a background. Without its own brush a Canvas draws nothing at all,
        // so an area a page or a cell moved out of during a zoom was never repainted by anyone, and
        // the pixels of the earlier zoom stayed on screen until a scroll disturbed them.
        _canvas.Bind(Panel.BackgroundProperty, this.GetObservable(BackgroundProperty));

        _scroll.Content = _canvas;
        _scroll.ScrollChanged += (_, _) => OnScrolled();

        SizeChanged += (_, _) => OnViewportChanged();

        AddInputHandlers();

        Content = _scroll;
    }

    public static readonly StyledProperty<Report?> ReportProperty =
        AvaloniaProperty.Register<ReportView, Report?>(nameof(Report));

    public static readonly StyledProperty<ReportZoomMode> ZoomModeProperty =
        AvaloniaProperty.Register<ReportView, ReportZoomMode>(
            nameof(ZoomMode), ReportZoomMode.FitWidth, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ReportView, double>(
            nameof(Zoom), 1d, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, value) => Math.Clamp(value, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom));

    public static readonly StyledProperty<double> PageSpacingProperty =
        AvaloniaProperty.Register<ReportView, double>(nameof(PageSpacing), ReportViewDefaults.PageSpacing);

    public static readonly StyledProperty<Color> PageBorderColorProperty =
        AvaloniaProperty.Register<ReportView, Color>(nameof(PageBorderColor), DefaultPageBorderColor);

    public static readonly StyledProperty<double> PageBorderThicknessProperty =
        AvaloniaProperty.Register<ReportView, double>(
            nameof(PageBorderThickness), ReportViewDefaults.PageBorderThickness,
            coerce: (_, value) => Math.Max(0, value));

    public static readonly StyledProperty<Thickness> DocumentPaddingProperty =
        AvaloniaProperty.Register<ReportView, Thickness>(nameof(DocumentPadding), default);

    public static readonly StyledProperty<int> CurrentPageProperty =
        AvaloniaProperty.Register<ReportView, int>(
            nameof(CurrentPage), 1, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> VerticalOverdrawProperty =
        AvaloniaProperty.Register<ReportView, double>(
            nameof(VerticalOverdraw), ReportViewDefaults.VerticalOverdraw,
            coerce: (_, value) => Math.Max(0, value));

    public static readonly StyledProperty<double> RenderBudgetProperty =
        AvaloniaProperty.Register<ReportView, double>(
            nameof(RenderBudget), ReportViewDefaults.RenderBudget, coerce: (_, value) => Math.Max(0, value));

    private int _pageCount;

    public static readonly DirectProperty<ReportView, int> PageCountProperty =
        AvaloniaProperty.RegisterDirect<ReportView, int>(
            nameof(PageCount), o => o.PageCount, defaultBindingMode: BindingMode.OneWayToSource);

    private double _effectiveZoom = 1;

    public static readonly DirectProperty<ReportView, double> EffectiveZoomProperty =
        AvaloniaProperty.RegisterDirect<ReportView, double>(
            nameof(EffectiveZoom), o => o.EffectiveZoom, defaultBindingMode: BindingMode.OneWayToSource);

    /// <summary>The report to show. It must already have been built.</summary>
    public Report? Report
    {
        get => GetValue(ReportProperty);
        set => SetValue(ReportProperty, value);
    }

    public ReportZoomMode ZoomMode
    {
        get => GetValue(ZoomModeProperty);
        set => SetValue(ZoomModeProperty, value);
    }

    /// <summary>The zoom factor used when <see cref="ZoomMode"/> is <c>Custom</c>; 1 is 100%.</summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>The gap between two pages at 100%; part of the document, so it scales with the zoom.</summary>
    public double PageSpacing
    {
        get => GetValue(PageSpacingProperty);
        set => SetValue(PageSpacingProperty, value);
    }

    /// <summary>
    ///     The colour of the line around each page. It is what separates a page from the surface
    ///     behind it, which matters most when that surface is as light as the paper.
    /// </summary>
    public Color PageBorderColor
    {
        get => GetValue(PageBorderColorProperty);
        set => SetValue(PageBorderColorProperty, value);
    }

    /// <summary>The width of the line around each page; 0 leaves the page unframed.</summary>
    public double PageBorderThickness
    {
        get => GetValue(PageBorderThicknessProperty);
        set => SetValue(PageBorderThicknessProperty, value);
    }

    /// <summary>
    ///     The space kept around the pages at 100%: between the edges of the viewport and the document,
    ///     as opposed to <see cref="PageSpacing"/>, which is the gap between two pages. Like the
    ///     spacing it belongs to the document and scales with the zoom, as a PDF viewer's does.
    /// </summary>
    public Thickness DocumentPadding
    {
        get => GetValue(DocumentPaddingProperty);
        set => SetValue(DocumentPaddingProperty, value);
    }

    /// <summary>The page at the top of the viewport, one-based.</summary>
    public int CurrentPage
    {
        get => GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int PageCount
    {
        get => _pageCount;
        private set => SetAndRaise(PageCountProperty, ref _pageCount, value);
    }

    /// <summary>
    ///     How far past the top and the bottom of the viewport a tile is drawn, as a fraction of the
    ///     viewport height. Scrolling within that margin stays sharp because the pixels are already
    ///     there; beyond it the low-resolution base layer shows until the next tile arrives. The
    ///     cost is linear: 0.5 makes a tile twice as tall, and so twice as expensive, as the screen.
    /// </summary>
    public double VerticalOverdraw
    {
        get => GetValue(VerticalOverdrawProperty);
        set => SetValue(VerticalOverdrawProperty, value);
    }

    /// <summary>
    ///     How much memory, in megabytes, the visible pages may occupy before the view stops drawing
    ///     them whole and goes back to drawing only the window. Drawing a page whole is what keeps
    ///     scrolling through it perfectly sharp, and it costs the square of the zoom: an A4 page is
    ///     14 MB at 100% on a two-times display, 57 MB at 200% and 357 MB at 500%.
    /// </summary>
    public double RenderBudget
    {
        get => GetValue(RenderBudgetProperty);
        set => SetValue(RenderBudgetProperty, value);
    }

    /// <summary>Raised when the report could not be prepared or a page could not be drawn.</summary>
    public event EventHandler<Exception>? RenderFailed;

    /// <summary>
    ///     The zoom the current mode resolves to, whatever the mode. In a fit mode this is the only
    ///     way to learn the actual factor, since <see cref="Zoom"/> then holds the last custom value.
    /// </summary>
    public double EffectiveZoom
    {
        get => _effectiveZoom;
        private set => SetAndRaise(EffectiveZoomProperty, ref _effectiveZoom, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ReportProperty)
        {
            _ = StartSessionAsync(change.GetNewValue<Report?>());
        }
        else if (change.Property == ZoomModeProperty || change.Property == ZoomProperty
                 || change.Property == PageSpacingProperty || change.Property == DocumentPaddingProperty)
        {
            OnZoomRelatedChanged();
        }
        else if (change.Property == PageBorderColorProperty || change.Property == PageBorderThicknessProperty)
        {
            OnPageBorderChanged();
        }
        else if (change.Property == CurrentPageProperty)
        {
            OnCurrentPageChanged(change.GetNewValue<int>());
        }
    }

    private void OnZoomRelatedChanged()
    {
        // A gesture has already told the presenter what it did and where to anchor it, and is only
        // writing these properties back so a binding sees them; asking the presenter again here
        // would replace that anchor with the viewport's centre.
        if (_zoomPublisher.Publishing)
            return;

        _presenter.PageSpacing = PageSpacing;
        _presenter.Padding = ToPagePadding(DocumentPadding);

        // No gesture is driving this: a property set from a binding, or from code, holds the middle
        // of the viewport still, exactly as a resize would - except this one is deliberate, so a fit
        // mode changing to a chosen percentage still anchors around the reader's eye.
        _presenter.SetZoom(ZoomMode, Zoom, CenterAnchor());
        AfterPresenterUpdate(immediate: false);
    }

    private void OnPageBorderChanged()
    {
        foreach (var page in _pageViews.Values)
            ApplyPageBorder(page);

        // The line's width is part of where a page is placed, not only of how it looks.
        _presenter.PageBorderThickness = PageBorderThickness;
        _presenter.ViewportChanged();
        AfterPresenterUpdate(immediate: false);
    }

    private void OnCurrentPageChanged(int page)
    {
        // Reporting where the scroll landed must not be mistaken for a request to go there - see
        // IReportViewSurface.ReportState.
        if (_reportingCurrentPage)
            return;

        _presenter.GoToPage(page);
    }

    /// <summary>
    ///     Publishes what the presenter now knows: the page at the top of the viewport, and the zoom
    ///     the current mode resolved to.
    /// </summary>
    void IReportViewSurface.ReportState(int currentPage, double effectiveZoom)
    {
        _reportingCurrentPage = true;
        try
        {
            CurrentPage = currentPage;
        }
        finally
        {
            _reportingCurrentPage = false;
        }

        EffectiveZoom = effectiveZoom;
    }

    private Task StartSessionAsync(Report? report)
        => _reportSession.LoadAsync(report, ReportViewRenderer.Instance);

    /// <summary>
    ///     Reacts to the view having scrolled.
    /// </summary>
    /// <remarks>
    ///     A tile that already covers the page is reused, so asking on every event costs nothing, and
    ///     a page scrolled into is drawn the moment it appears rather than after a wait.
    /// </remarks>
    private void OnScrolled() => _controller.Scrolled();

    /// <summary>
    ///     A pinch is shown through the canvas's transform, and the pages stay where the zoom the
    ///     gesture started at put them. Reacting to a scroll while that is on screen would relay them
    ///     out mid-gesture, which is the one thing the transform exists to avoid.
    /// </summary>
    bool IReportViewSurface.SuppressesViewportReaction => _pinch.Running;

    (double VerticalOverdraw, double RenderBudget) IReportViewSurface.TilePolicy
        => (VerticalOverdraw, RenderBudget);

    void IReportViewSurface.InvalidateSurface() => _canvas.InvalidateVisual();

    // Forwarded rather than renamed: these are called from this control's own code far more often
    // than through the interface.
    void IReportViewSurface.RefreshVisuals() => RefreshVisuals();

    void IReportViewSurface.ClearVisuals() => ClearVisuals();

    /// <summary>
    ///     Reacts to the view having changed size. Zoom and resize invalidate every tile at once, so
    ///     unlike a scroll this waits for the view to settle before asking for anything sharp.
    /// </summary>
    private void OnViewportChanged() => _controller.ViewportChanged();

    /// <summary>Redraws what changed, and asks for tiles now or once the view settles.</summary>
    /// <remarks>
    ///     Anything but a scroll moves every page and every cell at once, and the areas they move out
    ///     of are not all marked for repainting on their own - which left pixels of the previous zoom
    ///     in the gaps between pages and along their edges until a scroll disturbed them. Marking the
    ///     canvas dirty whole is what covers them, and it costs one repaint of the viewport per zoom
    ///     step rather than per frame, since a scroll never comes through here.
    /// </remarks>
    private void AfterPresenterUpdate(bool immediate) => _controller.AfterPresenterUpdate(immediate);

    /// <summary>The scroll viewer's own width, which every measurement here is against; never zero.</summary>
    private double ViewportWidth => Math.Max(1, _scroll.Viewport.Width);

    /// <summary>The scroll viewer's own height, which every measurement here is against; never zero.</summary>
    private double ViewportHeight => Math.Max(1, _scroll.Viewport.Height);

    /// <summary>Device pixels per device independent unit, which cells are rendered against.</summary>
    private double Density => TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

    /// <summary>The centre of the viewport, in the units <see cref="ReportViewPresenter.SetZoom"/> anchors by.</summary>
    private ViewPoint CenterAnchor() => new(ViewportWidth / 2, ViewportHeight / 2);

    private static PagePadding ToPagePadding(Thickness padding)
        => new(padding.Left, padding.Top, padding.Right, padding.Bottom);

    /// <summary>
    ///     Starts a DEBUG-only sample for pinch release → first centre tile → full viewport plan.
    ///     Call from <see cref="CommitPinch"/> before the relayout that requests tiles.
    /// </summary>
    private void BeginPinchCommitPerf()
    {
#if DEBUG
        _pinchCommitPerf = Stopwatch.StartNew();
        _pinchCommitWanted = null;
        _pinchCommitFirstLogged = false;
        _pinchCommitFullLogged = false;
        Debug.WriteLine("[QReport.Perf] t_commit");
#endif
    }

    private void CapturePinchCommitPlan(IReadOnlyList<TileRequest> requests)
    {
#if DEBUG
        if (_pinchCommitPerf is null || _pinchCommitWanted is not null)
            return;

        _pinchCommitWanted = new TileKey[requests.Count];
        for (var i = 0; i < requests.Count; i++)
            _pinchCommitWanted[i] = requests[i].Key;

        if (_pinchCommitWanted.Length == 0)
        {
            Debug.WriteLine("[QReport.Perf] t_viewport_full 0 ms (empty plan)");
            _pinchCommitFullLogged = true;
            _pinchCommitPerf = null;
        }
#endif
    }

    /// <summary>
    ///     Logs t_first_centre_tile and t_viewport_full once each while a pinch-commit sample runs.
    ///     Centre = first key of the centre-ordered plan; full = every plan key present in tile views.
    /// </summary>
    private void SamplePinchCommitPerf()
    {
#if DEBUG
        if (_pinchCommitPerf is null || _pinchCommitWanted is null || _pinchCommitFullLogged)
            return;

        if (!_pinchCommitFirstLogged && _pinchCommitWanted.Length > 0
            && _tileViews.ContainsKey(_pinchCommitWanted[0]))
        {
            _pinchCommitFirstLogged = true;
            Debug.WriteLine(
                $"[QReport.Perf] t_first_centre_tile {_pinchCommitPerf.ElapsedMilliseconds} ms");
        }

        var full = true;
        foreach (var key in _pinchCommitWanted)
        {
            if (_tileViews.ContainsKey(key))
                continue;

            full = false;
            break;
        }

        if (!full)
            return;

        _pinchCommitFullLogged = true;
        Debug.WriteLine(
            $"[QReport.Perf] t_viewport_full {_pinchCommitPerf.ElapsedMilliseconds} ms (n={_pinchCommitWanted.Length})");
        _pinchCommitPerf = null;
#endif
    }

    /// <summary>
    ///     Brings the views in the scroll content in line with the cells the cache has drawn now.
    /// </summary>
    /// <remarks>
    ///     Called when something changes - a scroll into new pages, a zoom, a cell arriving - and
    ///     never per frame. Between those moments the platform scrolls these views itself, which is
    ///     the whole point: a viewer that repaints its own pixels can never keep pace with a gesture.
    ///     Where the pages themselves sit is the presenter's own doing, applied through
    ///     <see cref="IReportViewHost.PlacePage"/> as soon as it is asked to update.
    /// </remarks>
    private void RefreshVisuals()
    {
        if (_reportSession.Tiles is not { PageCount: > 0 })
        {
            ClearVisuals();
            return;
        }

        _presenter.PlaceTiles(_tileViews.Keys.ToList());
        SamplePinchCommitPerf();
    }

    private void ApplyPageBorder(Border page)
    {
        page.BorderBrush = new SolidColorBrush(PageBorderColor);
        page.BorderThickness = new Thickness(PageBorderThickness);
        page.Padding = new Thickness(PageBorderThickness);
    }

    private void ClearVisuals()
    {
        _canvas.Children.Clear();
        _pageViews.Clear();
        _tileViews.Clear();
    }

    // -- IReportViewHost -----------------------------------------------------------------------

    double IReportViewHost.ViewportWidth => ViewportWidth;

    double IReportViewHost.ViewportHeight => ViewportHeight;

    double IReportViewHost.ScrollX => _scroll.Offset.X;

    double IReportViewHost.ScrollY => _scroll.Offset.Y;

    double IReportViewHost.Density => Density;

    void IReportViewHost.ScrollTo(double x, double y) => _scroll.Offset = new Vector(x, y);

    void IReportViewHost.SetExtent(double width, double height)
    {
        // Guarded because writing an unchanged value invalidates the layout, which raises another
        // scroll/size change, which lands back here - see ExtentWrite.Needed for why the guard has
        // to treat NaN specially.
        var changed = false;

        if (ExtentWrite.Needed(_canvas.Width, width))
        {
            _canvas.Width = width;
            changed = true;
        }

        if (ExtentWrite.Needed(_canvas.Height, height))
        {
            _canvas.Height = height;
            changed = true;
        }

        // The presenter scrolls in the same turn: without this pass the ScrollViewer still measures
        // the previous canvas and clamps that scroll against the old document height.
        if (changed)
            _scroll.UpdateLayout();
    }

    void IReportViewHost.PlacePage(int index, ViewRect bounds)
    {
        if (!_pageViews.TryGetValue(index, out var page))
        {
            var image = new Image { Stretch = Stretch.Fill };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            page = new Border
            {
                Background = Brushes.White,
                Child = image
            };

            ApplyPageBorder(page);

            _pageViews[index] = page;

            // Behind every cell: the pages are the backdrop the sharp cells are laid over.
            _canvas.Children.Insert(0, page);
        }

        if (page.Child is Image { Source: null } pageImage && _reportSession.Tiles?.BaseLayer(index) is { } baseLayer)
            pageImage.Source = new Bitmap(new MemoryStream(baseLayer));

        Canvas.SetLeft(page, bounds.X);
        Canvas.SetTop(page, bounds.Y);
        page.Width = bounds.Width;
        page.Height = bounds.Height;
    }

    void IReportViewHost.PlaceTile(ViewRect bounds, Tile tile)
    {
        if (!_tileViews.TryGetValue(tile.Key, out var placed))
        {
            var image = new Image { Stretch = Stretch.Fill };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            placed = new TileView(image);

            _tileViews[tile.Key] = placed;
            _canvas.Children.Add(image);
        }

        placed.Show(tile.Bytes, tile.PixelWidth, tile.PixelHeight);

        Canvas.SetLeft(placed.Image, bounds.X);
        Canvas.SetTop(placed.Image, bounds.Y);
        placed.Image.Width = bounds.Width;
        placed.Image.Height = bounds.Height;
    }

    void IReportViewHost.RemoveTile(TileKey key)
    {
        if (!_tileViews.TryGetValue(key, out var placed))
            return;

        _canvas.Children.Remove(placed.Image);
        _tileViews.Remove(key);
    }

    /// <summary>A cell on screen, and the raw BGRA bytes it was built from.</summary>
    /// <remarks>
    ///     A key names its pixels for as long as one report is loaded - a cell's region and content
    ///     follow from its page, column, row and scale, and another report clears these views - so the
    ///     bytes never change under a key today. Kept anyway, and compared, so that a change to what a
    ///     key means cannot quietly leave a cell showing the pixels it was created with. Compared by
    ///     reference rather than rebuilt: this runs for every visible cell on every refresh.
    /// </remarks>
    private sealed class TileView(Image image)
    {
        private byte[]? _bytes;

        public Image Image { get; } = image;

        public void Show(byte[] bytes, int pixelWidth, int pixelHeight)
        {
            if (ReferenceEquals(_bytes, bytes))
                return;

            _bytes = bytes;

            var bitmap = new WriteableBitmap(
                new PixelSize(pixelWidth, pixelHeight),
                new Vector(96, 96),
                global::Avalonia.Platform.PixelFormats.Bgra8888,
                global::Avalonia.Platform.AlphaFormat.Unpremul);

            using (var framebuffer = bitmap.Lock())
            {
                var sourceStride = pixelWidth * 4;
                var destinationStride = framebuffer.RowBytes;

                if (sourceStride == destinationStride)
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        bytes, 0, framebuffer.Address, sourceStride * pixelHeight);
                }
                else
                {
                    for (var row = 0; row < pixelHeight; row++)
                    {
                        System.Runtime.InteropServices.Marshal.Copy(
                            bytes,
                            row * sourceStride,
                            framebuffer.Address + row * destinationStride,
                            sourceStride);
                    }
                }
            }

            Image.Source = bitmap;
        }
    }

    void IReportViewHost.Post(Action action) => Dispatcher.UIThread.Post(action);

    /// <summary>The two writes <see cref="ZoomPublisher"/> orders, as this control performs them.</summary>
    private sealed class Sink(ReportView view) : IZoomSink
    {
        public void SetZoomFactor(double zoom) => view.Zoom = zoom;

        public void SetZoomMode(ReportZoomMode mode) => view.ZoomMode = mode;
    }
}

/// <summary>
///     The renderer the control measures reports with. A single instance carries the drawers the
///     application registered through <c>UseQReport</c>.
/// </summary>
internal static class ReportViewRenderer
{
    private static SkiaReportRenderer? _instance;

    /// <summary>
    ///     The renderer installed by <c>UseQReport</c>. Throws rather than falling back to an
    ///     unconfigured renderer: without the fonts and asset access <c>UseQReport</c> installs, a
    ///     report renders with substitute fonts and blank images instead of reporting the mistake.
    /// </summary>
    public static SkiaReportRenderer Instance
    {
        get => _instance ?? throw new InvalidOperationException(
            "Call UseQReport during application startup before using a report view or the renderer.");
        set => _instance = value;
    }
}
