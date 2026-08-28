using Pysar.Elements;
using Pysar.Skia;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;

// Report elements and MAUI controls share several names; the report side is the one aliased away
// here because this file is a MAUI control first.
using Image = Microsoft.Maui.Controls.Image;
using ImageSource = Microsoft.Maui.Controls.ImageSource;

namespace Pysar.Maui;

/// <summary>
///     Shows a built report as scrollable, zoomable pages, rasterising only what is on screen so the
///     text stays sharp at any zoom without the memory a whole zoomed page would cost.
/// </summary>
/// <remarks>
///     Scrolling is a real <see cref="ScrollView"/> over an empty box the size of the document, so
///     the platform keeps its wheel, trackpad, momentum and scrollbars. The pages themselves are
///     painted by a canvas the size of the viewport, offset by the scroll position.
///
///     The arithmetic - what zoom a mode resolves to, where a page sits, which cells are worth
///     drawing - lives in <see cref="ReportViewPresenter"/>, framework-neutral and unit-tested. This
///     type is the <see cref="IReportViewHost"/> it draws through: the view tree, the bindable
///     properties, and the platform input that feeds the presenter.
/// </remarks>
public partial class ReportView : ContentView, IReportViewHost, IReportViewSurface
{
    /// <summary>
    ///     Units per report point at 100%: 96 to the inch against the point's 72, which is what a
    ///     browser's PDF viewer calls 100% - corrected for a platform that scales its whole interface.
    /// </summary>
    private static double UnitsPerPoint => ReportViewDefaults.UnitsPerPoint * InterfaceScaleCorrection;

    /// <summary>
    ///     Undoes an interface-wide scaling the platform applies after layout.
    /// </summary>
    /// <remarks>
    ///     Mac Catalyst draws an iPad interface at 77% of its laid-out size unless the app opts into
    ///     the Mac idiom - which is not open to us, because UIKit refuses controls MAUI relies on
    ///     there (a Picker is a UIPickerView, unsupported in the Mac idiom, and throws on sight).
    ///     UIKit exposes no factor for this: it lays views out in iPad points while reporting the
    ///     screen in macOS points, so a full-screen window measures wider than the screen it is on.
    ///     Without the correction a page at 100% comes out a fifth smaller than the same page in a
    ///     desktop PDF viewer.
    /// </remarks>
    private static double InterfaceScaleCorrection
    {
        get
        {
#if __MACCATALYST__
            return UIKit.UIDevice.CurrentDevice.UserInterfaceIdiom == UIKit.UIUserInterfaceIdiom.Pad
                ? 1 / 0.77
                : 1;
#else
            return 1;
#endif
        }
    }

    /// <summary>The line around a page unless the host asks for another one.</summary>
    private static readonly Color DefaultPageBorderColor = Color.FromArgb(ReportViewDefaults.PageBorderColorHex);

