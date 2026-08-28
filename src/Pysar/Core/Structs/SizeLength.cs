using System.Globalization;

namespace Pysar.Core.Structs;

public enum SizeLengthType { Fixed, Auto, Fill }

public readonly struct SizeLength : IEquatable<SizeLength>
{
    public SizeLengthType Type  { get; }
    public float          Value { get; }   // meaningful only for Fixed

    public bool IsFixed => Type == SizeLengthType.Fixed;
    public bool IsAuto  => Type == SizeLengthType.Auto;
    public bool IsFill  => Type == SizeLengthType.Fill;

    private SizeLength(SizeLengthType type, float value)
    {
        Type  = type;
        Value = value;
    }

    public static SizeLength Auto           => new(SizeLengthType.Auto,  0f);
    public static SizeLength Fill           => new(SizeLengthType.Fill,  0f);
    public static SizeLength Fixed(float v) => new(SizeLengthType.Fixed, v);

    public static implicit operator SizeLength(int   v) => Fixed(v);
    public static implicit operator SizeLength(float v) => Fixed(v);

    public static SizeLength Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException($"Cannot parse SizeLength from empty string.");

        var trimmed = value.Trim();

        if (trimmed == "*" || trimmed.Equals("Fill", StringComparison.OrdinalIgnoreCase))
            return Fill;

        if (trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return Auto;

        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return Fixed(v);

        throw new FormatException(
            $"Cannot parse SizeLength from '{value}'. Expected 'Fill', '*', 'Auto', or a number.");
    }

    public override string ToString() => Type switch
    {
        SizeLengthType.Auto  => "Auto",
        SizeLengthType.Fill  => "*",
        _                    => Value.ToString(CultureInfo.InvariantCulture)
    };

    public bool Equals(SizeLength other) => Type == other.Type && Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is SizeLength other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Type, Value);
    public static bool operator ==(SizeLength l, SizeLength r) => l.Equals(r);
    public static bool operator !=(SizeLength l, SizeLength r) => !l.Equals(r);
}
