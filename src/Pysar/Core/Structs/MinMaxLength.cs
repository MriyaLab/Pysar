using System.Globalization;

namespace Pysar.Core.Structs;

public enum MinMaxLengthType { None, Fixed }

public readonly struct MinMaxLength : IEquatable<MinMaxLength>
{
    public MinMaxLengthType Type  { get; }
    public float            Value { get; }   // meaningful only for Fixed

    public bool IsNone  => Type == MinMaxLengthType.None;
    public bool IsFixed => Type == MinMaxLengthType.Fixed;

    private MinMaxLength(MinMaxLengthType type, float value)
    {
        Type  = type;
        Value = value;
    }

    public static MinMaxLength None => new(MinMaxLengthType.None, 0f);

    public static MinMaxLength Fixed(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be a finite number.");

        return new(MinMaxLengthType.Fixed, value < 0f ? 0f : value);
    }

    public static implicit operator MinMaxLength(int   value) => Fixed(value);
    public static implicit operator MinMaxLength(float value) => Fixed(value);

    public static MinMaxLength Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return None;

        var trimmed = value.Trim();

        if (trimmed.Equals("None", StringComparison.OrdinalIgnoreCase))
            return None;

        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return Fixed(number);

        throw new FormatException(
            $"Cannot parse MinMaxLength from '{value}'. Expected 'None' or a number.");
    }

    public override string ToString() => Type switch
    {
        MinMaxLengthType.None => "None",
        _                     => Value.ToString(CultureInfo.InvariantCulture)
    };

    public bool Equals(MinMaxLength other) => Type == other.Type && Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is MinMaxLength other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Type, Value);
    public static bool operator ==(MinMaxLength left, MinMaxLength right) => left.Equals(right);
    public static bool operator !=(MinMaxLength left, MinMaxLength right) => !left.Equals(right);
}
