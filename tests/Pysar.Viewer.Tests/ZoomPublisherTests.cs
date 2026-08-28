using Pysar.Viewer.Zoom;
using Xunit;

namespace Pysar.Viewer.Tests;

public class ZoomPublisherTests
{
    /// <summary>
    ///     Records which of the two was written, in the order it was written, and the value with it.
    ///     The order is what the tests are about, so it is recorded as its own list rather than
    ///     formatted into a string - a formatted double would compare differently under a culture
    ///     with another decimal separator.
    /// </summary>
    private sealed class RecordingSink : IZoomSink
    {
        public List<string> Writes { get; } = [];

        public double LastFactor { get; private set; }

        public ReportZoomMode? LastMode { get; private set; }

        public ZoomPublisher? Publisher { get; set; }

        /// <summary>What <see cref="ZoomPublisher.Publishing"/> said while the sink was running.</summary>
        public List<bool> PublishingWhileWriting { get; } = [];

        public void SetZoomFactor(double zoom)
        {
            Writes.Add("factor");
            LastFactor = zoom;
            PublishingWhileWriting.Add(Publisher?.Publishing ?? false);
        }

        public void SetZoomMode(ReportZoomMode mode)
        {
            Writes.Add("mode");
            LastMode = mode;
            PublishingWhileWriting.Add(Publisher?.Publishing ?? false);
        }
    }

    private static (RecordingSink Sink, ZoomPublisher Publisher) Subject()
    {
        var sink = new RecordingSink();
        var publisher = new ZoomPublisher(sink);
        sink.Publisher = publisher;

        return (sink, publisher);
    }

    /// <summary>
    ///     Magnifying: the factor first, so the switch that follows is the single step the anchor is
    ///     spent on. The other order lands on the old custom factor first and jumps twice.
    /// </summary>
    [Fact]
    public void ACustomZoom_IsPublishedFactorFirst()
    {
        var (sink, publisher) = Subject();

        publisher.Publish(ReportZoomMode.Custom, 2);

        Assert.Equal(new[] { "factor", "mode" }, sink.Writes);
        Assert.Equal(2, sink.LastFactor, 6);
        Assert.Equal(ReportZoomMode.Custom, sink.LastMode);
    }

    /// <summary>
    ///     Returning to a fit mode: the mode first, since it resolves the zoom by itself and the
    ///     factor set after it then changes nothing.
    /// </summary>
    [Theory]
    [InlineData(ReportZoomMode.FitWidth)]
    [InlineData(ReportZoomMode.FitPage)]
    public void AFitMode_IsPublishedModeFirst(ReportZoomMode mode)
    {
        var (sink, publisher) = Subject();

        publisher.Publish(mode, 1.75);

        Assert.Equal(new[] { "mode", "factor" }, sink.Writes);
        Assert.Equal(1.75, sink.LastFactor, 6);
        Assert.Equal(mode, sink.LastMode);
    }

    [Fact]
    public void WhilePublishing_TheHostCanTellItIsItsOwnWriteComingBack()
    {
        var (sink, publisher) = Subject();

        publisher.Publish(ReportZoomMode.Custom, 2);

        Assert.Equal(new[] { true, true }, sink.PublishingWhileWriting);
        Assert.False(publisher.Publishing);
    }

    /// <summary>A sink that throws must not leave the flag set, or the host stops reacting for good.</summary>
    [Fact]
    public void AThrowingSink_LeavesTheFlagClear()
    {
        var publisher = new ZoomPublisher(new ThrowingSink());

        Assert.Throws<InvalidOperationException>(
            () => publisher.Publish(ReportZoomMode.Custom, 2));

        Assert.False(publisher.Publishing);
    }

    private sealed class ThrowingSink : IZoomSink
    {
        public void SetZoomFactor(double zoom) => throw new InvalidOperationException();

        public void SetZoomMode(ReportZoomMode mode) => throw new InvalidOperationException();
    }
}
