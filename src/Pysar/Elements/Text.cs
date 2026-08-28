using Pysar.Binding;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements.Base;

namespace Pysar.Elements;

[ContentProperty(nameof(Content))]
[System.Windows.Markup.ContentProperty(nameof(Content))]
public class Text : ReportElement<Text>
{
    public static BindableProperty ContentProperty { get; } =
        BindableProperty.Create(nameof(Content), typeof(string), typeof(Text), string.Empty);

    public static BindableProperty FontProperty { get; } =
        BindableProperty.Create(nameof(Font), typeof(Font), typeof(Text), new Font());

    public static BindableProperty LineHeightProperty { get; } =
        BindableProperty.Create(nameof(LineHeight), typeof(float), typeof(Text), 1.2f);

    public static BindableProperty HorizontalTextAlignmentProperty { get; } =
        BindableProperty.Create(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(Text), TextAlignment.Start);

    public static BindableProperty VerticalTextAlignmentProperty { get; } =
        BindableProperty.Create(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(Text), TextAlignment.Center);

    public static BindableProperty TextTrimmingProperty { get; } =
        BindableProperty.Create(nameof(TextTrimming), typeof(TextTrimming), typeof(Text), TextTrimming.WordWrap);

    public static BindableProperty AutoHeightProperty { get; } =
        BindableProperty.Create(nameof(AutoHeight), typeof(bool), typeof(Text), false);

    public Text()
    {
        Size = Size.Auto;
    }

    public string Content
    {
        get => (string)GetValue(ContentProperty)!;
        set => SetValue(ContentProperty, value);
    }

    public Font Font
    {
        get => (Font)GetValue(FontProperty)!;
        set => SetValue(FontProperty, value);
    }

    // Facades over Font so XAML can set font parts as flat attributes (FontFamily="Ubuntu" FontSize="9"
    // FontStyle="Bold" FontColor="#444444"). Font is a struct: read a copy, mutate, write it back.
    public string FontFamily
    {
        get => Font.Family;
        set { var f = Font; f.Family = value; Font = f; }
    }

    public float FontSize
    {
        get => Font.Size;
        set { var f = Font; f.Size = value; Font = f; }
    }

    public FontStyle FontStyle
    {
        get => Font.Style;
        set { var f = Font; f.Style = value; Font = f; }
    }

    public Color FontColor
    {
        get => Font.Color;
        set { var f = Font; f.Color = value; Font = f; }
    }

    public float LineHeight
    {
        get => (float)GetValue(LineHeightProperty)!;
        set => SetValue(LineHeightProperty, value);
    }

    public TextAlignment HorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty)!;
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }

    public TextAlignment VerticalTextAlignment
    {
        get => (TextAlignment)GetValue(VerticalTextAlignmentProperty)!;
        set => SetValue(VerticalTextAlignmentProperty, value);
    }

    public TextTrimming TextTrimming
    {
        get => (TextTrimming)GetValue(TextTrimmingProperty)!;
        set => SetValue(TextTrimmingProperty, value);
    }

    public bool AutoHeight
    {
        get => (bool)GetValue(AutoHeightProperty)!;
        set => SetValue(AutoHeightProperty, value);
    }
    
    public Text WithFont(Font font)
    {
        Font = font;
        return this;
    }

    public Text WithFont(string fontFamily)
    {
        Font = new Font(fontFamily);
        return this;
    }

    public Text WithFont(float fontSize)
    {
        Font = new Font(fontSize);
        return this;
    }

    public Text WithFont(Color fontColor)
    {
        Font = new Font(fontColor);
        return this;
    }

    public Text WithFont(FontStyle fontStyle)
    {
        Font = new Font(fontStyle);
        return this;
    }

    public Text WithFont(string fontFamily, float fontSize)
    {
        Font = new Font(fontFamily, fontSize);
        return this;
    }

    public Text WithFont(string fontFamily, Color fontColor)
    {
        Font = new Font(fontFamily, fontColor);
        return this;
    }

    public Text WithFont(string fontFamily, FontStyle fontStyle)
    {
        Font = new Font(fontFamily, fontStyle);
        return this;
    }

    public Text WithFont(float fontSize, Color fontColor)
    {
        Font = new Font(fontSize, fontColor);
        return this;
    }

    public Text WithFont(float fontSize, FontStyle fontStyle)
    {
        Font = new Font(fontSize, fontStyle);
        return this;
    }

    public Text WithFont(float fontSize, Color fontColor, FontStyle fontStyle = FontStyle.Normal )
    {
        Font = new Font(fontSize, fontColor, fontStyle);
        return this;
    }

    public Text WithFont(string fontFamily, Color fontColor, FontStyle fontStyle = FontStyle.Normal )
    {
        Font = new Font(fontFamily, fontColor, fontStyle);
        return this;
    }

    public Text WithFont(string fontFamily, float fontSize, Color fontColor, FontStyle fontStyle = FontStyle.Normal )
    {
        Font = new Font(fontFamily, fontSize, fontColor, fontStyle);
        return this;
    }

    public Text WithLineHeight(float lineHeight)
    {
        LineHeight = lineHeight;
        return this;
    }

    public Text WithHorizontalTextAlignment(TextAlignment textAlignment)
    {
        HorizontalTextAlignment = textAlignment;
        return this;
    }

    public Text WithVerticalTextAlignment(TextAlignment textAlignment)
    {
        VerticalTextAlignment = textAlignment;
        return this;
    }

    public Text WithTextTrimming(TextTrimming textTrimming)
    {
        TextTrimming = textTrimming;
        return this;
    }

    public Text WithAutoHeight(bool autoHeight)
    {
        AutoHeight = autoHeight;
        return this;
    }
}
