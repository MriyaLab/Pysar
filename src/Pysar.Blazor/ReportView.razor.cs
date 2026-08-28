using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Pysar.Elements;
using Pysar.Skia;
using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;
using Pysar.Viewer.Zoom;

namespace Pysar.Blazor;

public sealed partial class ReportView : IReportViewHost, IAsyncDisposable
{
    /// <summary>The report to show. It must already have been built.</summary>
    [Parameter] public Report? Report { get; set; }

    [Parameter] public ReportZoomMode ZoomMode { get; set; } = ReportZoomMode.FitWidth;

    [Parameter] public EventCallback<ReportZoomMode> ZoomModeChanged { get; set; }

    [Parameter] public double Zoom { get; set; } = 1;

    [Parameter] public EventCallback<double> ZoomChanged { get; set; }

    [Parameter] public int CurrentPage { get; set; } = 1;

    [Parameter] public EventCallback<int> CurrentPageChanged { get; set; }

    [Parameter] public EventCallback<int> PageCountChanged { get; set; }

    [Parameter] public EventCallback<double> EffectiveZoomChanged { get; set; }

    [Parameter] public double PageSpacing { get; set; } = ReportViewDefaults.PageSpacing;

    /// <summary>A CSS colour, where the other controls take a platform one.</summary>
    [Parameter] public string PageBorderColor { get; set; } = ReportViewDefaults.PageBorderColorHex;

    [Parameter] public double PageBorderThickness { get; set; } = ReportViewDefaults.PageBorderThickness;

    [Parameter] public PagePadding DocumentPadding { get; set; }

    [Parameter] public double VerticalOverdraw { get; set; } = ReportViewDefaults.VerticalOverdraw;

    [Parameter] public double RenderBudget { get; set; } = ReportViewDefaults.RenderBudget;

    /// <summary>
    /// When &gt; 0, request a lower-scale coverage pass before full-DPI tiles (WASM progressive sharp).
    /// 0 disables. Default 0.5 on Blazor.
    /// </summary>
    [Parameter]
    public double CoverageFactor { get; set; } = 0.5;

    [Parameter] public EventCallback<Exception> RenderFailed { get; set; }

    /// <summary>
    ///     How long a freshly drawn cell takes to replace what is under it, in milliseconds. 0 shows
    ///     it at once. Short by design: it is there to spread the change in sharpness over a few
    ///     frames, not to animate anything.
    /// </summary>
    [Parameter] public double TileFade { get; set; } = 120;

    private static string Px(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture) + "px";

    private static string Ms(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture) + "ms";

    private readonly Dictionary<int, ViewRect> _pages = [];
    private readonly Dictionary<TileKey, (ViewRect Bounds, Tile Tile)> _tiles = [];

    /// <summary>Data-URL cache for whole-page PNG backdrops already seen this session.</summary>
    private readonly Dictionary<int, string> _baseLayerUrls = [];

    /// <summary>Cells whose canvas exists in the DOM but has not been painted yet.</summary>
    private readonly List<TileKey> _unpainted = [];

    private readonly string _instance = Guid.NewGuid().ToString("N");

    private ReportViewPresenter _presenter = null!;
    private ReportViewTiles? _tileCache;
    private CancellationTokenSource? _session;

    /// <summary>
    ///     Used when the host registered no renderer of its own. Held rather than made per load so
    ///     that the drawers and caches a renderer carries survive a change of report.
    /// </summary>
    private SkiaReportRenderer? _ownRenderer;

    private ElementReference _scroller;
    private IJSObjectReference? _module;
    private IJSInProcessObjectReference? _moduleInProcess;
    private DotNetObjectReference<ReportView>? _self;

    private double _extentWidth;
    private double _extentHeight;
    private float _lastRenderScale = 1f;

    // The browser answers about itself asynchronously, but IReportViewHost is read synchronously, so
    // these are the component's own copy. ScrollTo writes them at once and the scroll event only
    // confirms; otherwise the tile planner would work from where the view was one round trip ago.
    private double _viewportWidth;
    private double _viewportHeight;
    private double _scrollX;
    private double _scrollY;
    private double _density = 1;

    // What the presenter has already been told, so a parameter set to the value it is already at is
    // not acted on again - which is what keeps a value this component published from coming back as
    // a request to do it over.
    private Report? _appliedReport;
    private double _appliedZoom = 1;
    private ReportZoomMode _appliedZoomMode = ReportZoomMode.FitWidth;
    private double _appliedPageSpacing = 24;
    private PagePadding _appliedPadding;
    private double _appliedBorderThickness = 1;
    private int _appliedCurrentPage = 1;

