namespace Pysar.Core.Structs;

public readonly record struct Rect(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;
    public float Height => Bottom - Top;
    public bool IsEmpty => Width == 0 && Height == 0;

    public static readonly Rect Empty = new(0, 0, 0,0);

    public ElementLayout ToElementLayout(Thickness padding)
    {
        return new ElementLayout(Left, Top, Right, Bottom, padding);
    }
}