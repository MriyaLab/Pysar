namespace Pysar.Core.Structs;

/// <summary>
///     Layout boxes for a report element. <see cref="Left"/>/<see cref="Top"/>/<see cref="Right"/>/
///     <see cref="Bottom"/> are the border-box (same as <c>LayoutNode.Bounds</c>); margin already sits
///     outside that box after measurement, so only <see cref="Padding"/> insets the content box.
/// </summary>
public readonly struct ElementLayout(
    float Left,
    float Top,
    float Right,
    float Bottom,
    Thickness Padding)
{
    public Rect Bounds => new(Left, Top, Right, Bottom);

    /// <summary>Border-box; kept for callers that historically inset by margin (now a no-op on bounds).</summary>
    public Rect OutputRect => Bounds;

    public Rect InnerRect => new(Left + Padding.Left, Top + Padding.Top,
        Right - Padding.Right, Bottom - Padding.Bottom);

    public static readonly ElementLayout Empty = new(0, 0, 0, 0, Thickness.Zero);
}