    // Transparent so the view's own BackgroundColor shows through: a scroll view paints the system
    // background by default, which would hide whatever the host set on the control.
    private readonly ScrollView _scroll = new()
    {
        Orientation = ScrollOrientation.Both,
        BackgroundColor = Colors.Transparent
    };
    // The pages themselves: real views inside the scroll view, so the platform moves them while
    // scrolling instead of the application repainting them. That is what a native document viewer
    // does, and no repaint can keep up with a finger otherwise.
    private readonly AbsoluteLayout _content = new()
    {
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Start
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
    ///     Orders the two writes back to <see cref="Zoom"/> and <see cref="ZoomMode"/> after a
    ///     gesture that already told the presenter what it did and where to anchor it, and flags that
    ///     the property-changed callback is seeing its own write rather than a fresh request - which
    ///     is correct for a menu or a binding but not for a pinch under the reader's fingers.
    /// </summary>
    private readonly ZoomPublisher _zoomPublisher;

    /// <summary>
    ///     The order this control follows between an input and the pixels. Shared with the other
    ///     hosts; what stays here is only what MAUI itself has to do, through
    ///     <see cref="IReportViewSurface"/>.
    /// </summary>
    private readonly ReportViewController _controller;

    /// <summary>
    ///     Nesting count while a platform path is writing the scroll position itself, so the Scrolled
    ///     handler does not treat that write as a reader scroll and re-enter the presenter mid-update.
    /// </summary>
    private int _suppressScrollReaction;

    public ReportView()
    {
        _presenter = new ReportViewPresenter(this)
        {
            // Fed in once: the correction depends on the platform and the idiom, neither of which
            // changes while the app runs.
            UnitsPerPoint = UnitsPerPoint
        };
        _pinch = new PinchSession(_presenter);
        _zoomPublisher = new ZoomPublisher(new Sink(this));

        _reportSession = new ReportViewSession(_presenter, this, new TaskRunScheduler());
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

        // A null handler is MAUI's teardown signal, but it is also what a control briefly has while
        // it moves between parents, so the session decides on the next turn of the loop.
        HandlerChanged += (_, _) =>
        {
            if (Handler is null)
                _reportSession.DisposeWhenStillDetached(() => Handler is not null);
        };

        _scroll.Content = _content;
        _scroll.Scrolled += (_, _) => OnScrolled();

        // The scroll view's own size is what every measurement here is against, and it is not always
        // known when the control's size is: on iOS the control is sized before the report is ready
        // and never resized after, which left the scrollable extent frozen at its pre-layout value.
        _scroll.SizeChanged += (_, _) => OnViewportChanged();
        SizeChanged += (_, _) => OnViewportChanged();

        AddGestures();

        Content = _scroll;
    }

    public static readonly BindableProperty ReportProperty = BindableProperty.Create(
        nameof(Report), typeof(Report), typeof(ReportView), propertyChanged: OnReportChanged);

    public static readonly BindableProperty ZoomModeProperty = BindableProperty.Create(
        nameof(ZoomMode), typeof(ReportZoomMode), typeof(ReportView), ReportZoomMode.FitWidth,
        BindingMode.TwoWay, propertyChanged: OnZoomChanged);

    public static readonly BindableProperty ZoomProperty = BindableProperty.Create(
        nameof(Zoom), typeof(double), typeof(ReportView), 1d, BindingMode.TwoWay,
        propertyChanged: OnZoomChanged);

    public static readonly BindableProperty PageSpacingProperty = BindableProperty.Create(
        nameof(PageSpacing), typeof(double), typeof(ReportView), ReportViewDefaults.PageSpacing,
        propertyChanged: OnZoomChanged);

    public static readonly BindableProperty PageBorderColorProperty = BindableProperty.Create(
        nameof(PageBorderColor), typeof(Color), typeof(ReportView), DefaultPageBorderColor,
        propertyChanged: OnPageBorderChanged);

    public static readonly BindableProperty PageBorderThicknessProperty = BindableProperty.Create(
        nameof(PageBorderThickness), typeof(double), typeof(ReportView), ReportViewDefaults.PageBorderThickness,
        propertyChanged: OnPageBorderChanged);

    public static readonly BindableProperty DocumentPaddingProperty = BindableProperty.Create(
        nameof(DocumentPadding), typeof(Thickness), typeof(ReportView), default(Thickness),
        propertyChanged: OnZoomChanged);

    public static readonly BindableProperty CurrentPageProperty = BindableProperty.Create(
        nameof(CurrentPage), typeof(int), typeof(ReportView), 1, BindingMode.TwoWay,
        propertyChanged: OnCurrentPageChanged);

    public static readonly BindableProperty PageCountProperty = BindableProperty.Create(
        nameof(PageCount), typeof(int), typeof(ReportView), 0);

    public static readonly BindableProperty EffectiveZoomProperty = BindableProperty.Create(
        nameof(EffectiveZoom), typeof(double), typeof(ReportView), 1d,
        // Source by default: the control resolves this one, a binding only observes it.
        defaultBindingMode: BindingMode.OneWayToSource);

    public static readonly BindableProperty VerticalOverdrawProperty = BindableProperty.Create(
        nameof(VerticalOverdraw), typeof(double), typeof(ReportView), ReportViewDefaults.VerticalOverdraw);

    public static readonly BindableProperty RenderBudgetProperty = BindableProperty.Create(
        nameof(RenderBudget), typeof(double), typeof(ReportView), ReportViewDefaults.RenderBudget);

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
        set => SetValue(ZoomProperty, Math.Clamp(value, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom));
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
        set => SetValue(PageBorderThicknessProperty, Math.Max(0, value));
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
        set => SetValue(VerticalOverdrawProperty, Math.Max(0, value));
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
        set => SetValue(RenderBudgetProperty, Math.Max(0, value));
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
        private set => SetValue(EffectiveZoomProperty, value);
    }

    private static void OnReportChanged(BindableObject bindable, object oldValue, object newValue)
        => _ = ((ReportView)bindable).StartSessionAsync((Report?)newValue);

    private static void OnZoomChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (ReportView)bindable;

        // A gesture has already told the presenter what it did and where to anchor it, and is only
        // writing these properties back so a binding sees them; asking the presenter again here
        // would replace that anchor with the viewport's centre.
        if (view._zoomPublisher.Publishing)
            return;

