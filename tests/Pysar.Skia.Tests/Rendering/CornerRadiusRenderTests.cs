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

    [Fact]
    public async Task Draw_RoundedBorder_ClipsChildToTheInnerArc()
    {
        // The child fills the content box (bounds deflated by the border), so its square corner lands
        // on top of the border's arc unless the clip follows CornerRadius on the inner box.
        var child = new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            BackgroundColor = Colors.Blue
        };
        var parent = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            BorderThickness = new Thickness(10),
            BorderColor = Colors.Black,
            CornerRadius = new CornerRadius(20)
        };
        parent.AddElement(child);

        var bitmap = await RenderAsync(parent);

        // Inner box is 10..90 with a radius of 20, so its arc centre is (30,30): (12,12) is outside it.
        Assert.NotEqual(SKColors.Blue, bitmap.GetPixel(12, 12));
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public async Task Draw_BorderThickerThanRadius_KeepsTheAuthoredRadiusOnTheContent()
    {
        // The whole point of the model: CornerRadius names the content corner, so a border thicker
        // than the radius no longer eats the rounding away - it only pushes the box corner out.
        var child = new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            BackgroundColor = Colors.Blue
        };
        var parent = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            BorderThickness = new Thickness(20),
            BorderColor = Colors.Black,
            CornerRadius = new CornerRadius(8)
        };
        parent.AddElement(child);

        var bitmap = await RenderAsync(parent);

        // Inner box is 20..80 with a radius of 8, so its arc centre is (28,28).
        Assert.NotEqual(SKColors.Blue, bitmap.GetPixel(21, 21));  // outside the arc
        Assert.Equal(SKColors.Blue, bitmap.GetPixel(25, 25));     // inside it
    }

    /// <summary>
    ///     200pt box, 30pt border, radius 60: the content arc keeps radius 60 on the 30..170 box
    ///     (centre 90,90 — crossing the diagonal at 90 - 60/√2 ≈ 47.6) and the box arc grows to
    ///     60 + 30 = 90 (centre 90,90 — crossing at 90 - 90/√2 ≈ 26.4, shared with the background).
    /// </summary>
    private static Frame ThickRoundedBorderFrame() => new()
    {
        Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(200)),
        BackgroundColor = Colors.Red,
        BorderThickness = new Thickness(30),
        BorderColor = Colors.Black,
        CornerRadius = new CornerRadius(60)
    };

    [Fact]
    public async Task Draw_RoundedBorder_OuterEdgeFollowsTheBackgroundArc()
    {
        var bitmap = await RenderAsync(ThickRoundedBorderFrame(), 260);

        // Nothing but border may be painted before the content arc - a background pixel there means
        // the border's outer edge is rounder than the box and the fill leaks past it in the corner.
        Assert.DoesNotContain(
            Enumerable.Range(0, 46).Select(i => bitmap.GetPixel(i, i)),
            pixel => pixel == SKColors.Red);
    }

    [Fact]
    public async Task Draw_RoundedBorder_InnerEdgeMatchesTheContentClip()
    {
        var bitmap = await RenderAsync(ThickRoundedBorderFrame(), 260);

        Assert.Equal(SKColors.Black, bitmap.GetPixel(45, 45));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(51, 51));
    }

    [Fact]
    public async Task Draw_RoundedBorder_BoxCornerIsRadiusPlusThickness()
    {
        // 200pt box, 40pt border, radius 20: the box corner is 20 + 40 = 60, centred at (60,60), so it
        // crosses the diagonal at 60 - 60/√2 ≈ 17.6.
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(200)),
            BorderThickness = new Thickness(40),
            BorderColor = Colors.Black,
            CornerRadius = new CornerRadius(20)
        };

        var bitmap = await RenderAsync(frame, 260);

        Assert.Equal(SKColors.Empty, bitmap.GetPixel(12, 12));  // outside the 60pt box arc
        Assert.Equal(SKColors.Black, bitmap.GetPixel(24, 24));  // inside it, still inside the ring
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

    private static async Task<SKBitmap> RenderAsync(Frame frame, int size = 200)
    {
        var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, size, size)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        return bitmap;
    }
}
