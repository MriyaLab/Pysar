using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Helpers;
using Pysar.Skia.Layout;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

/// <summary>
///     An Auto-height <see cref="Text"/> is drawn with its first baseline an ascent below the box top,
///     so the box has to be at least ascent+descent tall or the parent container clips the descenders.
///     <c>Font.Size * LineHeight</c> alone is not enough for a font whose own extent exceeds its line
///     height (Kanit and most faces carrying Thai or Devanagari, or any explicit small LineHeight).
/// </summary>
public class TextDescenderMeasurementTests
{
    private static readonly MeasureContext Ctx = new(scale: 1f);

    /// <summary>The font's own ascent+descent for <paramref name="text"/>, in points.</summary>
    private static float FontExtent(Text text)
    {
        using var font = TextMeasurer.BuildFont(text, text.Font.Size, FontService.GetTypeface(text.Font));
        return font.Metrics.Descent - font.Metrics.Ascent;
    }

    private static Text NarrowLineHeightText(TextTrimming trimming) => new()
    {
        Content = "gjpqy",
        Font = new Font("Arial", 20),
        LineHeight = 0.5f,          // deliberately below the font's own extent
        TextTrimming = trimming,
        Size = Size.Auto
    };

    [Theory]
    [InlineData(TextTrimming.None)]
    [InlineData(TextTrimming.Clip)]
    [InlineData(TextTrimming.WordWrap)]
    [InlineData(TextTrimming.TailTruncation)]
    [InlineData(TextTrimming.HeadTruncation)]
    [InlineData(TextTrimming.MiddleTruncation)]
    public async Task Text_Auto_SingleLine_BoxFitsAscentAndDescent(TextTrimming trimming)
    {
        var text = NarrowLineHeightText(trimming);

        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), Ctx, CancellationToken.None);

        Assert.Equal(FontExtent(text), node.Bounds.Height, 3);
    }

    [Fact]
    public async Task Text_Auto_MultiLine_AddsExtentToTheLastLineOnly()
    {
        // n lines advance by LineHeight; only the last line's descenders need the extra room, so the
        // block is (n-1) advances plus one full font extent - not n full extents.
        var text = NarrowLineHeightText(TextTrimming.WordWrap);
        text.Content = "gjpqy gjpqy gjpqy";

        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 40, 500)), Ctx, CancellationToken.None);

        var lineHeight = text.Font.Size * text.LineHeight;
        var lines = (int)MathF.Round((node.Bounds.Height - FontExtent(text)) / lineHeight) + 1;

        Assert.True(lines >= 3, $"expected the content to wrap, got {lines} line(s)");
        Assert.Equal(FontExtent(text) + (lines - 1) * lineHeight, node.Bounds.Height, 3);
    }

    [Fact]
    public async Task Text_Auto_LineHeightAboveFontExtent_KeepsLineBoxHeight()
    {
        // The common case: a line height that already clears the font's extent is left untouched, so
        // existing layouts keep measuring exactly Font.Size * LineHeight.
        var text = new Text
        {
            Content = "gjpqy",
            Font = new Font("Arial", 20),
            LineHeight = 2f,
            TextTrimming = TextTrimming.None,
            Size = Size.Auto
        };

        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), Ctx, CancellationToken.None);

        Assert.Equal(40f, node.Bounds.Height, 3);
    }
}