        view._presenter.PageSpacing = view.PageSpacing;
        view._presenter.Padding = view.ToPagePadding(view.DocumentPadding);

        // No gesture is driving this: a property set from a binding, or from code, holds the middle
        // of the viewport still, exactly as a resize would - except this one is deliberate, so a fit
        // mode changing to a chosen percentage still anchors around the reader's eye.
        view._presenter.SetZoom(view.ZoomMode, view.Zoom, view.CenterAnchor());
        view.AfterPresenterUpdate(immediate: false);
    }

    private static void OnPageBorderChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (ReportView)bindable;

        foreach (var page in view._pageViews.Values)
            view.ApplyPageBorder(page);

        // The line's width is part of where a page is placed, not only of how it looks.
        view._presenter.PageBorderThickness = view.PageBorderThickness;
        view._presenter.ViewportChanged();
        view.AfterPresenterUpdate(immediate: false);
    }

    private static void OnCurrentPageChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (ReportView)bindable;

        // Reporting where the scroll landed must not be mistaken for a request to go there - see
        // IReportViewSurface.ReportState.
        if (view._reportingCurrentPage)
            return;

        view._presenter.GoToPage((int)newValue);
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
    ///     A pinch is shown by scaling the content, and the pages stay where the zoom the gesture
    ///     started at put them. Reacting to a scroll while that is on screen would relay them out
    ///     mid-gesture, which is the one thing the scaling exists to avoid. The counter covers the
    ///     scrolls this control drives itself, which are not the reader moving the view.
    /// </summary>
    bool IReportViewSurface.SuppressesViewportReaction => _pinch.Running || _suppressScrollReaction > 0;

    (double VerticalOverdraw, double RenderBudget) IReportViewSurface.TilePolicy
        => (VerticalOverdraw, RenderBudget);

    /// <summary>
    ///     Nothing to do: the pages and cells are real views, and MAUI repaints them itself when they
    ///     move or their source changes.
    /// </summary>
    void IReportViewSurface.InvalidateSurface() { }

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
    private void AfterPresenterUpdate(bool immediate) => _controller.AfterPresenterUpdate(immediate);

    /// <summary>The scroll view's own width, which every measurement here is against; never zero.</summary>
    private double ViewportWidth => Math.Max(1, _scroll.Width);

    /// <summary>The scroll view's own height, which every measurement here is against; never zero.</summary>
    private double ViewportHeight => Math.Max(1, _scroll.Height);

    /// <summary>Device pixels per device independent unit, which cells are rendered against.</summary>
    private static double Density => DeviceDisplay.Current.MainDisplayInfo.Density;

    /// <summary>The centre of the viewport, in the units <see cref="ReportViewPresenter.SetZoom"/> anchors by.</summary>
    private ViewPoint CenterAnchor() => new(ViewportWidth / 2, ViewportHeight / 2);

    private PagePadding ToPagePadding(Thickness padding)
        => new(padding.Left, padding.Top, padding.Right, padding.Bottom);

    private void RequestTiles() => _controller.RequestTiles();

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
        page.Stroke = new SolidColorBrush(PageBorderColor ?? DefaultPageBorderColor);
        page.StrokeThickness = PageBorderThickness;
        page.Padding = new Thickness(PageBorderThickness);
    }

    private void ClearVisuals()
    {
        _content.Children.Clear();
        _pageViews.Clear();
        _tileViews.Clear();
    }

    // -- IReportViewHost -----------------------------------------------------------------------

    double IReportViewHost.ViewportWidth => ViewportWidth;

    double IReportViewHost.ViewportHeight => ViewportHeight;

    double IReportViewHost.ScrollX => _scroll.ScrollX;

    double IReportViewHost.ScrollY => _scroll.ScrollY;

    double IReportViewHost.Density => Density;

    void IReportViewHost.ScrollTo(double x, double y)
    {
        // Apple applies the offset on the UIScrollView in the same turn as SetExtent; everywhere
        // else ScrollToAsync still lands a frame later and _pendingScroll covers the arithmetic.
        var handled = false;
        ScrollToNative(x, y, ref handled);

        if (!handled)
            _ = _scroll.ScrollToAsync(x, y, animated: false);
    }

    void IReportViewHost.SetExtent(double width, double height)
    {
        // Assigning a size request invalidates the layout, which raises SizeChanged, which lands
        // back here: writing an unchanged value would spin that loop forever and no pass would ever
        // finish - the window stays blank, with no exception to show for it.
        if (Math.Abs(_content.WidthRequest - width) >= 0.5)
            _content.WidthRequest = width;

        if (Math.Abs(_content.HeightRequest - height) >= 0.5)
            _content.HeightRequest = height;

        // The presenter scrolls in the same turn: without the platform content size matching the
        // new extent that scroll is clamped against the document height before this update.
        var handled = false;
        SetExtentNative(width, height, ref handled);

        if (!handled)
        {
            _content.InvalidateMeasure();
            _scroll.InvalidateMeasure();
        }
    }

    /// <summary>Platform path that makes <paramref name="width"/>×<paramref name="height"/> scrollable now.</summary>
    partial void SetExtentNative(double width, double height, ref bool handled);

    /// <summary>Platform path that applies a scroll offset in the same turn as <see cref="SetExtentNative"/>.</summary>
    partial void ScrollToNative(double x, double y, ref bool handled);

    void IReportViewHost.PlacePage(int index, ViewRect bounds)
    {
        if (!_pageViews.TryGetValue(index, out var page))
        {
            page = new Border
            {
                BackgroundColor = Colors.White,
                Content = new Image { Aspect = Aspect.Fill }
            };

            ApplyPageBorder(page);

            _pageViews[index] = page;

            // Behind every cell: the pages are the backdrop the sharp cells are laid over.
            _content.Children.Insert(0, page);
        }

        if (page.Content is Image image && image.Source is null && _reportSession.Tiles?.BaseLayer(index) is { } baseLayer)
            image.Source = ImageSource.FromStream(() => new MemoryStream(baseLayer));

        AbsoluteLayout.SetLayoutBounds(page, ToRect(bounds));
    }

    void IReportViewHost.PlaceTile(ViewRect bounds, Tile tile)
    {
        if (!_tileViews.TryGetValue(tile.Key, out var placed))
        {
            placed = new TileView(new Image { Aspect = Aspect.Fill });

            _tileViews[tile.Key] = placed;
            _content.Children.Add(placed.Image);
        }

        placed.Show(tile.Bytes);

        AbsoluteLayout.SetLayoutBounds(placed.Image, ToRect(bounds));
    }

    void IReportViewHost.RemoveTile(TileKey key)
    {
        if (!_tileViews.TryGetValue(key, out var placed))
            return;

        _content.Children.Remove(placed.Image);
        _tileViews.Remove(key);
    }

    /// <summary>A cell on screen, and the bytes the pixels it shows were decoded from.</summary>
    /// <remarks>
    ///     A key names its pixels for as long as one report is loaded - a cell's region and content
    ///     follow from its page, column, row and scale, and another report clears these views - so the
    ///     bytes never change under a key today. Kept anyway, and compared, so that a change to what a
    ///     key means cannot quietly leave a cell showing the pixels it was created with. Compared by
    ///     reference rather than decoded again: this runs for every visible cell on every refresh, and
    ///     decoding here would put a scroll's worth of PNG through the decoder every frame.
    /// </remarks>
    private sealed class TileView(Image image)
    {
        private byte[]? _bytes;

        public Image Image { get; } = image;

        public void Show(byte[] bytes)
        {
            if (ReferenceEquals(_bytes, bytes))
                return;

            _bytes = bytes;

            // Into a local, deliberately: the lambda outlives this call, and capturing the field would
            // have MAUI read whatever it holds whenever it decides to decode.
            var source = bytes;

            Image.Source = ImageSource.FromStream(() => new MemoryStream(source));
        }
    }

    void IReportViewHost.Post(Action action) => MainThread.BeginInvokeOnMainThread(action);

    private static Rect ToRect(ViewRect bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    /// <summary>The two writes <see cref="ZoomPublisher"/> orders, as this control performs them.</summary>
    private sealed class Sink(ReportView view) : IZoomSink
    {
        public void SetZoomFactor(double zoom) => view.Zoom = zoom;

        public void SetZoomMode(ReportZoomMode mode) => view.ZoomMode = mode;
    }
}

/// <summary>
///     The renderer the control measures reports with. A single instance carries the drawers the
///     application registered through <c>UsePysar</c>.
/// </summary>
internal static class ReportViewRenderer
{
    private static SkiaReportRenderer? _instance;

    /// <summary>
    ///     The renderer installed by <c>UsePysar</c>. Throws rather than falling back to an
    ///     unconfigured renderer: without the fonts and asset access <c>UsePysar</c> installs, a
    ///     report renders with substitute fonts and blank images instead of reporting the mistake.
    /// </summary>
    public static SkiaReportRenderer Instance
    {
        get => _instance ?? throw new InvalidOperationException(
            "Call UsePysar during application startup before using a report view or the renderer.");
        set => _instance = value;
    }
}
