namespace Pysar.Skia.Layout;

/// <summary>Measurement services. No canvas — the measure phase never draws.</summary>
public sealed class MeasureContext(float scale)
{
    public float Scale { get; } = scale;

    /// <summary>
    ///     Measurers registered for custom element types. Empty unless the application registered
    ///     one, in which case an <c>Auto</c> dimension on such an element is asked of it rather than
    ///     resolved to zero.
    /// </summary>
    public MeasurerRegistry Measurers { get; init; } = new();
}