    /// <summary>Set once the module has attached and the scroller has been measured.</summary>
    private bool _attached;

    /// <summary>
    ///     Orders the two writes back to <see cref="ZoomChanged"/> and <see cref="ZoomModeChanged"/>
    ///     after a gesture publishes the zoom it settled on, and flags that the parameters coming
    ///     back are the component's own write rather than a request to zoom again.
    /// </summary>
    /// <remarks>
    ///     The zoom and the mode are two callbacks, and a parent that handles the first one re-renders
    ///     inside the call: the component is then handed the new zoom together with the mode it had
    ///     before, a pair the parent never held. Applying that pair puts a gesture begun in a fit mode
    ///     straight back into it, which is the whole of the gesture undone. The comparisons above
    ///     cover the settled pair that follows; this covers the half of it in between.
    /// </remarks>
    private ZoomPublisher _zoomPublisher = null!;

    /// <summary>The running pinch, shown through the document's CSS transform.</summary>
    private PinchSession _pinch = null!;

    /// <summary>
    ///     Drop the CSS preview transform after the next render so the relayout lands under the
    ///     covering scale, the way Avalonia drops <c>RenderTransform</c> after <c>ApplyGestureZoom</c>.
    /// </summary>
    private bool _clearPreviewAfterRender;

#if DEBUG
    private long _perfCommitTimestamp;
    private bool _perfFirstCentreLogged;
    private bool _perfUsableLogged;
    private bool _perfFullLogged;
    private float _perfTargetScale;
#endif

    private string CanvasId(TileKey key)
        => $"qr-{_instance}-{key.PageIndex}-{key.Column}-{key.Row}-{key.Scale:0.###}";

    private int TileZIndex(TileKey key)
    {
        var target = _lastRenderScale;
        var targetTol = Math.Abs(target) * 1e-4f;
        if (Math.Abs(key.Scale - target) <= targetTol)
            return 30;

        // Coverage under bridge: a half-res coverage pass on top of stretched previous-scale
        // tiles made text look wider and blurrier for a beat before full-DPI arrived. Bridge is
        // the pinch-preview continuity; coverage only fills holes where no bridge exists.
        if (CoverageFactor > 0)
        {
            var coverageScale = target * (float)CoverageFactor;
            var coverageTol = Math.Abs(coverageScale) * 1e-4f;
            if (Math.Abs(key.Scale - coverageScale) <= coverageTol)
                return 10;
        }

        return 20;
    }

    /// <summary>The soft whole-page PNG for a page, or null until it has been drawn.</summary>
    private string? BaseLayerUrl(int pageIndex)
    {
        if (_baseLayerUrls.TryGetValue(pageIndex, out var url))
            return url;

        if (_tileCache?.BaseLayer(pageIndex) is not { } png)
            return null;

        url = "data:image/png;base64," + Convert.ToBase64String(png);
        _baseLayerUrls[pageIndex] = url;
        return url;
    }

    double IReportViewHost.ViewportWidth => _viewportWidth;

    double IReportViewHost.ViewportHeight => _viewportHeight;

    double IReportViewHost.ScrollX => _scrollX;

    double IReportViewHost.ScrollY => _scrollY;

    double IReportViewHost.Density => _density;

    void IReportViewHost.ScrollTo(double x, double y)
    {
        // Optimistic: the presenter reads ScrollX back immediately after asking for this.
        _scrollX = x;
        _scrollY = y;

        _ = _module?.InvokeVoidAsync("scrollTo", _scroller, x, y);
    }

    void IReportViewHost.SetExtent(double width, double height)
    {
        // The guard is not optional. In MAUI, writing an unchanged extent re-entered layout forever.
        // In Avalonia the same guard silently never fired, because an unset size there is NaN and
        // every comparison against it is false. Comparing with a tolerance on plain doubles avoids
        // both: there is no NaN here, and a redundant write is dropped.
        if (Math.Abs(_extentWidth - width) < 0.01 && Math.Abs(_extentHeight - height) < 0.01)
            return;

        _extentWidth = width;
        _extentHeight = height;
    }

    void IReportViewHost.PlacePage(int index, ViewRect bounds) => _pages[index] = bounds;

