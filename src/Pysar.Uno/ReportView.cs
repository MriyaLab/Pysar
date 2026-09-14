using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Pysar.Elements;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;
using Windows.Storage.Streams;
using Windows.UI;

// Report elements and WinUI controls share several names; the report side is the one aliased away
// here because this file is a WinUI control first.
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace Pysar.Uno;

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
///     properties. Input is in <c>ReportViewInput.cs</c>.
/// </remarks>
public partial class ReportView : UserControl, IReportViewHost, IReportViewSurface
{
    /// <summary>The line around a page unless the host asks for another one.</summary>
    private static readonly Color DefaultPageBorderColor = ParseHex(ReportViewDefaults.PageBorderColorHex);

    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollMode = ScrollMode.Auto,
        VerticalScrollMode = ScrollMode.Auto,

        // The control zooms through the presenter, so the ScrollViewer's own zoom would be a second,
        // uncoordinated source of scale - and the one the presenter could not see.
        ZoomMode = Microsoft.UI.Xaml.Controls.ZoomMode.Disabled
    };

    // The pages and cells themselves: real views inside the scroll viewer, so the platform moves
    // them while scrolling instead of the application repainting them. Top/left because a
    // ScrollViewer centres undersized content by default, which is not how a document viewer lays
    // pages out.
    private readonly Canvas _canvas = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,

        // Without a brush a Canvas is not hit-testable at all, and every input handler in
        // ReportViewInput.cs is attached to it rather than to the scroll viewer.
        Background = new SolidColorBrush(Colors.Transparent)
    };

    private readonly Dictionary<int, Border> _pageViews = [];
    private readonly Dictionary<TileKey, TileView> _tileViews = [];

    private readonly ReportViewPresenter _presenter;

    private readonly ReportViewSession _reportSession;

    /// <summary>
    ///     Set while <see cref="CurrentPage"/> is being written from the presenter's own report of
    ///     where the scroll landed, so that write is not mistaken for a request to scroll there.
    /// </summary>
    /// <remarks>
    ///     Load-bearing in a way it is not in the Avalonia host: a WinUI dependency-property callback
    ///     fires on every write, including the control's own, so without this guard reporting the
    ///     current page would immediately be read back as a request to navigate to it.
    /// </remarks>
    private bool _reportingCurrentPage;

    /// <summary>
    ///     The order this control follows between an input and the pixels. Shared with the other
    ///     hosts; what stays here is only what WinUI itself has to do, through
    ///     <see cref="IReportViewSurface"/>.
    /// </summary>
    private readonly ReportViewController _controller;

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

        _scroll.Content = _canvas;
        _scroll.ViewChanged += (_, _) => OnScrolled();

        SizeChanged += (_, _) => OnViewportChanged();

        // Unloading is also what reparenting looks like, so the session decides on the next turn of
        // the loop, by which point a reparented control has been loaded again. IsLoaded rather than
        // XamlRoot: a WinUI element keeps its XamlRoot reference after it unloads, so asking that
        // would report every detached control as still attached and never release its tiles.
        Unloaded += (_, _) => _reportSession.DisposeWhenStillDetached(() => IsLoaded);

        AddInputHandlers();

        Content = _scroll;
    }

    // -- Dependency properties -----------------------------------------------------------------

    public static readonly DependencyProperty ReportProperty = DependencyProperty.Register(
        nameof(Report), typeof(Report), typeof(ReportView),
        new PropertyMetadata(null, (d, e) => _ = ((ReportView)d).StartSessionAsync(e.NewValue as Report)));

    public static readonly DependencyProperty ZoomModeProperty = DependencyProperty.Register(
        nameof(ZoomMode), typeof(ReportZoomMode), typeof(ReportView),
        new PropertyMetadata(ReportZoomMode.FitWidth, (d, _) => ((ReportView)d).OnZoomRelatedChanged()));

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom), typeof(double), typeof(ReportView),
        new PropertyMetadata(1d, (d, e) => ((ReportView)d).OnCoercedChanged(
            e, CoerceZoom, static v => v.OnZoomRelatedChanged())));

    public static readonly DependencyProperty PageSpacingProperty = DependencyProperty.Register(
        nameof(PageSpacing), typeof(double), typeof(ReportView),
        new PropertyMetadata(ReportViewDefaults.PageSpacing, (d, _) => ((ReportView)d).OnZoomRelatedChanged()));

    public static readonly DependencyProperty PageBorderColorProperty = DependencyProperty.Register(
        nameof(PageBorderColor), typeof(Color), typeof(ReportView),
        new PropertyMetadata(DefaultPageBorderColor, (d, _) => ((ReportView)d).OnPageBorderChanged()));

    public static readonly DependencyProperty PageBorderThicknessProperty = DependencyProperty.Register(
        nameof(PageBorderThickness), typeof(double), typeof(ReportView),
        new PropertyMetadata(ReportViewDefaults.PageBorderThickness, (d, e) => ((ReportView)d).OnCoercedChanged(
            e, CoerceNonNegative, static v => v.OnPageBorderChanged())));

    public static readonly DependencyProperty DocumentPaddingProperty = DependencyProperty.Register(
        nameof(DocumentPadding), typeof(Thickness), typeof(ReportView),
        new PropertyMetadata(default(Thickness), (d, _) => ((ReportView)d).OnZoomRelatedChanged()));

    public static readonly DependencyProperty CurrentPageProperty = DependencyProperty.Register(
        nameof(CurrentPage), typeof(int), typeof(ReportView),
        new PropertyMetadata(1, (d, e) => ((ReportView)d).OnCurrentPageChanged((int)e.NewValue)));

    public static readonly DependencyProperty PageCountProperty = DependencyProperty.Register(
        nameof(PageCount), typeof(int), typeof(ReportView), new PropertyMetadata(0));

    public static readonly DependencyProperty EffectiveZoomProperty = DependencyProperty.Register(
        nameof(EffectiveZoom), typeof(double), typeof(ReportView), new PropertyMetadata(1d));

    public static readonly DependencyProperty VerticalOverdrawProperty = DependencyProperty.Register(
        nameof(VerticalOverdraw), typeof(double), typeof(ReportView),
        new PropertyMetadata(ReportViewDefaults.VerticalOverdraw, (d, e) => ((ReportView)d).OnCoercedChanged(
            e, CoerceNonNegative, null)));

    public static readonly DependencyProperty RenderBudgetProperty = DependencyProperty.Register(
        nameof(RenderBudget), typeof(double), typeof(ReportView),
        new PropertyMetadata(ReportViewDefaults.RenderBudget, (d, e) => ((ReportView)d).OnCoercedChanged(
            e, CoerceNonNegative, null)));

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
    /// <remarks>
    ///     Clamped in the setter. WinUI has no coercion callback, unlike Avalonia's
    ///     <c>StyledProperty</c>, so nothing in this package may write
    ///     <see cref="ZoomProperty"/> through <c>SetValue</c> directly - the clamp would be skipped.
    /// </remarks>
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
    ///     The space kept around the pages at 100%: between the edges of the viewport and the
    ///     document, as opposed to <see cref="PageSpacing"/>, which is the gap between two pages.
    ///     Like the spacing it belongs to the document and scales with the zoom, as a PDF viewer's does.
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
        private set => SetValue(PageCountProperty, value);
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
    ///     scrolling through it perfectly sharp, and it costs the square of the zoom.
    /// </summary>
    public double RenderBudget
    {
        get => (double)GetValue(RenderBudgetProperty);
        set => SetValue(RenderBudgetProperty, value);
    }

    /// <summary>
    ///     The zoom the current mode resolves to, whatever the mode. In a fit mode this is the only
    ///     way to learn the actual factor, since <see cref="Zoom"/> then holds the last custom value.
    /// </summary>
    public double EffectiveZoom
    {
        get => (double)GetValue(EffectiveZoomProperty);
        private set => SetValue(EffectiveZoomProperty, value);
    }

    /// <summary>Raised when the report could not be prepared or a page could not be drawn.</summary>
    public event EventHandler<Exception>? RenderFailed;

    // -- Reactions -----------------------------------------------------------------------------

    /// <summary>
    ///     Applies a dependency property's value limits, then runs its reaction.
    /// </summary>
    /// <remarks>
    ///     This is where the limits have to live. WinUI has no coercion callback, and a limit applied
    ///     in the CLR property setter is skipped by every write that matters: XAML attributes and
    ///     bindings call <c>SetValue</c> directly and never go through the setter. The Avalonia host
    ///     gets this for free from <c>StyledProperty</c>'s <c>coerce</c>, which covers every write.
    ///
    ///     Writing the coerced value back re-enters this callback once, and that second pass agrees
    ///     with its own coercion and so runs the reaction. It terminates because
    ///     <paramref name="coerce"/> is required to be idempotent and to never return NaN - a NaN
    ///     would never compare equal to itself and would loop for ever.
    /// </remarks>
    private void OnCoercedChanged(
        DependencyPropertyChangedEventArgs change, Func<double, double> coerce, Action<ReportView>? react)
    {
        var value = (double)change.NewValue;
        var coerced = coerce(value);

        if (coerced != value)
        {
            // Taken from the change rather than named at the call site: a callback declared in a
            // static field's own initializer cannot refer to that field without the compiler
            // treating it as possibly uninitialised.
            SetValue(change.Property, coerced);
            return;
        }

        react?.Invoke(this);
    }

    /// <summary>Holds the zoom inside the range the shared zoom arithmetic clamps to.</summary>
    private static double CoerceZoom(double zoom)
        => double.IsNaN(zoom) ? 1d : Math.Clamp(zoom, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom);

    /// <summary>Keeps a measurement at or above zero; NaN becomes zero rather than propagating.</summary>
    private static double CoerceNonNegative(double value)
        => double.IsNaN(value) ? 0d : Math.Max(0, value);

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
        // of the viewport still, exactly as a resize would - so a fit mode changing to a chosen
        // percentage still anchors around the reader's eye.
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

    private Task StartSessionAsync(Report? report)
        => _reportSession.LoadAsync(report, ReportViewRenderer.Instance);

    /// <summary>
    ///     Reacts to the view having scrolled. A tile that already covers the page is reused, so
    ///     asking on every event costs nothing, and a page scrolled into is drawn the moment it
    ///     appears rather than after a wait.
    /// </summary>
    private void OnScrolled() => _controller.Scrolled();

    /// <summary>
    ///     Reacts to the view having changed size. Zoom and resize invalidate every tile at once, so
    ///     unlike a scroll this waits for the view to settle before asking for anything sharp.
    /// </summary>
    private void OnViewportChanged() => _controller.ViewportChanged();

    /// <summary>Redraws what changed, and asks for tiles now or once the view settles.</summary>
    private void AfterPresenterUpdate(bool immediate) => _controller.AfterPresenterUpdate(immediate);

    /// <summary>The scroll viewer's own width, which every measurement here is against; never zero.</summary>
    private double ViewportWidth => Math.Max(1, _scroll.ViewportWidth);

    /// <summary>The scroll viewer's own height, which every measurement here is against; never zero.</summary>
    private double ViewportHeight => Math.Max(1, _scroll.ViewportHeight);

    /// <summary>Device pixels per layout unit, which cells are rendered against.</summary>
    private double Density => XamlRoot?.RasterizationScale ?? 1;

    /// <summary>The centre of the viewport, in the units <see cref="ReportViewPresenter.SetZoom"/> anchors by.</summary>
    private ViewPoint CenterAnchor() => new(ViewportWidth / 2, ViewportHeight / 2);

    private static PagePadding ToPagePadding(Thickness padding)
        => new(padding.Left, padding.Top, padding.Right, padding.Bottom);

    /// <summary>
    ///     Reads the shared default border colour. <c>ReportViewDefaults</c> carries it as a hex
    ///     string so no framework's colour type appears in the shared viewer, and WinUI has no
    ///     <c>Color.Parse</c> to read one back.
    /// </summary>
    /// <remarks>
    ///     Six digits only, deliberately: <see cref="ReportViewDefaults.PageBorderColorHex"/>
    ///     documents that an eight-digit form is not read the same way by every host, and is
    ///     required to stay <c>#RRGGBB</c>. Accepting eight here would quietly permit the form that
    ///     invariant exists to prevent.
    /// </remarks>
    private static Color ParseHex(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);

        return Color.FromArgb(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    // -- IReportViewSurface --------------------------------------------------------------------

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

    /// <summary>
    ///     A pinch is shown through the canvas's transform, and the pages stay where the zoom the
    ///     gesture started at put them. Reacting to a scroll while that is on screen would relay them
    ///     out mid-gesture, which is the one thing the transform exists to avoid.
    /// </summary>
    bool IReportViewSurface.SuppressesViewportReaction => _pinch.Running;

    (double VerticalOverdraw, double RenderBudget) IReportViewSurface.TilePolicy
        => (VerticalOverdraw, RenderBudget);

    /// <summary>
    ///     Empty, as this method's own documentation provides for: WinUI repaints its surface when
    ///     its children change, so there is nothing for a host to ask for here. The Avalonia host
    ///     needs the opposite - a Canvas there leaves the area a page moved out of unpainted.
    /// </summary>
    void IReportViewSurface.InvalidateSurface()
    {
    }

    // Forwarded rather than renamed: these are called from this control's own code far more often
    // than through the interface.
    void IReportViewSurface.RefreshVisuals() => RefreshVisuals();

    void IReportViewSurface.ClearVisuals() => ClearVisuals();

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

    /// <summary>
    ///     Scrolls the view, without animation. An animated <c>ChangeView</c> reaches the offset over
    ///     several frames, so the presenter's own reads of <see cref="IReportViewHost.ScrollY"/> in
    ///     the same turn would see the previous position and plan cells for the wrong region.
    /// </summary>
    /// <remarks>
    ///     <c>ChangeView</c> answers whether it took the request, and refuses it while the scroll
    ///     viewer has no template yet - which is the state on the first layout pass after a report
    ///     loads. Dropping that answer would leave the presenter believing the view had moved and
    ///     planning cells for a region nobody is looking at, so a refusal is retried once the
    ///     platform has laid the scroll viewer out.
    /// </remarks>
    void IReportViewHost.ScrollTo(double x, double y)
    {
        if (_scroll.ChangeView(x, y, null, disableAnimation: true))
            return;

        DispatcherQueue.TryEnqueue(() => _scroll.ChangeView(x, y, null, disableAnimation: true));
    }

    void IReportViewHost.SetExtent(double width, double height)
    {
        // Guarded because writing an unchanged value invalidates the layout, which raises another
        // view/size change, which lands back here - see ExtentWrite.Needed for why the guard has to
        // treat NaN specially.
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
            page = new Border
            {
                Background = new SolidColorBrush(Colors.White),
                Child = new Image { Stretch = Stretch.Fill }
            };

            ApplyPageBorder(page);

            _pageViews[index] = page;

            // Behind every cell: the pages are the backdrop the sharp cells are laid over.
            _canvas.Children.Insert(0, page);
        }

        if (page.Child is Image { Source: null } pageImage
            && _reportSession.Tiles?.BaseLayer(index) is { } baseLayer)
        {
            pageImage.Source = DecodeBaseLayer(baseLayer);
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

    /// <summary>
    ///     Runs an action on the user-interface thread.
    /// </summary>
    /// <remarks>
    ///     A refusal is deliberately not recovered from. <c>TryEnqueue</c> only answers false once the
    ///     queue is shutting down, and at that point there is no thread left to run the action on and
    ///     no view left to update - every honest response is a no-op. It is not raised through
    ///     <see cref="RenderFailed"/> either: a window closing is not a rendering failure, and
    ///     reporting one during teardown would fire the event at handlers that are themselves going
    ///     away. What the false return must not do is pass for success, hence the explicit discard.
    /// </remarks>
    void IReportViewHost.Post(Action action) => _ = DispatcherQueue.TryEnqueue(() => action());

    /// <summary>The low-resolution image behind a page, which the cache hands over already encoded.</summary>
    /// <remarks>
    ///     <c>SetSource</c> rather than <c>SetSourceAsync</c>: this runs inside a layout pass on the
    ///     user-interface thread and the bytes are already in memory, so there is nothing to await -
    ///     and awaiting would place the page one frame later than the cells laid over it.
    /// </remarks>
    private static BitmapImage DecodeBaseLayer(byte[] encoded)
    {
        var stream = new InMemoryRandomAccessStream();

        // A synchronous Stream over the buffer, rather than either of the two shapes that look more
        // natural here. A DataWriter owns the stream it wraps and closes it on dispose, and the
        // DetachStream() that would prevent that is one of the WinRT members Uno does not implement
        // (Uno0001). Blocking on the stream's own WriteAsync would be worse still: this runs on the
        // user-interface thread inside a layout pass, and on the WebAssembly host there is no second
        // thread to complete that operation - the wait is a deadlock rather than a stall, which is
        // the same hazard UnoAssetFileSystem is shaped around.
        using (var writable = stream.AsStreamForWrite())
        {
            writable.Write(encoded, 0, encoded.Length);
            writable.Flush();
        }

        stream.Seek(0);

        var bitmap = new BitmapImage();
        bitmap.SetSource(stream);

        return bitmap;
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

            // A WriteableBitmap's PixelBuffer is BGRA8 with no row padding, which is exactly what
            // TilePixels.Bgra produces - so this is one copy, where the Avalonia host has to go row
            // by row against its framebuffer's stride.
            var bitmap = new WriteableBitmap(pixelWidth, pixelHeight);

            using (var pixels = bitmap.PixelBuffer.AsStream())
                pixels.Write(bytes, 0, Math.Min(bytes.Length, pixelWidth * pixelHeight * 4));

            bitmap.Invalidate();

            Image.Source = bitmap;
        }
    }

    /// <summary>The two writes <see cref="ZoomPublisher"/> orders, as this control performs them.</summary>
    private sealed class Sink(ReportView view) : IZoomSink
    {
        public void SetZoomFactor(double zoom) => view.Zoom = zoom;

        public void SetZoomMode(ReportZoomMode mode) => view.ZoomMode = mode;
    }
}
