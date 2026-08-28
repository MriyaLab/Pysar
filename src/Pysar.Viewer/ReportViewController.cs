using Pysar.Viewer.Tiles;

namespace Pysar.Viewer;

/// <summary>
///     The sequence a report view follows between an input and the pixels: when the presenter is
///     told, when tiles are planned, and whether that happens now or once the view has settled.
/// </summary>
/// <remarks>
///     Extracted because every host had written it out again. The bookkeeping in it is small but
///     stateful - place tiles before requesting them, do not react to a viewport a gesture is already
///     driving, ask again once scrolling stops - and a mistake in any of it shows up as tiles that
///     never arrive or a zoom that steps twice. One copy is one place to fix that, and the only
///     copy that a test can reach without a running framework.
/// </remarks>
public sealed class ReportViewController
{
    private readonly ReportViewPresenter _presenter;
    private readonly IReportViewSurface _surface;
    private readonly TileSettleTimer _settleTimer;

    public ReportViewController(
        ReportViewPresenter presenter,
        ReportViewSession session,
        IReportViewSurface surface,
        TimeSpan? settleDelay = null)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(surface);

        _presenter = presenter;
        _surface = surface;
        Session = session;

        _settleTimer = new TileSettleTimer(RequestTiles, settleDelay);
        _settleTimer.Failed += exception => Failed?.Invoke(exception);

        _presenter.StateChanged += ReportState;
    }

    /// <summary>The loaded report's tile cache and its lifecycle.</summary>
    public ReportViewSession Session { get; }

    /// <summary>A tile request failed. Hosts surface this as their own render-failed event.</summary>
    public event Action<Exception>? Failed;

    /// <summary>The viewport scrolled. Ignored while a gesture is driving it.</summary>
    public void Scrolled()
    {
        if (_surface.SuppressesViewportReaction)
            return;

        _presenter.Scrolled();
        AfterPresenterUpdate(immediate: true);
    }

    /// <summary>The viewport's size or density changed.</summary>
    public void ViewportChanged()
    {
        _presenter.ViewportChanged();
        AfterPresenterUpdate(immediate: false);
    }

    /// <summary>
    ///     Brings the view up to date with whatever the presenter was just told.
    /// </summary>
    /// <param name="immediate">
    ///     <see langword="true"/> for a change that has already finished - a scroll that landed, a
    ///     gesture that committed - so the sharp cells are asked for now. <see langword="false"/> for
    ///     one that is still moving, which repaints what is already drawn and asks again once it
    ///     settles, rather than planning a set of cells that is obsolete before it is drawn.
    /// </param>
    public void AfterPresenterUpdate(bool immediate)
    {
        _surface.RefreshVisuals();

        if (immediate)
        {
            RequestTiles();
            return;
        }

        _surface.InvalidateSurface();
        _ = _settleTimer.ScheduleAsync();
    }

    /// <summary>Plans the cells the current viewport needs and asks the cache to draw them.</summary>
    public void RequestTiles()
    {
        if (Session.Tiles is not { } tiles)
            return;

        var (overdraw, budget) = _surface.TilePolicy;
        _presenter.VerticalOverdraw = overdraw;
        _presenter.RenderBudget = budget;

        if (_presenter.PlanTiles() is not { } plan)
            return;

        tiles.RequestTiles(plan.Requests);
        TilesRequested?.Invoke(plan);
    }

    /// <summary>
    ///     A plan has just been handed to the cache. For hosts that need to see the cells a pass
    ///     draws - Avalonia times a pinch commit against them - without reimplementing the planning.
    /// </summary>
    public event Action<TilePlan>? TilesRequested;

    private void ReportState() => _surface.ReportState(_presenter.CurrentPage, _presenter.EffectiveZoom);
}
