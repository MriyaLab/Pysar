namespace Pysar.Core.Structs;

public record struct Thickness(float left, float top, float right, float bottom)
{
    public float Left   { get; init; } = left;
    public float Top    { get; init; } = top;
    public float Right  { get; init; } = right;
    public float Bottom { get; init; } = bottom;

    public Thickness(float uniform) : this(uniform, uniform, uniform, uniform) { }

    public Thickness(float horizontal, float vertical)
        : this(horizontal, vertical, horizontal, vertical) { }

    public static readonly Thickness Zero = new(0);
}
