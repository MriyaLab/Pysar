using Pysar.Elements;
using Pysar.Skia;
using Pysar.Viewer.Tiles;
using SkiaSharp;

namespace Pysar.Viewer;

/// <summary>
///     Loading one report into one viewer: the cancellation, the tile cache, and the wiring between
///     them and the presenter.
/// </summary>
/// <remarks>
///     Every load supersedes the one before it, and the one before it may still be rendering. The
///     order here is the whole of the type: cancel, dispose, tell the presenter there is nothing,
///     and only then build anything new - so a cache that is still finishing cannot place a cell
///     belonging to a report the reader has already navigated away from.
///     <para>
///     A load releases the previous report's bitmaps, but a control destroyed without ever loading
///     again - the window closes, the page navigates away - has no next load to release the last
///     one. <see cref="DisposeWhenStillDetached"/> is what the hosts call from their teardown hooks
///     for that case; it carries the reasoning about why detaching alone is not enough to go on.
///     </para>
/// </remarks>
public sealed class ReportViewSession(
    ReportViewPresenter presenter,
    IReportViewHost host,
    IRenderScheduler scheduler,
    Func<SKBitmap, byte[]>? package = null,
    int maxDegreeOfParallelism = 0) : IDisposable
{
    private CancellationTokenSource? _cancellation;

    /// <summary>The cache for the loaded report, or <c>null</c> when there is none.</summary>
    public ReportViewTiles? Tiles { get; private set; }

    /// <summary>New pixels have arrived; raised on the host's own thread.</summary>
    public event Action? Invalidated;

    /// <summary>A render failed, or the report could not be prepared.</summary>
    public event Action<Exception>? Failed;

    /// <summary>Everything belonging to the previous report has gone; the host should clear its views.</summary>
    public event Action? Cleared;

    /// <summary>
    ///     The new report is in the presenter; the host should lay it out. Raised twice: once now,
    ///     and once after this turn of the user-interface loop.
    /// </summary>
    /// <remarks>
    ///     The first layout can finish after the session does, and an extent measured against a
    ///     viewport that does not exist yet leaves a scroll viewer with nothing to scroll until
    ///     something else disturbs it - a zoom change, say. Asking again on the next turn gives it
    ///     the real viewport. The handler must therefore be safe to run twice.
    /// </remarks>
    public event Action? Loaded;

    /// <summary>
    ///     Replaces whatever is loaded with <paramref name="report"/>, or with nothing when it is
    ///     <c>null</c>.
    /// </summary>
    public async Task LoadAsync(Report? report, SkiaReportRenderer renderer)
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;

        Tiles?.Dispose();
        Tiles = null;
        presenter.SetTiles(null);

        Cleared?.Invoke();

        if (report is null)
            return;

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;

        try
        {
            var session = await renderer.CreateSessionAsync(report, cancellation.Token);

            if (cancellation.IsCancellationRequested)
                return;

            var tiles = new ReportViewTiles(session, scheduler, package, maxDegreeOfParallelism);

            tiles.Invalidated += () => host.Post(() => Invalidated?.Invoke());
            tiles.Failed += exception => host.Post(() => Failed?.Invoke(exception));

            Tiles = tiles;
            presenter.SetTiles(tiles);

            _ = tiles.LoadBaseLayersAsync(cancellation.Token);

            Loaded?.Invoke();
            host.Post(() => Loaded?.Invoke());
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer report.
        }
        catch (Exception exception)
        {
            Failed?.Invoke(exception);
        }
    }

    /// <summary>
    ///     Releases the loaded report if <paramref name="isAttached"/> still reports the view as gone
    ///     by the next turn of the user-interface loop. Call it from the host's detach hook.
    /// </summary>
    /// <remarks>
    ///     Every platform's detach hook - WPF's <c>Unloaded</c>, Avalonia's
    ///     <c>OnDetachedFromVisualTree</c>, MAUI's handler disconnect - also fires when a control is
    ///     merely reparented, so detaching is not on its own a reason to tear anything down.
    ///     Reparenting completes within one turn of the loop, so asking again on the next turn
    ///     separates the two: a view that came back answers <c>true</c> and keeps its cache, and one
    ///     that is really gone releases its bitmaps now instead of when the collector reaches them.
    /// </remarks>
    public void DisposeWhenStillDetached(Func<bool> isAttached)
    {
        ArgumentNullException.ThrowIfNull(isAttached);

        host.Post(() =>
        {
            if (isAttached())
                return;

            Dispose();
        });
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;

        Tiles?.Dispose();
        Tiles = null;
    }
}
