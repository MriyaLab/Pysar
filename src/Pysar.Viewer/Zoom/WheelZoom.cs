namespace Pysar.Viewer.Zoom;

/// <summary>
///     A wheel notch as a zoom step, for the desktop hosts.
/// </summary>
/// <remarks>
///     Not used by the browser host, and deliberately so, for two separate reasons. The curve: a
///     browser reports <c>wheel.deltaY</c> in pixels - around a hundred per notch - where WPF
///     reports 120 and Avalonia reports 1, so the curve that suits one does not suit the other. The
///     interaction model, independently of the curve: the desktop wheel handled here commits and
///     relays out on every notch - <c>BeginPinch</c>, <c>PinchByStep</c>, then an immediate apply -
///     while the browser wheel is a pinch stream, accumulating a running factor, previewing it
///     through a CSS transform per animation frame, and committing once after the stream of wheel
///     events settles. Porting this curve's constant into <c>reportView.js</c> would not unify the
///     two behaviours; the interaction they drive apart is not just the numbers.
///     <c>reportView.js</c> keeps its own curve and its own gesture.
/// </remarks>
public static class WheelZoom
{
    /// <summary>
    ///     How much one notch changes the zoom, as a fraction of the current zoom. A trackpad's
    ///     Avalonia delta was measured between 0.02 and 0.16 per event over a pinch; at this
    ///     sensitivity that is a 1% to 8% step - just inside
    ///     <see cref="ReportViewDefaults.ZoomStepThreshold"/> at the small end, and a brisk zoom at
    ///     the large end.
    /// </summary>
    private const double Sensitivity = 0.5;

    /// <summary>
    ///     Wheel units per notch on Windows, which reports in whole multiples of this while
    ///     Avalonia's delta is already close to the ±1 range <see cref="StepFor"/> expects.
    ///     Avalonia's unit is the canonical one; Windows is the one being normalised toward it - the
    ///     unit a future host should convert its own delta into, not the other way round.
    /// </summary>
    private const double WindowsUnitsPerNotch = 120;

    /// <summary>
    ///     The zoom step one wheel event amounts to. Clamped to a single notch first, so a physical
    ///     mouse wheel - whose notch can report a much larger delta than a trackpad's - never turns
    ///     one click into more than a 50% jump.
    /// </summary>
    /// <remarks>
    ///     Not reciprocal: a notch in is 1.5 and a notch out is 0.5, and 1.5 * 0.5 = 0.75, so one
    ///     notch in followed by one notch out does not return the reader to where they started, and
    ///     the gap compounds over repeated pairs. Pre-existing behaviour of both desktop hosts from
    ///     before this arithmetic had one name - not something this task changes.
    /// </remarks>
    public static double StepFor(double notches)
        => 1 + Math.Clamp(notches, -1, 1) * Sensitivity;

    /// <summary>The same, for a host whose delta is in Windows wheel units.</summary>
    public static double StepForWindowsDelta(double delta)
        => StepFor(delta / WindowsUnitsPerNotch);
}
