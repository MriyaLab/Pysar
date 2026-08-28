namespace Pysar.Core.Structs;

public readonly struct SizeConstraint : IEquatable<SizeConstraint>
{
    public MinMaxLength Width  { get; }
    public MinMaxLength Height { get; }

    public SizeConstraint(MinMaxLength width, MinMaxLength height)
    {
        Width  = width;
        Height = height;
    }

    public static SizeConstraint None => new(MinMaxLength.None, MinMaxLength.None);

    public static SizeConstraint Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return None;

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new FormatException(
                $"Cannot parse SizeConstraint from '{value}'. Expected 'width,height'.");

        return new SizeConstraint(MinMaxLength.Parse(parts[0]), MinMaxLength.Parse(parts[1]));
    }

    public override string ToString() => $"{Width},{Height}";

    public bool Equals(SizeConstraint other) => Width.Equals(other.Width) && Height.Equals(other.Height);
    public override bool Equals(object? obj) => obj is SizeConstraint other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Width, Height);
    public static bool operator ==(SizeConstraint left, SizeConstraint right) => left.Equals(right);
    public static bool operator !=(SizeConstraint left, SizeConstraint right) => !left.Equals(right);
}
