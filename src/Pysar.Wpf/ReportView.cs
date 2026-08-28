using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Pysar.Elements;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;

// Report elements and WPF controls share several names; the report side is the one aliased
// away here because this file is a WPF control first.
using Image = System.Windows.Controls.Image;
// Parent namespace Pysar has a Binding child namespace; do not use the short name Binding.
using WpfBinding = System.Windows.Data.Binding;

namespace Pysar.Wpf;

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
///     properties. Input is a separate file.
/// </remarks>
public partial class ReportView : UserControl, IReportViewHost, IReportViewSurface
{
    /// <summary>The line around a page unless the host asks for another one.</summary>
    private static readonly Color DefaultPageBorderColor =
        (Color)ColorConverter.ConvertFromString(ReportViewDefaults.PageBorderColorHex)!;

    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
    };

    // The pages and cells themselves: real views inside the scroll viewer, so the platform moves
    // them while scrolling instead of the application repainting them. Top/left when the canvas is
    // somehow still shorter than the viewport - undersized content must not be centred, which is
    // not how a document viewer lays pages out.
    private readonly Canvas _canvas = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
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
    ///     hosts; what stays here is only what WPF itself has to do, through
    ///     <see cref="IReportViewSurface"/>.
    /// </summary>
    private readonly ReportViewController _controller;

    /// <summary>
    ///     Orders the two writes back to <see cref="Zoom"/> and <see cref="ZoomMode"/> after an input
    ///     handler has already told the presenter what it did and where to anchor it, and flags that
    ///     the property-changed handler is seeing its own write rather than a fresh request.
    /// </summary>
    private readonly ZoomPublisher _zoomPublisher;

    public ReportView()
    {
        _presenter = new ReportViewPresenter(this) { UnitsPerPoint = ReportViewDefaults.UnitsPerPoint };
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

        // Unloaded also fires when a control is reparented, so the session decides on the next turn
        // of the dispatcher, by which point a reparented control is loaded again.
        Unloaded += (_, _) => _reportSession.DisposeWhenStillDetached(() => IsLoaded);

        // The canvas paints the surface behind the pages itself, rather than leaving it to whatever
        // ancestor happens to have a background. Without its own brush a Canvas draws nothing at all,
        // so an area a page or a cell moved out of during a zoom was never repainted by anyone, and
        // the pixels of the earlier zoom stayed on screen until a scroll disturbed them.
        BindingOperations.SetBinding(
            _canvas,
            Panel.BackgroundProperty,
            new WpfBinding(nameof(Background)) { Source = this });

        _scroll.Content = _canvas;
        _scroll.ScrollChanged += (_, _) => OnScrolled();

        SizeChanged += (_, _) => OnViewportChanged();

        AddInputHandlers();

        Content = _scroll;
    }

    /// <summary>Wires desktop input; implemented in <c>ReportViewInput</c>.</summary>
    partial void AddInputHandlers();

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        OnViewportChanged();
    }

    public static readonly DependencyProperty ReportProperty =
        DependencyProperty.Register(
            nameof(Report),
            typeof(Report),
            typeof(ReportView),
            new PropertyMetadata(null, OnReportChanged));

    public static readonly DependencyProperty ZoomModeProperty =
        DependencyProperty.Register(
            nameof(ZoomMode),
            typeof(ReportZoomMode),
            typeof(ReportView),
            new FrameworkPropertyMetadata(
                ReportZoomMode.FitWidth,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnZoomRelatedPropertyChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(double),
            typeof(ReportView),
            new FrameworkPropertyMetadata(
                1d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnZoomRelatedPropertyChanged,
                CoerceZoom));

    public static readonly DependencyProperty PageSpacingProperty =
        DependencyProperty.Register(
            nameof(PageSpacing),
            typeof(double),
            typeof(ReportView),
            new PropertyMetadata(ReportViewDefaults.PageSpacing, OnZoomRelatedPropertyChanged));

    public static readonly DependencyProperty PageBorderColorProperty =
        DependencyProperty.Register(
            nameof(PageBorderColor),
            typeof(Color),
            typeof(ReportView),
            new PropertyMetadata(DefaultPageBorderColor, OnPageBorderPropertyChanged));

    public static readonly DependencyProperty PageBorderThicknessProperty =
        DependencyProperty.Register(
            nameof(PageBorderThickness),
            typeof(double),
            typeof(ReportView),
            new PropertyMetadata(
                ReportViewDefaults.PageBorderThickness, OnPageBorderPropertyChanged, CoerceNonNegativeDouble));

    public static readonly DependencyProperty DocumentPaddingProperty =
        DependencyProperty.Register(
            nameof(DocumentPadding),
            typeof(Thickness),
            typeof(ReportView),
            new PropertyMetadata(default(Thickness), OnZoomRelatedPropertyChanged));

    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(
            nameof(CurrentPage),
            typeof(int),
            typeof(ReportView),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCurrentPagePropertyChanged));

    public static readonly DependencyProperty VerticalOverdrawProperty =
        DependencyProperty.Register(
            nameof(VerticalOverdraw),
            typeof(double),
            typeof(ReportView),
            new PropertyMetadata(ReportViewDefaults.VerticalOverdraw, null, CoerceNonNegativeDouble));

    public static readonly DependencyProperty RenderBudgetProperty =
        DependencyProperty.Register(
            nameof(RenderBudget),
            typeof(double),
            typeof(ReportView),
            new PropertyMetadata(ReportViewDefaults.RenderBudget, null, CoerceNonNegativeDouble));

    public static readonly DependencyProperty PageCountProperty =
        DependencyProperty.Register(
            nameof(PageCount),
            typeof(int),
            typeof(ReportView),
            new FrameworkPropertyMetadata(0));

    public static readonly DependencyProperty EffectiveZoomProperty =
        DependencyProperty.Register(
            nameof(EffectiveZoom),
            typeof(double),
            typeof(ReportView),
            new FrameworkPropertyMetadata(1d));

    /// <summary>The report to show. It must already have been built.</summary>
    public Report? Report
    {
        get => (Report?)GetValue(ReportProperty);
        set => SetValue(ReportProperty, value);
    }

    public ReportZoomMode ZoomMode
    {
        get => (ReportZoomMode)GetValue(ZoomModeProperty);
        set => SetValue(ZoomModeProperty, value);
    }

    /// <summary>The zoom factor used when <see cref="ZoomMode"/> is <c>Custom</c>; 1 is 100%.</summary>
    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>The gap between two pages at 100%; part of the document, so it scales with the zoom.</summary>
    public double PageSpacing
    {
        get => (double)GetValue(PageSpacingProperty);
        set => SetValue(PageSpacingProperty, value);
    }

    /// <summary>
    ///     The colour of the line around each page. It is what separates a page from the surface
    ///     behind it, which matters most when that surface is as light as the paper.
    /// </summary>
    public Color PageBorderColor
    {
        get => (Color)GetValue(PageBorderColorProperty);
        set => SetValue(PageBorderColorProperty, value);
    }

    /// <summary>The width of the line around each page; 0 leaves the page unframed.</summary>
    public double PageBorderThickness
    {
        get => (double)GetValue(PageBorderThicknessProperty);
        set => SetValue(PageBorderThicknessProperty, value);
    }

    /// <summary>
    ///     The space kept around the pages at 100%: between the edges of the viewport and the document,
    ///     as opposed to <see cref="PageSpacing"/>, which is the gap between two pages. Like the
    ///     spacing it belongs to the document and scales with the zoom, as a PDF viewer's does.
    /// </summary>
    public Thickness DocumentPadding
    {
        get => (Thickness)GetValue(DocumentPaddingProperty);
        set => SetValue(DocumentPaddingProperty, value);
    }

    /// <summary>The page at the top of the viewport, one-based.</summary>
    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public int PageCount
    {
        get => (int)GetValue(PageCountProperty);
        // Public: WPF XAML OneWayToSource bindings require an accessible setter on the target.
        set => SetValue(PageCountProperty, value);
    }

    /// <summary>
    ///     How far past the top and the bottom of the viewport a tile is drawn, as a fraction of the
    ///     viewport height. Scrolling within that margin stays sharp because the pixels are already
    ///     there; beyond it the low-resolution base layer shows until the next tile arrives. The
    ///     cost is linear: 0.5 makes a tile twice as tall, and so twice as expensive, as the screen.
    /// </summary>
    public double VerticalOverdraw
    {
        get => (double)GetValue(VerticalOverdrawProperty);
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
        get => (double)GetValue(RenderBudgetProperty);
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
        get => (double)GetValue(EffectiveZoomProperty);
        // Public: WPF XAML OneWayToSource bindings require an accessible setter on the target.
        set => SetValue(EffectiveZoomProperty, value);
    }

    private static object CoerceZoom(DependencyObject dependencyObject, object baseValue)
        => Math.Clamp((double)baseValue, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom);

    private static object CoerceNonNegativeDouble(DependencyObject dependencyObject, object baseValue)
        => Math.Max(0, (double)baseValue);

    private static void OnReportChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var view = (ReportView)dependencyObject;
        _ = view.StartSessionAsync((Report?)args.NewValue);
    }

    private static void OnZoomRelatedPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
        => ((ReportView)dependencyObject).OnZoomRelatedChanged();

    private static void OnPageBorderPropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
        => ((ReportView)dependencyObject).OnPageBorderChanged();

    private static void OnCurrentPagePropertyChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
        => ((ReportView)dependencyObject).OnCurrentPageChanged((int)args.NewValue);

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
    ///     Always false, unlike the other three hosts: this control has no pinch. A wheel notch and a
    ///     double click each relay out once, in one turn, so there is never a moment where what is on
    ///     screen disagrees with what the presenter has been told.
    /// </summary>
    bool IReportViewSurface.SuppressesViewportReaction => false;

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
    private double ViewportWidth => Math.Max(1, _scroll.ViewportWidth);

    /// <summary>The scroll viewer's own height, which every measurement here is against; never zero.</summary>
    private double ViewportHeight => Math.Max(1, _scroll.ViewportHeight);

    /// <summary>Device pixels per device independent unit, which cells are rendered against.</summary>
    private double Density =>
        IsLoaded ? VisualTreeHelper.GetDpi(this).PixelsPerDip : 1d;

    /// <summary>The centre of the viewport, in the units <see cref="ReportViewPresenter.SetZoom"/> anchors by.</summary>
    private ViewPoint CenterAnchor() => new(ViewportWidth / 2, ViewportHeight / 2);

    private static PagePadding ToPagePadding(Thickness padding)
        => new(padding.Left, padding.Top, padding.Right, padding.Bottom);

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

    double IReportViewHost.ScrollX => _scroll.HorizontalOffset;

    double IReportViewHost.ScrollY => _scroll.VerticalOffset;

    double IReportViewHost.Density => Density;

    void IReportViewHost.ScrollTo(double x, double y)
    {
        _scroll.ScrollToHorizontalOffset(x);
        _scroll.ScrollToVerticalOffset(y);
    }

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
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

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
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(baseLayer);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            pageImage.Source = bitmap;
        }

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
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

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
                pixelWidth,
                pixelHeight,
                96,
                96,
                PixelFormats.Bgra32,
                null);

            bitmap.WritePixels(
                new Int32Rect(0, 0, pixelWidth, pixelHeight),
                bytes,
                pixelWidth * 4,
                0);
            bitmap.Freeze();

            Image.Source = bitmap;
        }
    }

    void IReportViewHost.Post(Action action)
        => Dispatcher.BeginInvoke(action, DispatcherPriority.Normal);

    /// <summary>The two writes <see cref="ZoomPublisher"/> orders, as this control performs them.</summary>
    private sealed class Sink(ReportView view) : IZoomSink
    {
        public void SetZoomFactor(double zoom) => view.Zoom = zoom;

        public void SetZoomMode(ReportZoomMode mode) => view.ZoomMode = mode;
    }
}
