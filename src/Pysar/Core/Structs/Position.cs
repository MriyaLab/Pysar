namespace Pysar.Core.Structs;

public readonly struct Position(float? x, float? y)
{
    public float? X { get; init; } = x;
    public float? Y { get; init; } = y;

    public static readonly Position Zero = new(0, 0);
    public static readonly Position Empty = new(null, null);
    
    public bool IsEmpty => X == null && Y == null;
}
