using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Zoom;
using Xunit;

namespace Pysar.Viewer.Tests;

public class PinchSessionTests
{
    private static (FakeHost Host, ReportViewPresenter Presenter, PinchSession Pinch) Subject()
    {
        var host = new FakeHost { ViewportWidth = 800, ViewportHeight = 1000 };
        var presenter = new ReportViewPresenter(host);

        presenter.SetDocument(pageCount: 3, pagePointWidth: 595.5f, pagePointHeight: 842f);
        presenter.ViewportChanged();

        return (host, presenter, new PinchSession(presenter));
    }

    private static readonly ViewPoint Centre = new(400, 500);

    [Fact]
    public void BeforeItBegins_ThereIsNothingToShow()
    {
        var (_, _, pinch) = Subject();

        Assert.False(pinch.Running);
        Assert.Null(pinch.MoveByStep(1.5));
    }

    [Fact]
    public void StepsAccumulateAcrossFrames()
    {
        var (_, presenter, pinch) = Subject();
        var start = presenter.EffectiveZoom;

        pinch.Begin(Centre);
        pinch.MoveByStep(1.25);

        var preview = pinch.MoveByStep(1.6);

        Assert.NotNull(preview);
        Assert.Equal(start * 2, preview!.Value.Zoom, 3);
        Assert.Equal(2, pinch.Scale, 3);
    }

    [Fact]
    public void AScaleIsMeasuredAgainstTheZoomTheGestureBeganAt()
    {
        var (_, presenter, pinch) = Subject();
        var start = presenter.EffectiveZoom;

        pinch.Begin(Centre);
        pinch.MoveByScale(1.5);

        var preview = pinch.MoveByScale(1.5);

        Assert.NotNull(preview);
        Assert.Equal(start * 1.5, preview!.Value.Zoom, 3);
    }

    /// <summary>
    ///     The anchor and the held point belong to the gesture, not to the frame: a scroll in the
    ///     middle of one must not change which point of the report the zoom is holding.
    /// </summary>
    [Fact]
    public void AScrollMidGesture_DoesNotChangeTheHeldPoint()
    {
        var (host, _, pinch) = Subject();

        pinch.Begin(Centre);
        var held = pinch.Held;

        host.ScrollY += 300;

        pinch.MoveByStep(1.2);

        Assert.Equal(held, pinch.Held);
        Assert.Equal(Centre, pinch.Anchor);
    }

    /// <summary>
    ///     Past the zoom limits the drawing must stop growing with the fingers, or the release gives
    ///     the difference back in one jump.
    /// </summary>
    [Fact]
    public void PastTheMaximumZoom_TheShownScaleStopsGrowing()
    {
        var (_, presenter, pinch) = Subject();
        var start = presenter.EffectiveZoom;

        pinch.Begin(Centre);
        pinch.MoveByStep(1000);

        Assert.Equal(ZoomModel.MaximumZoom / start, pinch.Scale, 3);
        Assert.Equal(ZoomModel.MaximumZoom / start, pinch.MoveByStep(2)!.Value.Zoom / start, 3);
    }

    [Fact]
    public void AGestureThatChangedAlmostNothing_IsNotWorthCommitting()
    {
        var (_, _, pinch) = Subject();

        pinch.Begin(Centre);
        pinch.MoveByStep(1.001);

        Assert.Null(pinch.End());
        Assert.False(pinch.Running);
    }

    [Fact]
    public void AGestureWorthCommitting_ReturnsTheFactorAndStops()
    {
        var (_, _, pinch) = Subject();

        pinch.Begin(Centre);
        pinch.MoveByStep(1.5);

        var commit = pinch.End();

        Assert.NotNull(commit);
        Assert.Equal(1.5, commit!.Value.Factor, 3);
        Assert.False(pinch.Running);
        Assert.Equal(1, pinch.Scale, 6);
    }

    /// <summary>
    ///     What <see cref="PinchSession.End"/> commits is the clamped factor the gesture actually
    ///     reached, not the raw input that pushed it past the limit - the headline reason the
    ///     running scale is read back from the presenter rather than kept as a local running total.
    /// </summary>
    [Fact]
    public void APinchPastTheMaximum_CommitsTheClampedFactorNotTheRawInput()
    {
        var (_, presenter, pinch) = Subject();
        var start = presenter.EffectiveZoom;

        pinch.Begin(Centre);
        pinch.MoveByStep(1000);

        var commit = pinch.End();

        Assert.NotNull(commit);
        Assert.Equal(ZoomModel.MaximumZoom / start, commit!.Value.Factor, 3);
    }

    /// <summary>
    ///     Mirrors <see cref="PastTheMaximumZoom_TheShownScaleStopsGrowing"/> for the other
    ///     direction: the two clamps go through different arithmetic in the presenter's scroll
    ///     handling, so the minimum needs its own coverage rather than trusting the maximum's.
    /// </summary>
    [Fact]
    public void PastTheMinimumZoom_TheShownScaleStopsShrinking()
    {
        var (_, presenter, pinch) = Subject();
        var start = presenter.EffectiveZoom;

        pinch.Begin(Centre);
        pinch.MoveByStep(0.001);

        Assert.Equal(ZoomModel.MinimumZoom / start, pinch.Scale, 3);
        Assert.Equal(ZoomModel.MinimumZoom / start, pinch.MoveByStep(0.5)!.Value.Zoom / start, 3);
    }

    [Fact]
    public void EndWithoutABegin_ReturnsNull()
    {
        var (_, _, pinch) = Subject();

        Assert.Null(pinch.End());
        Assert.False(pinch.Running);
    }

    /// <summary>
    ///     Avalonia's Mac magnify handler re-<c>Begin</c>s whenever the OS reports a new gesture
    ///     start without an intervening end (<c>if (e.Began || !_pinch.Running) _pinch.Begin(point)</c>).
    ///     The anchor and the held point belong to one gesture: carrying the previous gesture's into
    ///     this one would zoom around a point nobody touched.
    /// </summary>
    [Fact]
    public void BeginCalledTwice_ResetsToTheSecondGesture()
    {
        var (_, presenter, pinch) = Subject();

        pinch.Begin(Centre);
        pinch.MoveByStep(1.5);

        var second = new ViewPoint(100, 150);
        pinch.Begin(second);

        Assert.Equal(1, pinch.Scale);
        Assert.Equal(second, pinch.Anchor);
        Assert.Equal(presenter.PointAt(second), pinch.Held);
    }

    /// <summary>
    ///     Mac Catalyst runs two recognisers over the same fingers, and the one that ends first ends
    ///     the gesture for both. A frame arriving after that would scale the document by the anchor
    ///     of a gesture that is over, and leave it scaled with nothing left to put it back.
    /// </summary>
    [Fact]
    public void AFrameAfterTheEnd_IsIgnored()
    {
        var (_, _, pinch) = Subject();

        pinch.Begin(Centre);
        pinch.MoveByStep(1.5);
        pinch.End();

        Assert.Null(pinch.MoveByStep(1.5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveFactor_ShowsNothing(double factor)
    {
        var (_, _, pinch) = Subject();

        pinch.Begin(Centre);

        Assert.Null(pinch.MoveByStep(factor));
    }

    /// <summary>Without a document there is no point to hold and no preview to show.</summary>
    [Fact]
    public void WithoutAReport_ThereIsNothingToShow()
    {
        var host = new FakeHost();
        var presenter = new ReportViewPresenter(host);
        var pinch = new PinchSession(presenter);

        pinch.Begin(Centre);

        Assert.Null(pinch.Held);
        Assert.Null(pinch.MoveByStep(1.5));
    }
}
