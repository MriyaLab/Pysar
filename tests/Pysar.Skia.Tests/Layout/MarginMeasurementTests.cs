using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class MarginMeasurementTests
{
    private static readonly MeasureContext Ctx = new(scale: 1f);

    private static Task<LayoutNode> Measure(Frame frame, Rect available) =>
        LayoutEngine.MeasureAsync(frame, new MeasureConstraint(available), Ctx, CancellationToken.None);

    [Fact]
    public async Task Container_PositiveMargin_InsetsBox()
    {
        var frame = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill), Margin = new Thickness(10) };
        var node = await Measure(frame, new Rect(0, 0, 100, 100));
        Assert.Equal(new Rect(10, 10, 90, 90), node.Bounds);
    }

    [Fact]
    public async Task Container_NegativeMargin_ExpandsBoxOutward()
    {
        var frame = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill), Margin = new Thickness(-10) };
        var node = await Measure(frame, new Rect(0, 0, 100, 100));
        Assert.Equal(new Rect(-10, -10, 110, 110), node.Bounds);
    }

    [Fact]
    public async Task Container_HorizontalNegativeMargin_WidensBox()
    {
        // Margin(-30, 0) → left/right = -30, top/bottom = 0 (the full-bleed banner case).
        var frame = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)), Margin = new Thickness(-30, 0) };
        var node = await Measure(frame, new Rect(30, 0, 565, 400)); // content zone with 30pt side margins
        Assert.Equal(0, node.Bounds.Left);      // 30 + (-30)
        Assert.Equal(595, node.Bounds.Right);   // 565 - (-30)
        Assert.Equal(0, node.Bounds.Top);
        Assert.Equal(100, node.Bounds.Bottom);
    }

    [Fact]
    public async Task Text_PositiveMargin_InsetsBoxLikeFrame()
    {
        var text = new Text
        {
            Content = "Hi",
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(20)),
            Margin = new Thickness(10)
        };

        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 200, 100)), Ctx, CancellationToken.None);

        Assert.Equal(new Rect(10, 10, 190, 30), node.Bounds);
    }

    [Fact]
    public async Task Text_TopMargin_ShiftsBoxDown()
    {
        var text = new Text
        {
            Content = "Hi",
            Size = new Size(SizeLength.Auto, SizeLength.Auto),
            Margin = new Thickness(0, 40, 0, 0),
            Font = new Font { Size = 14 }
        };

        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 200, 200)), Ctx, CancellationToken.None);

        Assert.Equal(40, node.Bounds.Top);
        Assert.True(node.Bounds.Height > 0);
    }

    [Fact]
    public async Task VerticalStack_TextTopMargin_CreatesGapAbove()
    {
        var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        stack.AddElement(new Text
        {
            Content = "A",
            Size = new Size(SizeLength.Auto, SizeLength.Fixed(20)),
            BackgroundColor = Colors.Red
        });
        stack.AddElement(new Text
        {
            Content = "B",
            Size = new Size(SizeLength.Auto, SizeLength.Fixed(20)),
            Margin = new Thickness(0, 40, 0, 0),
            BackgroundColor = Colors.Blue
        });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 200, 500)), Ctx, CancellationToken.None);

        Assert.Equal(0, node.Children[0].Bounds.Top);
        Assert.Equal(20, node.Children[0].Bounds.Bottom);
        Assert.Equal(60, node.Children[1].Bounds.Top); // 20 + top margin 40
    }
}