    void IReportViewHost.PlaceTile(ViewRect bounds, Tile tile)
    {
        var known = _tiles.TryGetValue(tile.Key, out var placed);

        _tiles[tile.Key] = (bounds, tile);

        // Only a cell whose pixels are not on the canvas already; one that merely moved is repositioned
        // by the style, and repainting it would copy megabytes into JavaScript for nothing. Compared by
        // reference rather than by key alone: a key names its pixels for as long as one report is
        // loaded, and comparing what it actually carries keeps that an observation rather than a thing
        // this method has to be right about.
        if (!known || !ReferenceEquals(placed.Tile.Bytes, tile.Bytes))
            _unpainted.Add(tile.Key);
    }

    void IReportViewHost.RemoveTile(TileKey key)
    {
        _tiles.Remove(key);
        _unpainted.Remove(key);
    }

    void IReportViewHost.Post(Action action) => _ = InvokeAsync(action);

    protected override void OnInitialized()
    {
        _presenter = new ReportViewPresenter(this);
        _presenter.StateChanged += OnPresenterStateChanged;
        _pinch = new PinchSession(_presenter);
        _zoomPublisher = new ZoomPublisher(new Sink(this));
    }

    protected override async Task OnParametersSetAsync()
    {
        // Nothing is laid out against these, so they are simply kept current.
        _presenter.CoverageFactor = CoverageFactor;
        _presenter.VerticalOverdraw = VerticalOverdraw;
        _presenter.RenderBudget = RenderBudget;

        if (!ReferenceEquals(_appliedReport, Report))
        {
            _appliedReport = Report;

            // Before the first render there is no viewport to lay a report out against, so the first
            // one waits for the module to attach and measure the scroller.
            if (_attached)
                await LoadReportAsync();
        }

        if (!_zoomPublisher.Publishing
            && (_appliedZoom != Zoom || _appliedZoomMode != ZoomMode
                || _appliedPageSpacing != PageSpacing || _appliedPadding != DocumentPadding))
        {
            _appliedZoom = Zoom;
            _appliedZoomMode = ZoomMode;
            _appliedPageSpacing = PageSpacing;
            _appliedPadding = DocumentPadding;

            _presenter.PageSpacing = PageSpacing;
            _presenter.Padding = DocumentPadding;

            // The middle of the viewport is held, exactly as a resize would hold it: this is a zoom
            // asked for in code or by the toolbar rather than by a gesture with an anchor of its own.
            _presenter.SetZoom(ZoomMode, Zoom, CenterAnchor());

            AfterPresenterUpdate();
        }

        if (_appliedBorderThickness != PageBorderThickness)
        {
            _appliedBorderThickness = PageBorderThickness;

            // The line's width is part of where a page is placed, not only of how it looks.
            _presenter.PageBorderThickness = PageBorderThickness;
            _presenter.ViewportChanged();

            AfterPresenterUpdate();
        }

        if (_appliedCurrentPage != CurrentPage)
        {
            _appliedCurrentPage = CurrentPage;

            _presenter.GoToPage(CurrentPage);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Pysar.Blazor/reportView.js");
            _moduleInProcess = _module as IJSInProcessObjectReference;

            _self = DotNetObjectReference.Create(this);

            await _module.InvokeVoidAsync("attach", _scroller, _self);

            _attached = true;
            _appliedReport = Report;

            await LoadReportAsync();
        }

        // After the render, not during it: a canvas cannot be painted before Blazor has put it in
        // the DOM, and PlaceTile runs while the render tree is still being built.
        //
        // Pinch commit must clear the preview WITHOUT awaiting first. Any await here returns to the
        // browser with the new layout still under the old CSS matrix → text jumps larger/smaller for
        // a frame. WASM exposes IJSInProcessObjectReference so the whole paint+clear stays on stack.
        var clearPreview = _clearPreviewAfterRender;
        if (clearPreview)
            _clearPreviewAfterRender = false;

        if (clearPreview)
        {
            var pending = _unpainted.ToArray();
            _unpainted.Clear();
            PaintAllThenClearPreview(pending);
            return;
        }

        await PaintAsync();

        // After the paint, because what it is checking is where the painted cells landed: the page
        // around the viewer settles over several renders, and a document that moved by half a
        // device pixel resamples every cell on it.
        if (_module is not null)
            await _module.InvokeVoidAsync("realign", _scroller);
    }

