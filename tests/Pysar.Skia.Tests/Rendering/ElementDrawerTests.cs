using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class ElementDrawerTests
{
    [Fact]
    public async Task Draw_FrameWithBackground_PaintsBounds()
    {
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        var frame = new Frame { Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)), BackgroundColor = Colors.Red };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(20, 20, 200, 200)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 50));
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(150, 150));
    }

    [Fact]
    public async Task Draw_NestedChildBackground_PaintsChild()
    {
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        var child = new Frame { Size = new Size(SizeLength.Fixed(40), SizeLength.Fixed(40)), BackgroundColor = Colors.Blue };
        var frame = new Frame { Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)) };
        frame.AddElement(child);
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 200, 200)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        Assert.Equal(SKColors.Blue, bitmap.GetPixel(10, 10));
    }

    [Fact]
    public async Task Draw_ContainerWithPositiveBottomMargin_DoesNotClipChildBottom()
    {
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        var child = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill), BackgroundColor = Colors.Red };
        var parent = new Frame { Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)), Margin = new Thickness(0, 0, 0, 30) };
        parent.AddElement(child);
        var node = await LayoutEngine.MeasureAsync(parent,
            new MeasureConstraint(new Rect(0, 0, 200, 200)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        // The parent box is [0,100]; its bottom margin (30) is outside the box, so the child must fill the
        // whole box down to y=100 — a clip that re-insets the box by the margin would cut it off at y=70.
        Assert.Equal(SKColors.Red, bitmap.GetPixel(50, 90));
    }
}
