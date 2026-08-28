namespace Pysar.Viewer.Zoom;

/// <summary>Where a host mirrors the zoom the core settled on, so a binding can see it.</summary>
/// <remarks>
///     Two methods rather than one call taking both: the two writes are separate on every host - a
///     dependency property, a styled property, a bindable property, an event callback - and the
///     order they happen in is the whole point of <see cref="ZoomPublisher"/>.
/// </remarks>
public interface IZoomSink
{
    void SetZoomFactor(double zoom);

    void SetZoomMode(ReportZoomMode mode);
}

/// <summary>
///     Writes a zoom back to the host in the order that costs one step rather than two.
/// </summary>
/// <remarks>
///     A fit mode resolves the zoom by itself, so writing the mode first makes the factor that
///     follows change nothing; writing the factor first would step through it and then through the
///     mode, and the reader sees the view jump twice. Magnifying to a custom factor is the mirror
///     image: the factor first, because while the mode is still a fit one the new factor changes
///     nothing, and the switch that follows is the single step the zoom's anchor is spent on.
///     <para>
///     Copied into four controls and inverted between two call sites in each, this was the most
///     duplicated decision in the viewer and the one with the least visible failure mode.
///     </para>
/// </remarks>
public sealed class ZoomPublisher(IZoomSink sink)
{
    /// <summary>
    ///     Set while the sink is being written. A host reads it from its own property-changed
    ///     handler: the values it is about to see are the ones it just published, and asking the
    ///     presenter to zoom again for them would replace the gesture's anchor with the viewport's
    ///     centre.
    /// </summary>
    public bool Publishing { get; private set; }

    public void Publish(ReportZoomMode mode, double zoom)
    {
        Publishing = true;
        try
        {
            if (mode == ReportZoomMode.Custom)
            {
                sink.SetZoomFactor(zoom);
                sink.SetZoomMode(mode);
            }
            else
            {
                sink.SetZoomMode(mode);
                sink.SetZoomFactor(zoom);
            }
        }
        finally
        {
            Publishing = false;
        }
    }
}