    private async Task PaintAsync()
    {
        if (_module is null || _unpainted.Count == 0)
            return;

        var pending = _unpainted.ToArray();
        _unpainted.Clear();

        const int maxBytes = 4 * 1024 * 1024;
        const int maxTiles = 4;

        var ids = new List<string>(maxTiles);
        var widths = new List<int>(maxTiles);
        var heights = new List<int>(maxTiles);
        var pixelChunks = new List<byte[]>(maxTiles);
        var batchBytes = 0;

        async Task FlushAsync()
        {
            if (ids.Count == 0)
                return;

            if (ids.Count == 1)
            {
                await _module.InvokeVoidAsync(
                    "paint", ids[0], widths[0], heights[0], pixelChunks[0]);
            }
            else
            {
                PackPixels(pixelChunks, batchBytes, out var offsets, out var lengths, out var combined);
                await _module.InvokeVoidAsync(
                    "paintBatch", ids, widths, heights, offsets, lengths, combined);
            }

            ids.Clear();
            widths.Clear();
            heights.Clear();
            pixelChunks.Clear();
            batchBytes = 0;
        }

        foreach (var key in pending)
        {
            if (!_tiles.TryGetValue(key, out var placed))
                continue;

            var bytes = placed.Tile.Bytes.Length;
            if (ids.Count > 0 && (ids.Count >= maxTiles || batchBytes + bytes > maxBytes))
                await FlushAsync();

            ids.Add(CanvasId(key));
            widths.Add(placed.Tile.PixelWidth);
            heights.Add(placed.Tile.PixelHeight);
            pixelChunks.Add(placed.Tile.Bytes);
            batchBytes += bytes;
        }

        await FlushAsync();
    }

    /// <summary>
    ///     Paints every pending cell and drops the pinch CSS in one turn. Prefers in-process interop
    ///     so the browser cannot composite the post-commit layout still multiplied by the preview.
    /// </summary>
    private void PaintAllThenClearPreview(TileKey[] pending)
    {
        if (_module is null)
            return;

        var ids = new List<string>(pending.Length);
        var widths = new List<int>(pending.Length);
        var heights = new List<int>(pending.Length);
        var pixelChunks = new List<byte[]>(pending.Length);
        var batchBytes = 0;

        foreach (var key in pending)
        {
            if (!_tiles.TryGetValue(key, out var placed))
                continue;

            ids.Add(CanvasId(key));
            widths.Add(placed.Tile.PixelWidth);
            heights.Add(placed.Tile.PixelHeight);
            pixelChunks.Add(placed.Tile.Bytes);
            batchBytes += placed.Tile.Bytes.Length;
        }

        if (ids.Count == 0)
        {
            if (_moduleInProcess is not null)
                _moduleInProcess.InvokeVoid("clearPreviewTransform", _scroller);
            else
                _ = _module.InvokeVoidAsync("clearPreviewTransform", _scroller);

            return;
        }

        PackPixels(pixelChunks, batchBytes, out var offsets, out var lengths, out var combined);

        if (_moduleInProcess is not null)
        {
            _moduleInProcess.InvokeVoid(
                "paintManyThenClearPreview", _scroller, ids, widths, heights, offsets, lengths, combined);
            return;
        }

        // Server-side Blazor fallback: cannot block the paint frame the same way.
        _ = _module.InvokeVoidAsync(
            "paintManyThenClearPreview", _scroller, ids, widths, heights, offsets, lengths, combined);
    }

    private static void PackPixels(
        List<byte[]> pixelChunks, int batchBytes,
        out int[] offsets, out int[] lengths, out byte[] combined)
    {
        offsets = new int[pixelChunks.Count];
        lengths = new int[pixelChunks.Count];
        combined = new byte[batchBytes];
        var offset = 0;

        for (var i = 0; i < pixelChunks.Count; i++)
        {
            var chunk = pixelChunks[i];
            offsets[i] = offset;
            lengths[i] = chunk.Length;
            Buffer.BlockCopy(chunk, 0, combined, offset, chunk.Length);
            offset += chunk.Length;
        }
    }

    /// <summary>
    ///     The renderer the view draws through: the one the host registered with
    ///     <c>AddPysar</c>, so that custom drawers registered there apply on screen as well as in
    ///     an export. A host that registered none gets a private renderer with the built-in drawers,
    ///     which is all a report of stock elements needs.
    /// </summary>
    private SkiaReportRenderer Renderer
        => Services.GetService(typeof(SkiaReportRenderer)) as SkiaReportRenderer
            ?? (_ownRenderer ??= new SkiaReportRenderer());

