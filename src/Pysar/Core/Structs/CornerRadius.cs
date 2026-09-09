namespace Pysar.Core.Structs;

/// <summary>
///     Per-corner radii, in the MAUI order: top-left, top-right, bottom-left, bottom-right.
/// </summary>
public record struct CornerRadius(float topLeft, float topRight, float bottomLeft, float bottomRight)
{
    public float TopLeft     { get; init; } = topLeft;
    public float TopRight    { get; init; } = topRight;
    public float BottomLeft  { get; init; } = bottomLeft;
    public float BottomRight { get; init; } = bottomRight;

    public CornerRadius(float uniform) : this(uniform, uniform, uniform, uniform) { }

    public static readonly CornerRadius Zero = new(0);

    /// <summary>
    ///     True when no corner is rounded. Render paths branch on this rather than on equality with
    ///     <see cref="Zero"/> so that negative radii — which Skia cannot draw — take the square path.
    /// </summary>
    public bool IsZero => TopLeft <= 0 && TopRight <= 0 && BottomLeft <= 0 && BottomRight <= 0;
}
