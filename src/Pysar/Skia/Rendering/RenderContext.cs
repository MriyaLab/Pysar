using SkiaSharp;

namespace Pysar.Skia.Rendering;

/// <summary>Draw-phase services: the target canvas and the point→pixel scale.</summary>
/// <param name="canvas">The surface being painted.</param>
/// <param name="scale">Point→pixel factor for what is drawn.</param>
/// <param name="measureScale">
///     The factor the layout was measured at, when it differs from <paramref name="scale"/>.
///     A viewer measures once and then draws the same layout at whatever the zoom asks for; text
///     line breaking has to follow the measurement rather than the zoom, or a line that fitted when
///     the page was laid out would re-break - or gain an ellipsis - purely because the user zoomed.
/// </param>
public sealed class RenderContext(SKCanvas canvas, float scale, float? measureScale = null)
{
    public SKCanvas Canvas { get; } = canvas;

    public float Scale { get; } = scale;

    /// <summary>The scale line breaking and truncation are decided at.</summary>
    public float MeasureScale { get; } = measureScale ?? scale;

    /// <summary>
    ///     When set, <see cref="ElementDrawer"/> skips nodes whose <see cref="Layout.LayoutNode.Bounds"/>
    ///     (same point space after the canvas section translate) do not intersect this rectangle.
    ///     Callers inflate for AA/stroke before assigning.
    /// </summary>
    public SKRect? CullBoundsPt { get; set; }

    public float ToPixels(float pt) => pt * Scale;

    /// <summary>Converts to the pixel space the layout was measured in.</summary>
    public float ToMeasuredPixels(float pt) => pt * MeasureScale;
}
