using Pysar.Viewer.Zoom;
using Xunit;

namespace Pysar.Viewer.Tests;

public class WheelZoomTests
{
    [Fact]
    public void NoNotch_LeavesTheZoomAlone()
    {
        Assert.Equal(1, WheelZoom.StepFor(0), 6);
    }

    [Fact]
    public void OneNotchForward_ZoomsInByTheSensitivity()
    {
        Assert.Equal(1.5, WheelZoom.StepFor(1), 6);
    }

    [Fact]
    public void OneNotchBack_ZoomsOutByTheSensitivity()
    {
        Assert.Equal(0.5, WheelZoom.StepFor(-1), 6);
    }

    /// <summary>
    ///     A physical mouse wheel can report a far larger delta than a trackpad's, and a single
    ///     click must not become an arbitrary jump.
    /// </summary>
    [Theory]
    [InlineData(10, 1.5)]
    [InlineData(-10, 0.5)]
    public void ALargeNotch_IsClampedToOne(double notches, double expected)
    {
        Assert.Equal(expected, WheelZoom.StepFor(notches), 6);
    }

    /// <summary>Windows reports 120 units per notch; the core wants notches.</summary>
    [Fact]
    public void WindowsUnits_AreOneNotchPer120()
    {
        Assert.Equal(1.5, WheelZoom.StepForWindowsDelta(120), 6);
        Assert.Equal(0.5, WheelZoom.StepForWindowsDelta(-120), 6);
    }

    /// <summary>A delta short of a full notch is not rounded up to one; it scales fractionally.</summary>
    [Fact]
    public void AFractionOfANotch_ScalesFractionally()
    {
        Assert.Equal(1.25, WheelZoom.StepForWindowsDelta(60), 6);
    }

    /// <summary>
    ///     Pins the range the private <c>Sensitivity</c> constant's own remarks stake out - a
    ///     trackpad's measured 0.02 to 0.16 per event - to the 1% to 8% step it claims. It also pins the
    ///     <see cref="ReportViewDefaults.ZoomStepThreshold"/> boundary: at 0.02 the step is exactly
    ///     1% of the zoom, which clears the threshold only because that comparison is a strict
    ///     <c>&lt;</c> rather than <c>&lt;=</c>.
    /// </summary>
    [Theory]
    [InlineData(0.02, 1.01)]
    [InlineData(0.16, 1.08)]
    public void TheMeasuredTrackpadRange_ProducesTheClaimedStep(double notches, double expected)
    {
        Assert.Equal(expected, WheelZoom.StepFor(notches), 6);
    }
}
