using Pysar.Viewer.Tiles;

namespace Pysar.Blazor;

/// <summary>
///     Draws on the thread the interface runs on, giving the browser a turn first.
/// </summary>
/// <remarks>
///     WebAssembly has no background thread to hand this to, so <c>TaskRunScheduler</c> - which every
///     desktop host uses - would run the work inline and freeze the page for as long as a cell takes.
///     Yielding first lets the browser paint the cells that have already arrived and process the
///     reader's scrolling before the next one starts.
/// </remarks>
public sealed class YieldingRenderScheduler : IRenderScheduler
{
    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        await Task.Yield();

        ct.ThrowIfCancellationRequested();

        return await work(ct);
    }
}
