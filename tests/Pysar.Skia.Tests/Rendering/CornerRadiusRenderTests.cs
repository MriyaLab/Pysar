using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

/// <summary>
///     Rounded frames: background, border, and the clip children are subject to. Pixels are sampled
///     just inside the box corner — the area a radius cuts away — and at the centre, which every
///     variant must keep painted.
/// </summary>
public class CornerRadiusRenderTests
{
    [Fact]
    public async Task Draw_RoundedBackground_LeavesBoxCornerUnpainted()
    {
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            BackgroundColor = Colors.Red,
            CornerRadius = new CornerRadius(20)
        };

        var bitmap = await RenderAsync(frame);

        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(2, 2));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public async Task Draw_PerCornerRadius_RoundsOnlyTheGivenCorners()
    {
        // Top-left rounded, top-right square.
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            BackgroundColor = Colors.Red,
            CornerRadius = new CornerRadius(20, 0, 0, 0)
        };

        var bitmap = await RenderAsync(frame);

        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(2, 2));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(97, 2));
    }

    [Fact]
    public async Task Draw_RoundedBorder_PaintsArcNotBoxCorner()
    {
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            BorderThickness = new Thickness(2),
            BorderColor = Colors.Black,
            CornerRadius = new CornerRadius(20)
        };

        var bitmap = await RenderAsync(frame);

        Assert.Equal(SKColors.Empty, bitmap.GetPixel(1, 1));
        // The arc crosses the diagonal at roughly (20 - 20/√2) ≈ 6px from each edge.
        Assert.Contains(
            Enumerable.Range(4, 6).Select(i => bitmap.GetPixel(i, i)),
            pixel => pixel.Alpha > 0);
    }

    [Fact]
    public async Task Draw_OversizedRadius_ClampsToCapsule()
    {
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            BackgroundColor = Colors.Red,
            CornerRadius = new CornerRadius(500)
        };

        var bitmap = await RenderAsync(frame);

        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(2, 2));
    }

    [Fact]
    public async Task Draw_ClippedRoundedParent_CutsChildOnTheArc()
    {
        var parent = RoundedParentWithFillingChild(clipped: true);

        var bitmap = await RenderAsync(parent);

        Assert.NotEqual(SKColors.Blue, bitmap.GetPixel(2, 2));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public async Task Draw_UnclippedRoundedParent_LeavesChildSquare()
    {
        var parent = RoundedParentWithFillingChild(clipped: false);

        var bitmap = await RenderAsync(parent);

        // The child spills into the corner the parent's own background leaves rounded.
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(2, 2));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public async Task Draw_ZeroRadiusParent_ClipsExactlyAsBefore()
    {
        var child = new Frame
        {
            Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(200)),
            BackgroundColor = Colors.Blue
        };
        var parent = new Frame { Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)) };
        parent.AddElement(child);

        var bitmap = await RenderAsync(parent);

        Assert.Equal(SKColors.Blue, bitmap.GetPixel(0, 0));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(99, 99));
        Assert.Equal(SKColors.Empty, bitmap.GetPixel(100, 100));
    }

    private static Frame RoundedParentWithFillingChild(bool clipped)
    {
        var child = new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            BackgroundColor = Colors.Blue
        };
        var parent = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            BackgroundColor = Colors.Red,
            CornerRadius = new CornerRadius(20),
            IsClippedToBounds = clipped
        };
        parent.AddElement(child);
        return parent;
    }

    private static async Task<SKBitmap> RenderAsync(Frame frame)
    {
        var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 200, 200)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        return bitmap;
    }
}