    private async Task LoadReportAsync()
    {
        _session?.Cancel();
        _session?.Dispose();
        _session = null;

        _tileCache?.Dispose();
        _tileCache = null;

        _pages.Clear();
        _tiles.Clear();
        _unpainted.Clear();
        _baseLayerUrls.Clear();

        _presenter.SetTiles(null);

        // A report starts at its beginning. Left where the previous one was read to, the position
        // would be measured against the new document as soon as its pages are placed, and the page
        // it lands on reported as the current one - which the toolbar has just set to the first.
        ((IReportViewHost)this).ScrollTo(0, 0);

        if (Report is null)
            return;

        var cts = new CancellationTokenSource();
        _session = cts;

        try
        {
            var session = await Renderer.CreateSessionAsync(Report);

            if (cts.IsCancellationRequested)
                return;

            var tiles = new ReportViewTiles(
                session, new YieldingRenderScheduler(), TilePixels.Rgba, maxDegreeOfParallelism: 1);

            tiles.Invalidated += () => _ = InvokeAsync(OnTilesInvalidated);
            tiles.Failed += exception => _ = InvokeAsync(() => RenderFailed.InvokeAsync(exception));

            _tileCache = tiles;

            _presenter.SetTiles(tiles);
            _presenter.ViewportChanged();

            _ = tiles.LoadBaseLayersAsync(cts.Token);

            RequestTiles();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer report.
        }
        catch (Exception exception)
        {
            await RenderFailed.InvokeAsync(exception);
        }
    }

    private void OnTilesInvalidated()
    {
        RefreshVisuals();

#if DEBUG
        LogZoomPerfIfNeeded();
#endif

        StateHasChanged();
    }

    /// <summary>
    ///     Repositions pages' tiles for the current layout. Must run after every zoom/layout change
    ///     before the next paint: otherwise bridge cells stay at the previous zoom's bounds and
    ///     dropping the pinch CSS transform flashes the pre-gesture page.
    /// </summary>
    private void RefreshVisuals(bool repaintAll = false)
    {
        if (_tileCache is null)
            return;

        _presenter.PlaceTiles(_tiles.Keys.ToList());
        _lastRenderScale = _presenter.ViewportRenderScaleForPerf();

        // Blazor may recreate canvas nodes on re-render; PlaceTile skips paint when bytes are
        // unchanged, leaving blank cells under the cleared preview. Re-queue every placed tile.
        if (repaintAll)
        {
            foreach (var key in _tiles.Keys)
                _unpainted.Add(key);
        }
    }

    /// <summary>
    ///     Same split Avalonia uses after <c>SetZoom</c>: place what is already drawn under the new
    ///     layout, then ask for missing cells. Skipping the place step is what made pinch-release flash.
    /// </summary>
    private void AfterPresenterUpdate(bool repaintAll = false)
    {
        RefreshVisuals(repaintAll);
        RequestTiles();
    }

#if DEBUG
    private void BeginZoomPerf()
    {
        _perfCommitTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        _perfFirstCentreLogged = _perfUsableLogged = _perfFullLogged = false;
        _perfTargetScale = _presenter.ViewportRenderScaleForPerf();
        Console.WriteLine("[Pysar.Perf] t_commit=0");
    }

