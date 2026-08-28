using Pysar.Binding;
using Pysar.Core.Structs;

namespace Pysar.Elements;

public class FontImageSource : ImageSource
{
    public static BindableProperty GlyphProperty { get; } =
        BindableProperty.Create(nameof(Glyph), typeof(string), typeof(FontImageSource), string.Empty);

    public static BindableProperty FontFamilyProperty { get; } =
        BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(FontImageSource), string.Empty);

    public static BindableProperty SizeProperty { get; } =
        BindableProperty.Create(nameof(Size), typeof(float), typeof(FontImageSource), 30f);

    public static BindableProperty ColorProperty { get; } =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(FontImageSource), Colors.Black);

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty)!;
        set => SetValue(GlyphProperty, value);
    }

    public string FontFamily
    {
        get => (string)GetValue(FontFamilyProperty)!;
        set => SetValue(FontFamilyProperty, value);
    }

    public float Size
    {
        get => (float)GetValue(SizeProperty)!;
        set => SetValue(SizeProperty, value);
    }

    public Color Color
    {
        get => (Color)GetValue(ColorProperty)!;
        set => SetValue(ColorProperty, value);
    }

    public new FontImageSource Clone() => (FontImageSource)base.Clone();

    public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);
}
