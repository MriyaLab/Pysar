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