    private void LogZoomPerfIfNeeded()
    {
        if (_perfCommitTimestamp == 0)
            return;

        var target = _perfTargetScale;
        var tol = Math.Abs(target) * 1e-4f;
        double vw = ((IReportViewHost)this).ViewportWidth;
        double vh = ((IReportViewHost)this).ViewportHeight;
        double scrollX = ((IReportViewHost)this).ScrollX;
        double scrollY = ((IReportViewHost)this).ScrollY;
        var cx = scrollX + vw / 2;
        var cy = scrollY + vh / 2;

        static double Ms(long start) =>
            (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;

        static bool Contains(ViewRect r, double x, double y) =>
            x >= r.X && y >= r.Y && x < r.X + r.Width && y < r.Y + r.Height;

        bool SampleCovered(Func<TileKey, bool> scaleOk)
        {
            const int n = 3;
            for (var iy = 0; iy < n; iy++)
            for (var ix = 0; ix < n; ix++)
            {
                var x = scrollX + (ix + 0.5) * vw / n;
                var y = scrollY + (iy + 0.5) * vh / n;
                var hit = false;
                foreach (var (key, placed) in _tiles)
                {
                    if (!scaleOk(key) || !Contains(placed.Bounds, x, y))
                        continue;
                    hit = true;
                    break;
                }

                if (!hit)
                    return false;
            }

            return true;
        }

        if (!_perfFirstCentreLogged
            && _tiles.Any(p =>
                Math.Abs(p.Key.Scale - target) <= tol && Contains(p.Value.Bounds, cx, cy)))
        {
            _perfFirstCentreLogged = true;
            Console.WriteLine($"[Pysar.Perf] t_first_centre={Ms(_perfCommitTimestamp):F1}ms");
        }

        if (!_perfUsableLogged && SampleCovered(_ => true))
        {
            _perfUsableLogged = true;
            Console.WriteLine($"[Pysar.Perf] t_viewport_usable={Ms(_perfCommitTimestamp):F1}ms");
        }

        if (!_perfFullLogged && SampleCovered(k => Math.Abs(k.Scale - target) <= tol))
        {
            _perfFullLogged = true;
            Console.WriteLine($"[Pysar.Perf] t_viewport_full_dpi={Ms(_perfCommitTimestamp):F1}ms");
        }
    }
#endif

    /// <summary>The point a zoom nobody gestured for is held around.</summary>
    private ViewPoint CenterAnchor() => new(_viewportWidth / 2, _viewportHeight / 2);

    private void RequestTiles()
    {
        if (_tileCache is null)
            return;

        _lastRenderScale = _presenter.ViewportRenderScaleForPerf();

        if (_presenter.PlanTiles() is { } plan)
            _tileCache.RequestTiles(plan.Requests);
    }

    private void OnPresenterStateChanged()
    {
        if (CurrentPage != _presenter.CurrentPage)
        {
            // Where the scroll landed, reported so a binding sees it. Recorded as applied first:
            // coming back as a parameter, it must not be mistaken for a request to go there.
            _appliedCurrentPage = _presenter.CurrentPage;

            _ = CurrentPageChanged.InvokeAsync(_presenter.CurrentPage);
        }

        _ = PageCountChanged.InvokeAsync(_presenter.PageCount);
        _ = EffectiveZoomChanged.InvokeAsync(_presenter.EffectiveZoom);
    }

    public async ValueTask DisposeAsync()
    {
        _session?.Cancel();
        _session?.Dispose();
        _session = null;

        _tileCache?.Dispose();
        _self?.Dispose();

        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("detach", _scroller);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The page went away before the component did. Nothing to clean up on that side.
            }
        }

