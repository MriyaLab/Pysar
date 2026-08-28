using System.Globalization;
using Pysar.Core.Structs;

namespace Pysar.Core;

/// <summary>
///     Converts an attribute/setter string to a target CLR value. Shared by the XAML loader and the
///     trigger engine so both parse literals identically. Throws <see cref="FormatException"/> on bad
///     input (callers may wrap it in a domain-specific exception).
/// </summary>
public static class ValueConverter
{
    public static bool IsConvertible(Type target)
    {
        var u = Nullable.GetUnderlyingType(target) ?? target;
        return u == typeof(string) || u.IsEnum || u == typeof(bool) || u == typeof(int)
            || u == typeof(float) || u == typeof(double)
            || u == typeof(Thickness) || u == typeof(SizeLength) || u == typeof(Size)
            || u == typeof(GridLength) || u == typeof(Color) || u == typeof(Position)
            || u == typeof(Uri)
            || u == typeof(MinMaxLength) || u == typeof(SizeConstraint);
    }

    public static object? Convert(string text, Type target)
    {
        if (target == typeof(string)) return text;

        var underlying = Nullable.GetUnderlyingType(target) ?? target;

        if (underlying.IsEnum) return Enum.Parse(underlying, text, ignoreCase: true);
        if (underlying == typeof(Thickness)) return ParseThickness(text);
        if (underlying == typeof(SizeLength)) return ParseSizeLength(text);
        if (underlying == typeof(Size)) return ParseSize(text);
        if (underlying == typeof(Position)) return ParsePosition(text);
        if (underlying == typeof(GridLength)) return GridLength.Parse(text);
        if (underlying == typeof(Color)) return Color.Parse(text);
        if (underlying == typeof(Uri)) return ParseUri(text);
        if (underlying == typeof(MinMaxLength)) return MinMaxLength.Parse(text);
        if (underlying == typeof(SizeConstraint)) return SizeConstraint.Parse(text);

        return System.Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture);
    }

    private static Thickness ParseThickness(string s)
    {
        var n = s.Split(',', StringSplitOptions.TrimEntries).Select(F).ToArray();
        return n.Length switch
        {
            1 => new Thickness(n[0]),
            2 => new Thickness(n[0], n[1]),
            4 => new Thickness(n[0], n[1], n[2], n[3]),
            _ => throw new FormatException($"Invalid Thickness '{s}' (expected 1, 2, or 4 numbers).")
        };
    }

    private static SizeLength ParseSizeLength(string s) => SizeLength.Parse(s);

    private static Size ParseSize(string s)
    {
        var parts = s.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) throw new FormatException($"Invalid Size '{s}' (expected 'width,height').");
        return new Size(ParseSizeLength(parts[0]), ParseSizeLength(parts[1]));
    }

    private static Position ParsePosition(string s)
    {
        var parts = s.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2) throw new FormatException($"Invalid Position '{s}' (expected 'x,y').");
        return new Position(F(parts[0]), F(parts[1]));
    }

    private static Uri ParseUri(string text)
    {
        if (!Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri))
            throw new FormatException($"Invalid Uri '{text}'.");
        return uri;
    }

    private static float F(string v) => float.Parse(v, CultureInfo.InvariantCulture);
}
