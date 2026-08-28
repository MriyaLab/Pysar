using Pysar.Core.Enums;

namespace Pysar.Core.Structs;

public struct Font
{
    public Font()
    {
    }

    public Font(FontStyle fontStyle)
    {
        Style = fontStyle;
    }

    public Font(string fontFamily)
    {
        Family = fontFamily;
    }

    public Font(float fontSize)
    {
        Size = fontSize;
    }

    public Font(Color fontColor)
    {
        Color = fontColor;
    }

    public Font(Color fontColor, FontStyle fontStyle) : this(fontStyle)
    {
        Color = fontColor;
    }

    public Font(float fontSize, Color fontColor, FontStyle style = FontStyle.Normal) : this(fontColor, style)
    {
        Size = fontSize;
    }

    public Font(string fontFamily, Color fontColor, FontStyle style = FontStyle.Normal) : this(fontColor, style)
    {
        Family = fontFamily;
    }

    public Font(string fontFamily, float fontSize, FontStyle style = FontStyle.Normal) : this(style)
    {
        Family = fontFamily;
        Size = fontSize;
    }

    public Font(string fontFamily, float fontSize, Color fontColor, FontStyle style = FontStyle.Normal) : this(
        fontFamily, fontSize, style)
    {
        Color = fontColor;
    }

    public Font(string fontFamily, FontStyle fontStyle)
    {
        Family = fontFamily;
        Style = fontStyle;
    }

    public Font(float fontSize, FontStyle fontStyle)
    {
        Size = fontSize;
        Style = fontStyle;
    }

    public string Family { get; set; } = "Arial";
    public float Size { get; set; } = 12f;
    public FontStyle Style { get; set; } = FontStyle.Normal;
    public Color Color { get; set; } = Colors.Black;
}
