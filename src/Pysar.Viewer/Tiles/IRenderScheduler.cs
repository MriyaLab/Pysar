namespace Pysar.Viewer.Tiles;

/// <summary>
///     Where a page or a cell is actually drawn.
/// </summary>
/// <remarks>
///     Not simply <see cref="Task.Run"/>: in the browser threads are not available by default, so
///     that host runs the work on the UI thread and yields to the loop between cells instead.
/// </remarks>
public interface IRenderScheduler
{
    /// <summary>
    ///     Runs <paramref name="work"/> wherever this host draws, and completes with its result.
    /// </summary>
    /// <remarks>
    ///     The work is asynchronous, not a plain function, on purpose: rendering a region is itself
    ///     an awaitable, and a scheduler that had to block on it would deadlock the moment the host
    ///     ran the work on the thread it was waiting on - which is exactly what the browser does.
    /// </remarks>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}

/// <summary>The scheduler for every host that has threads.</summary>
public sealed class TaskRunScheduler : IRenderScheduler
{
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        => Task.Run(() => work(ct), ct);
}
