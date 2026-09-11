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

    [Fact]
    public void Opacity_DefaultsToOne_RotationDefaultsToZero()
    {
        var frame = new Frame();
        Assert.Equal(1f, frame.Opacity);
        Assert.Equal(0f, frame.Rotation);
    }

    [Fact]
    public void WithOpacityAndRotation_SetProperties()
    {
        var frame = new Frame().WithOpacity(0.4f).WithRotation(45f);
        Assert.Equal(0.4f, frame.Opacity);
        Assert.Equal(45f, frame.Rotation);
    }

    [Fact]
    public async Task Draw_OpacityHalf_BlendsFillWithWhite()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(80), SizeLength.Fixed(80)),
            BackgroundColor = Colors.Red,
            Opacity = 0.5f
        };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 100, 100)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        var pixel = bitmap.GetPixel(20, 20);
        Assert.NotEqual(SKColors.Red, pixel);
        Assert.NotEqual(SKColors.White, pixel);
        Assert.Equal(255, pixel.Red);
        Assert.InRange(pixel.Green, 100, 160);
        Assert.InRange(pixel.Blue, 100, 160);
    }

    [Fact]
    public async Task Draw_ParentAndChildOpacity_Multiply()
    {
        using var halfBitmap = new SKBitmap(100, 100);
        using var nestedBitmap = new SKBitmap(100, 100);
        using var halfCanvas = new SKCanvas(halfBitmap);
        using var nestedCanvas = new SKCanvas(nestedBitmap);
        halfCanvas.Clear(SKColors.White);
        nestedCanvas.Clear(SKColors.White);

        var half = new Frame
        {
            Size = new Size(SizeLength.Fixed(80), SizeLength.Fixed(80)),
            BackgroundColor = Colors.Red,
            Opacity = 0.5f
        };
        var child = new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            BackgroundColor = Colors.Red,
            Opacity = 0.5f
        };
        var parent = new Frame
        {
            Size = new Size(SizeLength.Fixed(80), SizeLength.Fixed(80)),
            Opacity = 0.5f
        };
        parent.AddElement(child);

        var constraint = new MeasureConstraint(new Rect(0, 0, 100, 100));
        var ctx = new MeasureContext(1f);
        var halfNode = await LayoutEngine.MeasureAsync(half, constraint, ctx, CancellationToken.None);
        var nestedNode = await LayoutEngine.MeasureAsync(parent, constraint, ctx, CancellationToken.None);

        ElementDrawer.Draw(halfNode, new RenderContext(halfCanvas, 1f));
        ElementDrawer.Draw(nestedNode, new RenderContext(nestedCanvas, 1f));
        halfCanvas.Flush();
        nestedCanvas.Flush();

        var halfPixel = halfBitmap.GetPixel(20, 20);
        var nestedPixel = nestedBitmap.GetPixel(20, 20);
        Assert.Equal(255, nestedPixel.Red);
        Assert.InRange(nestedPixel.Green, halfPixel.Green + 1, 230);
        Assert.InRange(nestedPixel.Blue, halfPixel.Blue + 1, 230);
    }

    [Fact]
    public async Task Measure_OpacityZero_KeepsAuthoredSize()
    {
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(40)),
            Opacity = 0f
        };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 200, 200)), new MeasureContext(1f), CancellationToken.None);

        Assert.Equal(100f, node.Bounds.Width);
        Assert.Equal(40f, node.Bounds.Height);
    }

    [Fact]
    public async Task Draw_OpacityAboveOne_PaintsFullyOpaque()
    {
        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(80), SizeLength.Fixed(80)),
            BackgroundColor = Colors.Red,
            Opacity = 2f
        };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 100, 100)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        Assert.Equal(SKColors.Red, bitmap.GetPixel(20, 20));
    }

    [Fact]
    public async Task Draw_Rotation90_MovesFillAroundBoundsCenter()
    {
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(80), SizeLength.Fixed(20)),
            BackgroundColor = Colors.Red,
            Rotation = 90f
        };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(20, 40, 200, 200)), new MeasureContext(1f), CancellationToken.None);

        ElementDrawer.Draw(node, new RenderContext(canvas, 1f));
        canvas.Flush();

        Assert.Equal(80f, node.Bounds.Width);
        Assert.Equal(20f, node.Bounds.Height);
        Assert.NotEqual(SKColors.Red, bitmap.GetPixel(40, 50));
        Assert.Equal(SKColors.Red, bitmap.GetPixel(60, 30));
    }

    [Fact]
    public async Task Draw_RotatedElement_NotCulledByUnrotatedBounds()
    {
        using var bitmap = new SKBitmap(200, 200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(80), SizeLength.Fixed(20)),
            BackgroundColor = Colors.Red,
            Rotation = 90f
        };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(20, 40, 200, 200)), new MeasureContext(1f), CancellationToken.None);
        var ctx = new RenderContext(canvas, 1f)
        {
            CullBoundsPt = new SKRect(50, 10, 70, 25)
        };

        ElementDrawer.Draw(node, ctx);
        canvas.Flush();

        Assert.Equal(SKColors.Red, bitmap.GetPixel(60, 20));
    }
}
