namespace Pysar.Viewer.Zoom;

/// <summary>
///     Turns the zoom gestures into changes of <see cref="ZoomModel"/>.
/// </summary>
/// <remarks>
///     Two kinds of pinch report arrive from the platforms and they are not interchangeable: a step
///     since the previous event of the same gesture, which is what MAUI's cross-platform recogniser
///     and Android's ScaleGestureDetector give, and a scale against the start of the gesture, which
///     is what UIPinchGestureRecognizer gives. Treating one as the other is what made a pinch jump
///     between 412%, 500% and 422%, so each has its own entry point here.
///     <para>
///     A frame too small to be worth a relayout is held back here rather than written to the
///     <see cref="ZoomModel"/> and skipped by the host afterwards. A host that checked the threshold
///     after the fact left the model describing a zoom nothing had been laid out for, and every
///     later <c>Viewport()</c> - the extent, the tile plan, where a cell is placed, which page is at
///     the top - then measured the new zoom against pages still standing at the old one. It did not
///     correct itself and it accumulated: a trackpad pinch in a browser arrives as hundreds of wheel
///     events, most of them under the threshold, and the drawing drifted several percent away from
///     the pages under it while the reported zoom never moved at all.
///     </para>
/// </remarks>
public sealed class GestureModel(ZoomModel zoom)
{
    /// <summary>The zoom a double tap magnifies to.</summary>
    public const double DoubleTapZoom = 2;

    private double _pinchStartZoom;
    private double _pinchZoom;

    /// <summary>Where the reader was before a double tap magnified the view.</summary>
    private (ReportZoomMode Mode, double Zoom)? _beforeDoubleTap;

    /// <summary>
    ///     Starts a gesture. The frames that follow are accumulated against this point, so a stream
    ///     of frames too small to apply on their own still adds up to one that is.
    /// </summary>
    public void BeginPinch() => _pinchZoom = _pinchStartZoom = zoom.EffectiveZoom;

    /// <summary>Zooms by a step measured against the previous event of the same gesture.</summary>
    /// <returns>
    ///     Whether the zoom reached the <see cref="ZoomModel"/>. <see langword="false"/> means the
    ///     gesture so far is still under <see cref="ReportViewDefaults.ZoomStepThreshold"/> and
    ///     nothing has changed for a host to lay out - the step is not lost, it is carried until the
    ///     gesture has moved far enough to be worth one.
    /// </returns>
    public bool PinchByStep(double step)
    {
        if (_pinchStartZoom <= 0)
            return false;

        return Apply(Math.Clamp(_pinchZoom * step, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom));
    }

    /// <summary>Zooms by a scale measured against the zoom the gesture began at.</summary>
    /// <returns>Whether the zoom reached the <see cref="ZoomModel"/>; see <see cref="PinchByStep"/>.</returns>
    public bool PinchByScale(double scale)
    {
        if (_pinchStartZoom <= 0)
            return false;

        return Apply(Math.Clamp(_pinchStartZoom * scale, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom));
    }

    /// <summary>
    ///     Magnifies, and puts the reader back where they were when tapped again - so 100% goes to
    ///     200% and back to 100%, and a fit mode returns to that fit mode.
    /// </summary>
    public void DoubleTap()
    {
        if (_beforeDoubleTap is { } previous && zoom.EffectiveZoom >= DoubleTapZoom - 0.001)
        {
            _beforeDoubleTap = null;

            // The mode first: if it is a fit one it resolves the zoom by itself, and the factor set
            // after it then changes nothing. The other order would step through the old factor.
            zoom.Mode = previous.Mode;
            zoom.Zoom = previous.Zoom;

            // A fit mode resolves to whatever the viewport makes of it, so the running total is read
            // back from the model rather than assumed to be previous.Zoom.
            _pinchZoom = zoom.EffectiveZoom;

            return;
        }

        _beforeDoubleTap = (zoom.Mode, zoom.Zoom);

        SetZoom(DoubleTapZoom);
    }

    /// <summary>
    ///     Carries the gesture to <paramref name="value"/>, and writes it to the
    ///     <see cref="ZoomModel"/> if it has moved far enough from what is laid out to be worth the
    ///     relayout.
    /// </summary>
    /// <remarks>
    ///     The running total is kept whichever way that goes, so nothing a reader did is discarded:
    ///     a slow pinch whose every frame is under the threshold still zooms, on the frame the
    ///     accumulated total crosses it. What must never happen is the other half of that - writing a
    ///     value no host will lay out - which is why the threshold is decided here and not after the
    ///     write. See the remarks on this type.
    /// </remarks>
    private bool Apply(double value)
    {
        _pinchZoom = value;

        // A pinch replaces whatever a double tap was going to come back to, whether or not this
        // frame is the one that moves the zoom.
        _beforeDoubleTap = null;

        // Against what is in the model, not against where the gesture began: the frames that have
        // already been applied are paid for, and only the distance from the last of them is left to
        // justify another relayout.
        var applied = zoom.EffectiveZoom;

        if (Math.Abs(value - applied) < applied * ReportViewDefaults.ZoomStepThreshold)
            return false;

        SetZoom(value);

        return true;
    }

    private void SetZoom(double value)
    {
        // The factor first: while the mode is still a fit one the new factor changes nothing, so the
        // switch that follows is the single step the anchor is spent on.
        zoom.Zoom = value;
        zoom.Mode = ReportZoomMode.Custom;

        // A double tap writes the model without going through Apply, so the running total has to be
        // brought along here rather than there, or the next pinch frame would step from a zoom the
        // reader has already left.
        _pinchZoom = value;
    }
}
