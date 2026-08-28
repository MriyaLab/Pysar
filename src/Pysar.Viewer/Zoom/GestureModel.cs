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
/// </remarks>
public sealed class GestureModel(ZoomModel zoom)
{
    /// <summary>The zoom a double tap magnifies to.</summary>
    public const double DoubleTapZoom = 2;

    private double _pinchStartZoom;
    private double _pinchZoom;

    /// <summary>Where the reader was before a double tap magnified the view.</summary>
    private (ReportZoomMode Mode, double Zoom)? _beforeDoubleTap;

    public void BeginPinch() => _pinchZoom = _pinchStartZoom = zoom.EffectiveZoom;

    /// <summary>Zooms by a step measured against the previous event of the same gesture.</summary>
    public void PinchByStep(double step)
    {
        if (_pinchStartZoom <= 0)
            return;

        _pinchZoom = Math.Clamp(_pinchZoom * step, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom);

        Apply(_pinchZoom);
    }

    /// <summary>Zooms by a scale measured against the zoom the gesture began at.</summary>
    public void PinchByScale(double scale)
    {
        if (_pinchStartZoom <= 0)
            return;

        Apply(Math.Clamp(_pinchStartZoom * scale, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom));
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

            return;
        }

        _beforeDoubleTap = (zoom.Mode, zoom.Zoom);

        SetZoom(DoubleTapZoom);
    }

    private void Apply(double value)
    {
        // A pinch replaces whatever a double tap was going to come back to.
        _beforeDoubleTap = null;

        SetZoom(value);
    }

    private void SetZoom(double value)
    {
        // The factor first: while the mode is still a fit one the new factor changes nothing, so the
        // switch that follows is the single step the anchor is spent on.
        zoom.Zoom = value;
        zoom.Mode = ReportZoomMode.Custom;
    }
}
