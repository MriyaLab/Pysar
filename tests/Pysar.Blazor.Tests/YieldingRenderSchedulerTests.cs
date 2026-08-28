using Pysar.Blazor;
using Xunit;

namespace Pysar.Blazor.Tests;

public class YieldingRenderSchedulerTests
{
    [Fact]
    public async Task TheWork_DoesNotStartBeforeTheCallerGetsControlBack()
    {
        // Task.Yield() only defers onto the caller's SynchronizationContext; without one (the
        // xunit default) its continuation races onto the thread pool and can start before this
        // thread reaches the assert. A single-threaded context - standing in for the one
        // WebAssembly always installs - makes the deferral deterministic, like the real host.
        var previousContext = SynchronizationContext.Current;
        var context = new QueuingSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var scheduler = new YieldingRenderScheduler();
            var started = false;

            // The whole point of this scheduler. In the browser there is one thread, so a scheduler that
            // ran the work inline would draw a cell before the page had a chance to show the last one.
            var running = scheduler.RunAsync(
                _ =>
                {
                    started = true;
                    return Task.FromResult(1);
                },
                CancellationToken.None);

            Assert.False(started);

            context.RunUntilIdle();
            await running;

            Assert.True(started);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private sealed class QueuingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void RunUntilIdle()
        {
            while (_queue.Count > 0)
            {
                var (callback, state) = _queue.Dequeue();
                callback(state);
            }
        }
    }

    [Fact]
    public async Task TheResult_ComesBack()
    {
        var scheduler = new YieldingRenderScheduler();

        Assert.Equal(42, await scheduler.RunAsync(_ => Task.FromResult(42), CancellationToken.None));
    }

    [Fact]
    public async Task ACancelledToken_StopsTheWorkFromRunning()
    {
        var scheduler = new YieldingRenderScheduler();
        var started = false;

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scheduler.RunAsync(
                _ =>
                {
                    started = true;
                    return Task.FromResult(1);
                },
                cancellation.Token));

        Assert.False(started);
    }
}
