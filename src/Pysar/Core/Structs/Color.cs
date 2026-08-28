using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Pysar.Core.Structs;

[TypeConverter(typeof(ColorTypeConverter))]
public record struct Color(byte r, byte g, byte b, byte a = 255)
{
    public byte A { get; init; } = a;
    public byte R { get; init; } = r;
    public byte G { get; init; } = g;
    public byte B { get; init; } = b;

    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(r, g, b, a);

    public static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length != 6 && hex.Length != 8)
            throw new ArgumentException($"Hex color must be 6 or 8 characters after stripping '#', but got {hex.Length}.", nameof(hex));
        if (hex.Length == 6)
            hex = "FF" + hex;
        var value = Convert.ToUInt32(hex, 16);
        return FromArgb(
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >>  8) & 0xFF),
            (byte)( value        & 0xFF));
    }

    public static Color Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        value = value.Trim();
        if (value.StartsWith('#'))
            return FromHex(value);

        var field = typeof(Colors).GetField(
            value,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
        if (field?.GetValue(null) is Color color)
            return color;

        throw new FormatException($"Unknown color '{value}'.");
    }

    public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}

public sealed class ColorTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(
        ITypeDescriptorContext? context,
        CultureInfo? culture,
        object value)
        => value is string text
            ? Color.Parse(text)
            : base.ConvertFrom(context, culture, value);
}

public struct Colors
{
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Transparent = new(0, 0, 0, 0);

    public static readonly Color LightGray = new(211, 211, 211);
    public static readonly Color DarkGray = new(169, 169, 169);
    public static readonly Color Gray = new(128, 128, 128);
    public static readonly Color DimGray = new(105, 105, 105);
    public static readonly Color Gainsboro = new(220, 220, 220);

    public static readonly Color Red = new(255, 0, 0);
    public static readonly Color DarkRed = new(139, 0, 0);
    public static readonly Color IndianRed = new(205, 92, 92);
    public static readonly Color Firebrick = new(178, 34, 34);

    public static readonly Color Green = new(0, 128, 0);
    public static readonly Color DarkGreen = new(0, 100, 0);
    public static readonly Color Lime = new(0, 255, 0);
    public static readonly Color ForestGreen = new(34, 139, 34);
    public static readonly Color Olive = new(128, 128, 0);

    public static readonly Color Blue = new(0, 0, 255);
    public static readonly Color DarkBlue = new(0, 0, 139);
    public static readonly Color Navy = new(0, 0, 128);
    public static readonly Color RoyalBlue = new(65, 105, 225);
    public static readonly Color SkyBlue = new(135, 206, 235);
    public static readonly Color LightBlue = new(173, 216, 230);

    public static readonly Color Yellow = new(255, 255, 0);
    public static readonly Color Gold = new(255, 215, 0);
    public static readonly Color Khaki = new(240, 230, 140);

    public static readonly Color Orange = new(255, 165, 0);
    public static readonly Color DarkOrange = new(255, 140, 0);
    public static readonly Color Coral = new(255, 127, 80);

    public static readonly Color Purple = new(128, 0, 128);
    public static readonly Color Violet = new(238, 130, 238);
    public static readonly Color Indigo = new(75, 0, 130);
    public static readonly Color Plum = new(221, 160, 221);

    public static readonly Color Pink = new(255, 192, 203);
    public static readonly Color HotPink = new(255, 105, 180);
    public static readonly Color DeepPink = new(255, 20, 147);

    public static readonly Color Brown = new(165, 42, 42);
    public static readonly Color SaddleBrown = new(139, 69, 19);
    public static readonly Color Chocolate = new(210, 105, 30);
    public static readonly Color Peru = new(205, 133, 63);

    public static readonly Color Cyan = new(0, 255, 255);
    public static readonly Color Azure = new(240, 255, 255);
    public static readonly Color Teal = new(0, 128, 128);
    public static readonly Color Turquoise = new(64, 224, 208);

    public static readonly Color Magenta = new(255, 0, 255);
}
