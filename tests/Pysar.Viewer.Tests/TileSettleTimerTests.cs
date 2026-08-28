using Pysar.Viewer.Tiles;
using Xunit;

namespace Pysar.Viewer.Tests;

public class TileSettleTimerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(20);

    [Fact]
    public async Task OneChange_AsksOnceTheViewHasStopped()
    {
        var asked = 0;
        var timer = new TileSettleTimer(() => asked++, Delay);

        await timer.ScheduleAsync();

        Assert.Equal(1, asked);
    }

    /// <summary>
    ///     A gesture raises an event per frame. Every frame must not cost a render pass: the timer
    ///     coalesces them into the one request that follows the last of them.
    /// </summary>
    [Fact]
    public async Task ManyChangesInARow_AskOnce()
    {
        var asked = 0;
        var timer = new TileSettleTimer(() => asked++, Delay);

        var first = timer.ScheduleAsync();

        for (var frame = 0; frame < 5; frame++)
        {
            await Task.Delay(5);
            _ = timer.ScheduleAsync();
        }

        await first;

        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task AfterItHasSettled_TheNextChangeAsksAgain()
    {
        var asked = 0;
        var timer = new TileSettleTimer(() => asked++, Delay);

        await timer.ScheduleAsync();
        await timer.ScheduleAsync();

        Assert.Equal(2, asked);
    }

    /// <summary>
    ///     A request that throws must not leave the timer thinking it is still settling, or the view
    ///     never asks for a cell again.
    /// </summary>
    [Fact]
    public async Task AFailedRequest_IsReportedAndTheTimerRecovers()
    {
        var fail = true;
        var failures = new List<Exception>();

        var timer = new TileSettleTimer(
            () =>
            {
                if (fail)
                    throw new InvalidOperationException("no");
            },
            Delay);

        timer.Failed += failures.Add;

        await timer.ScheduleAsync();

        Assert.Single(failures);

        fail = false;
        await timer.ScheduleAsync();

        Assert.Single(failures);
    }
}
