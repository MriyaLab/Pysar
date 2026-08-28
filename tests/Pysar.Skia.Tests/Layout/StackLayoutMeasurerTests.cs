using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class StackLayoutMeasurerTests
{
    private static readonly MeasureContext Ctx = new(scale: 1f);

    [Fact]
    public async Task Stack_ChildrenStackVertically_WithRowCutHints()
    {
        var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        for (int i = 0; i < 3; i++)
            stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 200, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(0, node.Children[0].Bounds.Top);
        Assert.Equal(100, node.Children[0].Bounds.Bottom);
        Assert.Equal(100, node.Children[1].Bounds.Top);
        Assert.Equal(200, node.Children[1].Bounds.Bottom);
        Assert.Equal(200, node.Children[2].Bounds.Top);
        Assert.Equal(300, node.Children[2].Bounds.Bottom);
        Assert.Equal(300, node.Bounds.Height);                       // Auto height = sum of rows
        Assert.Equal(new[] { 100f, 200f, 300f }, node.CutHints);     // one hint per row bottom
        Assert.Equal(200, node.Children[0].Bounds.Width);            // Fill width = available
    }

    [Fact]
    public async Task VerticalStack_Spacing_InsertsGapBetweenChildren()
    {
        var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto), Spacing = 10 };
        for (int i = 0; i < 3; i++)
            stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 200, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(0, node.Children[0].Bounds.Top);
        Assert.Equal(110, node.Children[1].Bounds.Top);   // 100 + 10
        Assert.Equal(220, node.Children[2].Bounds.Top);   // 100 + 10 + 100 + 10
        Assert.Equal(320, node.Bounds.Height);            // 3*100 + 2*10
        Assert.Equal(new[] { 100f, 210f, 320f }, node.CutHints);
    }

    [Fact]
    public async Task HorizontalStack_Spacing_InsertsGapAndShrinksFillShare()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)),
            Orientation = StackOrientation.Horizontal,
            Spacing = 20
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(50)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        // gaps: 2*20 = 40; remaining for two fills: 400 - 100 - 40 = 260 → 130 each
        Assert.Equal(0, node.Children[0].Bounds.Left);
        Assert.Equal(120, node.Children[1].Bounds.Left);   // 100 + 20
        Assert.Equal(250, node.Children[1].Bounds.Right);  // 120 + 130
        Assert.Equal(270, node.Children[2].Bounds.Left);   // 250 + 20
        Assert.Equal(400, node.Children[2].Bounds.Right);
    }

    [Fact]
    public async Task VerticalStack_PanelMargin_InsetsBorderBox()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)),
            Margin = new Thickness(10)
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 200, 200)), Ctx, CancellationToken.None);

        Assert.Equal(new Rect(10, 10, 190, 110), node.Bounds);
        Assert.Equal(new Rect(10, 10, 190, 60), node.Children[0].Bounds);
    }

    [Fact]
    public async Task VerticalStack_FillChild_NegativeHorizontalMargin_ExpandsBeyondContent()
    {
        var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        stack.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(40)),
            Margin = new Thickness(-20, 0)
        });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 200, 500)), Ctx, CancellationToken.None);

        Assert.Equal(new Rect(-20, 0, 220, 40), node.Children[0].Bounds);
    }

    [Fact]
    public async Task VerticalStack_FillChild_PositiveMargin_InsetsWithinContent()
    {
        var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        stack.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(40)),
            Margin = new Thickness(10)
        });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 200, 500)), Ctx, CancellationToken.None);

        Assert.Equal(new Rect(10, 10, 190, 50), node.Children[0].Bounds);
    }

    [Fact]
    public async Task HorizontalStack_FillChild_NegativeVerticalMargin_ExpandsBeyondContent()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(80)),
            Orientation = StackOrientation.Horizontal
        };
        stack.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            Margin = new Thickness(0, -10)
        });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 200, 500)), Ctx, CancellationToken.None);

        Assert.Equal(new Rect(0, -10, 200, 90), node.Children[0].Bounds);
    }

    [Fact]
    public async Task HorizontalStack_ChildrenStackLeftToRight_WithSingleRowCutHint()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Orientation = StackOrientation.Horizontal
        };
        for (int i = 0; i < 3; i++)
            stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(0, node.Children[0].Bounds.Left);
        Assert.Equal(50, node.Children[0].Bounds.Right);
        Assert.Equal(50, node.Children[1].Bounds.Left);
        Assert.Equal(100, node.Children[1].Bounds.Right);
        Assert.Equal(100, node.Children[2].Bounds.Left);
        Assert.Equal(150, node.Children[2].Bounds.Right);
        Assert.Equal(400, node.Bounds.Width);                    // Fill width = available
        Assert.Equal(100, node.Bounds.Height);                   // Auto height = max child bottom
        Assert.Equal(new[] { 100f }, node.CutHints);             // one hint: the row's bottom edge
    }

    [Fact]
    public async Task HorizontalStack_AutoWidth_SumsChildrenWidths()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Auto, SizeLength.Auto),
            Orientation = StackOrientation.Horizontal
        };
        for (int i = 0; i < 3; i++)
            stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(150, node.Bounds.Width);                    // Auto width = sum of children
        Assert.Equal(100, node.Bounds.Height);
    }

    [Fact]
    public async Task HorizontalStack_InvisibleChild_IsSkipped()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Orientation = StackOrientation.Horizontal
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(100)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(100)), IsVisible = false });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(2, node.Children.Count);
        Assert.Equal(0, node.Children[0].Bounds.Left);
        Assert.Equal(50, node.Children[1].Bounds.Left);
    }

    [Fact]
    public async Task HorizontalStack_Padding_OffsetsContentAndInflatesPanel()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Orientation = StackOrientation.Horizontal,
            Padding = new Thickness(10)
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(10, node.Children[0].Bounds.Left);
        Assert.Equal(10, node.Children[0].Bounds.Top);
        Assert.Equal(120, node.Bounds.Height);                   // 100 + top/bottom padding
        Assert.Equal(new[] { 110f }, node.CutHints);             // child bottom = 10 + 100
    }

    [Fact]
    public async Task HorizontalStack_AutoHeight_UsesMaxChildBottom()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Orientation = StackOrientation.Horizontal
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(100)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(80)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(100, node.Bounds.Height);          // max child bottom, not the last child's
        Assert.Equal(new[] { 100f }, node.CutHints);
    }

    [Fact]
    public async Task HorizontalStack_FillChildren_ShareRemainingWidthEqually()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)),
            Orientation = StackOrientation.Horizontal
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        // (400 - 100 fixed) / 2 fills = 150 each.
        Assert.Equal(100, node.Children[1].Bounds.Left);
        Assert.Equal(250, node.Children[1].Bounds.Right);
        Assert.Equal(250, node.Children[2].Bounds.Left);
        Assert.Equal(400, node.Children[2].Bounds.Right);
    }

    [Fact]
    public async Task HorizontalStack_FillHeight_PinsToContentHeight()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(80)),
            Orientation = StackOrientation.Horizontal
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fill) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        Assert.Equal(0, node.Children[0].Bounds.Top);
        Assert.Equal(80, node.Children[0].Bounds.Bottom);
    }

    [Fact]
    public async Task HorizontalStack_AutoChild_ReportsContentWidth_BeforeFillShare()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)),
            Orientation = StackOrientation.Horizontal
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)) });

        // Auto-width child: a vertical StackPanel whose width is its widest child (60).
        var autoChild = new StackPanel { Size = new Size(SizeLength.Auto, SizeLength.Fixed(100)) };
        autoChild.AddElement(new Frame { Size = new Size(SizeLength.Fixed(60), SizeLength.Fixed(50)) });
        stack.AddElement(autoChild);

        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        // Auto child reports its content width (60); the Fill child takes the rest: 400 - 100 - 60 = 240.
        Assert.Equal(100, node.Children[1].Bounds.Left);
        Assert.Equal(160, node.Children[1].Bounds.Right);
        Assert.Equal(160, node.Children[2].Bounds.Left);
        Assert.Equal(400, node.Children[2].Bounds.Right);
    }

    [Fact]
    public async Task HorizontalStack_FillChildren_ClampToZero_WhenFixedChildrenOverflow()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)),
            Orientation = StackOrientation.Horizontal
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(300), SizeLength.Fixed(100)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(100)) });
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        // 300 + 200 = 500 > 400 → Fill share clamps to 0.
        Assert.Equal(500, node.Children[2].Bounds.Left);
        Assert.Equal(500, node.Children[2].Bounds.Right);
    }

    [Fact]
    public async Task VerticalStack_AutoWidth_CenterHorizontalAlignment_PositionsCorrectly()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Auto, SizeLength.Fixed(100)),
            HorizontalAlignment = Alignment.Center
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(60), SizeLength.Fixed(50)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 1000)), Ctx, CancellationToken.None);

        // Auto width = 60 (child width). Center alignment in 400px space: origin at (400-60)/2 = 170
        Assert.Equal(170, node.Bounds.Left);
        Assert.Equal(230, node.Bounds.Right);
        Assert.Equal(60, node.Bounds.Width);
    }

    [Fact]
    public async Task VerticalStack_FixedSize_EndVerticalAlignment_PositionsCorrectly()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)),
            VerticalAlignment = Alignment.End
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 200)), Ctx, CancellationToken.None);

        // End alignment in 200px space: top = 200 - 50 = 150
        Assert.Equal(150, node.Bounds.Top);
        Assert.Equal(200, node.Bounds.Bottom);
    }

    [Fact]
    public async Task HorizontalStack_AutoHeight_CenterVerticalAlignment_PositionsCorrectly()
    {
        var stack = new StackPanel
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Orientation = StackOrientation.Horizontal,
            VerticalAlignment = Alignment.Center
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(60)) });

        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 400, 200)), Ctx, CancellationToken.None);

        // Auto height = 60 (child height). Center alignment in 200px space: top = (200-60)/2 = 70
        Assert.Equal(70, node.Bounds.Top);
        Assert.Equal(130, node.Bounds.Bottom);
        Assert.Equal(60, node.Bounds.Height);
    }

    [Fact]
    public async Task Stack_MinWidth_FloorsOuterBox()
    {
        var stack = new StackPanel
        {
            Orientation = StackOrientation.Vertical,
            Size = new Size(SizeLength.Auto, SizeLength.Auto),
            MinWidth = 100
        };
        stack.AddElement(new Frame { Size = new Size(SizeLength.Fixed(20), SizeLength.Fixed(20)) });
        var node = await LayoutEngine.MeasureAsync(stack,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), new MeasureContext(1f), CancellationToken.None);
        Assert.Equal(100, node.Bounds.Width);
    }
}
