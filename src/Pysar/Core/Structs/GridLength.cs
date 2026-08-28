using System.Globalization;

namespace Pysar.Core.Structs;

public enum GridLengthType { Fixed, Auto, Star }

public readonly struct GridLength
{
    public GridLengthType Type  { get; }
    public float          Value { get; }   // weight (Star) | points (Fixed) | 0 (Auto)

    private GridLength(GridLengthType type, float value)
    {
        Type  = type;
        Value = value;
    }

    public static GridLength Auto              => new(GridLengthType.Auto,  0f);
    public static GridLength Star(float weight = 1f) => new(GridLengthType.Star,  weight);
    public static GridLength Fixed(float points)     => new(GridLengthType.Fixed, points);

    public static GridLength Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmed = value.Trim();

        if (trimmed.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return Auto;

        if (trimmed.EndsWith('*'))
        {
            var prefix = trimmed[..^1];
            if (prefix.Length == 0)
                return Star(1f);
            if (float.TryParse(prefix, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
                return Star(weight);
            throw new FormatException($"Invalid star weight in GridLength: '{value}'");
        }

        if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var points))
            return Fixed(points);

        throw new FormatException($"Cannot parse GridLength: '{value}'");
    }

    public override string ToString() => Type switch
    {
        GridLengthType.Auto  => "Auto",
        GridLengthType.Star  => Value == 1f ? "*" : $"{Value.ToString(CultureInfo.InvariantCulture)}*",
        GridLengthType.Fixed => Value.ToString(CultureInfo.InvariantCulture),
        _                    => base.ToString()!
    };
}