        _module = null;
        _moduleInProcess = null;
    }

    /// <summary>The scroller changed size.</summary>
    [JSInvokable]
    public void OnViewportChanged(double width, double height, double density)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        _density = density;

        _presenter.ViewportChanged();

        AfterPresenterUpdate();
        StateHasChanged();
    }

    /// <summary>The reader scrolled. This confirms the cached position rather than setting it.</summary>
    [JSInvokable]
    public void OnScrolled(double x, double y)
    {
        _scrollX = x;
        _scrollY = y;

        // A pinch is shown through the document's CSS transform, and the pages stay where the zoom
        // the gesture started at put them. Reacting to a scroll while that is on screen would relay
        // them out mid-gesture, which is the one thing the transform exists to avoid.
        if (_pinch.Running)
            return;

        _presenter.Scrolled();

        // Scroll does not move tile bounds in document space; only the plan may change.
        RequestTiles();
        StateHasChanged();
    }

    /// <summary>
    ///     Magnifies around the clicked point, and puts the reader back where they were when clicked
    ///     again - so 100% goes to 200% and back to 100%, and a fit mode returns to that fit mode.
    ///     Shares <see cref="GestureModel.DoubleTap"/> with the other hosts rather than stepping by a
    ///     fixed factor, which would keep magnifying on every click instead of toggling back.
    /// </summary>
    [JSInvokable]
    public void OnDoubleTap(double anchorX, double anchorY)
    {
        var before = _presenter.EffectiveZoom;

        _presenter.Gestures.DoubleTap();

        ApplyGestureZoom(before, new ViewPoint(anchorX, anchorY));
    }

    /// <summary>Starts a touch pinch anchored at a point of the viewport.</summary>
    [JSInvokable]
    public void OnPinchBegin(double anchorX, double anchorY)
    {
        BeginPinch(new ViewPoint(anchorX, anchorY));
    }

    /// <summary>
    ///     Shows one frame of a running pinch by scaling what is already drawn. <paramref name="factor"/>
    ///     is the scale against the zoom the gesture began at.
    /// </summary>
    [JSInvokable]
    public void OnPinchMove(double factor)
    {
        ShowPinch(factor);
    }

    /// <summary>Ends a touch pinch: drops the transform and pays for the zoom once.</summary>
    [JSInvokable]
    public void OnPinchEnd()
    {
        CommitPinch();
    }

    /// <summary>Starts a gesture anchored at <paramref name="anchor"/>, a point of the viewport.</summary>
    private void BeginPinch(ViewPoint anchor)
    {
        _pinch.Begin(anchor);
        _clearPreviewAfterRender = false;
    }

    /// <summary>
    ///     Shows a frame of the gesture by scaling what is already drawn. <paramref name="factor"/>
    ///     is the scale against the zoom the gesture began at, which is what the JS side accumulates.
    /// </summary>
    private void ShowPinch(double factor)
    {
        if (_pinch.MoveByScale(factor) is not { } preview)
            return;

        // Direct CSS, not a Blazor re-render: a frame of a pinch must change one property of one node.
        _ = _module?.InvokeVoidAsync(
            "setPreviewTransform", _scroller, preview.Scale, preview.OffsetX, preview.OffsetY);
    }

    /// <summary>
    ///     Ends the gesture: puts the zoom it reached through the same path a wheel notch takes, so
    ///     the relayout and the sharp cells are paid for once, and drops the CSS after the render.
    /// </summary>
    private void CommitPinch()
    {
        if (_pinch.End() is not { } commit)
        {
            // Already false from BeginPinch, since a commit cannot happen without one; written
            // again so the pair with the clear below reads as one decision rather than a
            // dependency on what some earlier method left behind.
            _clearPreviewAfterRender = false;
            _ = _module?.InvokeVoidAsync("clearPreviewTransform", _scroller);
            return;
        }

        var before = _presenter.EffectiveZoom;

        // BeginPinch first: the commit's factor is measured against the zoom the gesture started
        // at, and PinchByScale is the entry point that reads it that way.
        _presenter.Gestures.BeginPinch();
        _presenter.Gestures.PinchByScale(commit.Factor);

        // Relayout first while the preview still covers the document. ApplyGestureZoom already
        // calls StateHasChanged; a second call here forced an extra Blazor render on every commit.
        ApplyGestureZoom(before, commit.Anchor, commit.Held);

        _clearPreviewAfterRender = true;
    }

    /// <summary>
    ///     Publishes the zoom an input handler just applied, if it moved far enough from the one
    ///     before it, and refreshes anchored at that handler's point so what is under the pointer or
    ///     the fingers stays there.
    /// </summary>
    private void ApplyGestureZoom(double before, ViewPoint viewportPoint, DocumentPoint? held = null)
    {
        var zoom = _presenter.EffectiveZoom;

        if (Math.Abs(zoom - before) < before * ReportViewDefaults.ZoomStepThreshold)
            return;

        // The gesture has already anchored the zoom where the reader put it; the sink records it as
        // applied as it publishes, which keeps the parameters it is about to raise from re-anchoring
        // it on the middle of the view.
        _zoomPublisher.Publish(_presenter.ZoomMode, _presenter.Zoom);

        _presenter.SetZoom(_appliedZoomMode, _appliedZoom, viewportPoint, held);
#if DEBUG
        BeginZoomPerf();
#endif
        // Place bridge tiles at the new geometry and re-paint them before clearPreviewTransform.
        AfterPresenterUpdate(repaintAll: true);
        StateHasChanged();
    }

    /// <summary>The two writes <see cref="ZoomPublisher"/> orders, as this component performs them.</summary>
    private sealed class Sink(ReportView view) : IZoomSink
    {
        public void SetZoomFactor(double zoom)
        {
            view._appliedZoom = zoom;
            _ = view.ZoomChanged.InvokeAsync(zoom);
        }

        public void SetZoomMode(ReportZoomMode mode)
        {
            view._appliedZoomMode = mode;
            _ = view.ZoomModeChanged.InvokeAsync(mode);
        }
    }
}
