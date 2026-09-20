using Pysar.Viewer.Zoom;
using Xunit;

namespace Pysar.Viewer.Tests;

public class GestureModelTests
{
    private static (ZoomModel Zoom, GestureModel Gestures) Subject()
    {
        var zoom = new ZoomModel
        {
            PagePointWidth = 595.5,
            PagePointHeight = 842,
            ViewportWidth = 800,
            ViewportHeight = 1000,
            Mode = ReportZoomMode.Custom,
            Zoom = 1
        };

        return (zoom, new GestureModel(zoom));
    }

    [Fact]
    public void PinchSteps_Accumulate()
    {
        var (zoom, gestures) = Subject();

        gestures.BeginPinch();
        gestures.PinchByStep(1.5);
        gestures.PinchByStep(2);

        Assert.Equal(3, zoom.EffectiveZoom, 3);
    }

    [Fact]
    public void PinchScale_IsMeasuredAgainstTheStartOfTheGesture()
    {
        var (zoom, gestures) = Subject();

        gestures.BeginPinch();
        gestures.PinchByScale(1.5);
        gestures.PinchByScale(2);

        Assert.Equal(2, zoom.EffectiveZoom, 3);
    }

    [Fact]
    public void DoubleTap_MagnifiesAndThenReturnsToWhereItStarted()
    {
        var (zoom, gestures) = Subject();
        zoom.Mode = ReportZoomMode.FitWidth;

        gestures.DoubleTap();

        Assert.Equal(ReportZoomMode.Custom, zoom.Mode);
        Assert.Equal(GestureModel.DoubleTapZoom, zoom.EffectiveZoom, 3);

        gestures.DoubleTap();

        Assert.Equal(ReportZoomMode.FitWidth, zoom.Mode);
    }

    [Fact]
    public void DoubleTap_FromAChosenPercentage_ReturnsToThatPercentage()
    {
        var (zoom, gestures) = Subject();
        zoom.Zoom = 1;

        gestures.DoubleTap();
        gestures.DoubleTap();

        Assert.Equal(ReportZoomMode.Custom, zoom.Mode);
        Assert.Equal(1, zoom.EffectiveZoom, 3);
    }

    /// <summary>
    ///     A step too small to be worth a relayout must leave the model exactly where the host last
    ///     laid it out. Writing it and letting the host skip the relayout afterwards is what left the
    ///     drawing measuring a zoom nothing had been laid out for.
    /// </summary>
    [Fact]
    public void APinchFrameUnderTheThreshold_DoesNotReachTheModel()
    {
        var (zoom, gestures) = Subject();

        gestures.BeginPinch();

        Assert.False(gestures.PinchByStep(1.004));
        Assert.Equal(1, zoom.EffectiveZoom, 6);
    }

    [Fact]
    public void APinchScaleUnderTheThreshold_DoesNotReachTheModel()
    {
        var (zoom, gestures) = Subject();

        gestures.BeginPinch();

        Assert.False(gestures.PinchByScale(1.004));
        Assert.Equal(1, zoom.EffectiveZoom, 6);
    }

    /// <summary>
    ///     Held back is not discarded: a slow pinch, whose every frame is under the threshold, still
    ///     zooms on the frame its running total crosses it.
    /// </summary>
    [Fact]
    public void PinchFramesUnderTheThreshold_AccumulateUntilTheyAreWorthApplying()
    {
        var (zoom, gestures) = Subject();

        gestures.BeginPinch();

        Assert.False(gestures.PinchByStep(1.004));
        Assert.False(gestures.PinchByStep(1.004));

        // Three frames of 0.4% is 1.2%, which clears the 1% threshold - and what lands is the whole
        // accumulated total, not the one frame that happened to cross it.
        Assert.True(gestures.PinchByStep(1.004));
        Assert.Equal(Math.Pow(1.004, 3), zoom.EffectiveZoom, 6);
    }

    [Fact]
    public void APinch_ForgetsWhereADoubleTapWouldHaveReturnedTo()
    {
        var (zoom, gestures) = Subject();

        gestures.DoubleTap();

        gestures.BeginPinch();
        gestures.PinchByStep(1.5);

        gestures.DoubleTap();

        Assert.Equal(GestureModel.DoubleTapZoom, zoom.EffectiveZoom, 3);
    }
}
