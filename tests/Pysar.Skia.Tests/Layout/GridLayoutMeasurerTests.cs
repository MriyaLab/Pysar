using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class GridLayoutMeasurerTests
{
    private static readonly MeasureContext Ctx = new(scale: 1f);

    private static Task<LayoutNode> Measure(Grid grid, Rect available) =>
        LayoutEngine.MeasureAsync(grid, new MeasureConstraint(available), Ctx, CancellationToken.None);

    [Fact]
    public async Task GridWithPadding_InsetsContentOnAllSides()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(300), SizeLength.Fixed(200)), Padding = new Thickness(20) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(new RowDefinition(GridLength.Star(1)));
        var child = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill) };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(300, node.Bounds.Width);
        Assert.Equal(200, node.Bounds.Height);
        Assert.Equal(new Rect(20, 20, 280, 180), node.Children[0].Bounds);
    }

    [Fact]
    public async Task FillChild_NegativeHorizontalMargin_ExpandsBeyondCell()
    {
        // Fill must resolve against the margin-adjusted cell, not a Fixed(cellWidth) pin that
        // keeps the pre-margin width and only shifts left (leaving a gap on the right).
        var grid = new Grid { Size = new Size(SizeLength.Fixed(400), SizeLength.Fixed(150)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(new RowDefinition(GridLength.Fixed(150)));
        var child = new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            Margin = new Thickness(-50, 0)
        };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(new Rect(-50, 0, 450, 150), node.Children[0].Bounds);
    }

    [Fact]
    public async Task FillChild_PositiveMargin_InsetsWithinCell()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(400), SizeLength.Fixed(150)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(new RowDefinition(GridLength.Fixed(150)));
        var child = new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            Margin = new Thickness(10)
        };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(new Rect(10, 10, 390, 140), node.Children[0].Bounds);
    }

    [Fact]
    public async Task NestedGridChildren_ArePositionedInTheirRows()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(100)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(
            new RowDefinition(GridLength.Fixed(60)),
            new RowDefinition(GridLength.Fixed(40)));
        var cell0 = new Grid { Size = new Size(SizeLength.Fill, SizeLength.Fill) };
        var cell1 = new Grid { Size = new Size(SizeLength.Fill, SizeLength.Fill) };
        grid.AddElement(cell0, 0, 0);
        grid.AddElement(cell1, 1, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(0, node.Children[0].Bounds.Top);
        Assert.Equal(60, node.Children[0].Bounds.Bottom);
        Assert.Equal(60, node.Children[1].Bounds.Top);
        Assert.Equal(100, node.Children[1].Bounds.Bottom);
    }

    [Fact]
    public async Task ChildWithExplicitPosition_IsOffsetWithinItsCell()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(400), SizeLength.Fixed(400)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(
            new RowDefinition(GridLength.Fixed(100)),
            new RowDefinition(GridLength.Fixed(300)));
        var child = new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)) }.At(30, 40);
        grid.AddElement(child, 1, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(new Rect(30, 140, 80, 190), node.Children[0].Bounds);
    }

    [Fact]
    public async Task ChildWithAlignment_IsAlignedWithinItsCell()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(400), SizeLength.Fixed(400)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(new RowDefinition(GridLength.Fixed(400)));
        var child = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            HorizontalAlignment = Alignment.Center,
            VerticalAlignment = Alignment.End
        };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(new Rect(150, 300, 250, 400), node.Children[0].Bounds);
    }

    [Fact]
    public async Task FixedGrid_EmitsRowBottomsAsCutHints()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(100)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(
            new RowDefinition(GridLength.Fixed(60)),
            new RowDefinition(GridLength.Fixed(40)));

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(new[] { 60f, 100f }, node.CutHints);
    }

    [Fact]
    public async Task AutoSizedGridWithStarColumns_StarBehavesLikeAuto()
    {
        var grid = new Grid
        {
            Size = Size.Auto,
            ColumnSpacing = 10,
            RowSpacing = 10
        };
        grid.WithColumnDefinitions(
            new ColumnDefinition(GridLength.Fixed(100)),
            new ColumnDefinition(GridLength.Star(1)),
            new ColumnDefinition(GridLength.Auto));
        grid.WithRowDefinitions(
            new RowDefinition(GridLength.Auto),
            new RowDefinition(GridLength.Auto));
        var c0 = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill) }
            .AddElement(new Text { Content = "Cell 0,0", Font = new Font { Size = 10 } });
        var c1 = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill) }
            .AddElement(new Text { Content = "Star", Font = new Font { Size = 10 } });
        var c2 = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill) }
            .AddElement(new Text { Content = "Auto", Font = new Font { Size = 10 } });
        grid.AddElement(c0, 0, 0);
        grid.AddElement(c1, 0, 1);
        grid.AddElement(c2, 0, 2);

        var node = await Measure(grid, new Rect(0, 0, 1000, 1000));

        Assert.True(node.Bounds.Width > 0 && node.Bounds.Width < 500, $"grid width {node.Bounds.Width}");
        Assert.All(node.Children, n => Assert.True(n.Bounds.Width > 0 && n.Bounds.Height > 0));
        var starAutoRatio = node.Children[1].Bounds.Width / Math.Max(node.Children[2].Bounds.Width, 1);
        Assert.True(starAutoRatio < 5, $"star behaves like auto, ratio {starAutoRatio}");
    }

    [Fact]
    public async Task EmptyRowDefinitions_FixedHeight_GivesChildTheGridContentHeight()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(24)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        var child = new Frame { Size = Size.Fill };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(new Rect(0, 0, 200, 24), node.Children[0].Bounds);
        Assert.Empty(grid.RowDefinitions);
    }

    [Fact]
    public async Task EmptyRowDefinitions_FillHeight_GivesChildTheAvailableHeight()
    {
        var grid = new Grid();
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        var child = new Frame { Size = Size.Fill };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 300, 200));

        Assert.Equal(200, node.Children[0].Bounds.Height);
        Assert.Empty(grid.RowDefinitions);
    }

    [Fact]
    public async Task EmptyRowDefinitions_AutoHeight_SizesRowToTextContent()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(200), SizeLength.Auto) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        var text = new Text { Content = "Hello", Font = new Font { Size = 10 } };
        grid.AddElement(text, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.True(node.Children[0].Bounds.Height > 0);
        Assert.Equal(node.Children[0].Bounds.Height, node.Bounds.Height);
        Assert.Empty(grid.RowDefinitions);
    }

    [Fact]
    public async Task EmptyColumnDefinitions_FixedWidth_GivesChildTheGridContentWidth()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(180), SizeLength.Fixed(40)) };
        grid.WithRowDefinitions(new RowDefinition(GridLength.Star(1)));
        var child = new Frame { Size = Size.Fill };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(new Rect(0, 0, 180, 40), node.Children[0].Bounds);
        Assert.Empty(grid.ColumnDefinitions);
    }

    [Fact]
    public async Task EmptyColumnDefinitions_FillWidth_GivesChildTheAvailableWidth()
    {
        var grid = new Grid();
        grid.WithRowDefinitions(new RowDefinition(GridLength.Star(1)));
        var child = new Frame { Size = Size.Fill };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 300, 200));

        Assert.Equal(300, node.Children[0].Bounds.Width);
        Assert.Empty(grid.ColumnDefinitions);
    }

    [Fact]
    public async Task EmptyColumnDefinitions_AutoWidth_SizesColumnToTextContent()
    {
        var grid = new Grid { Size = new Size(SizeLength.Auto, SizeLength.Fixed(40)) };
        grid.WithRowDefinitions(new RowDefinition(GridLength.Star(1)));
        var text = new Text { Content = "Hello", Font = new Font { Size = 10 } };
        grid.AddElement(text, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.True(node.Children[0].Bounds.Width > 0);
        Assert.Equal(node.Children[0].Bounds.Width, node.Bounds.Width);
        Assert.Empty(grid.ColumnDefinitions);
    }

    [Fact]
    public async Task EmptyRowDefinitions_ChildOnRow1_HasZeroHeight()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(24)) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        var onRow0 = new Frame { Size = Size.Fill };
        var onRow1 = new Frame { Size = Size.Fill };
        grid.AddElement(onRow0, 0, 0);
        grid.AddElement(onRow1, 1, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(24, node.Children[0].Bounds.Height);
        Assert.Equal(0, node.Children[1].Bounds.Height);
    }

    [Fact]
    public async Task Grid_MaxHeight_ClampsOuterBox()
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(100)),
            MaxHeight = 40
        };
        grid.WithRowDefinitions(new RowDefinition(GridLength.Fixed(100)));
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star()));
        var node = await LayoutEngine.MeasureAsync(grid,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), new MeasureContext(1f), CancellationToken.None);
        Assert.Equal(40, node.Bounds.Height);
    }

    [Fact]
    public async Task Grid_Auto_MinHeight_ExpandsRowSoChildCanCenter()
    {
        // Regression: MinHeight used to grow only the outer box; row tracks stayed content-sized,
        // so VerticalAlignment=Center had no free space inside the cell.
        var grid = new Grid
        {
            Size = new Size(SizeLength.Fixed(200), SizeLength.Auto),
            MinHeight = 40
        };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star()));
        var child = new Frame
        {
            Size = new Size(SizeLength.Fixed(20), SizeLength.Fixed(10)),
            VerticalAlignment = Alignment.Center
        };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(40, node.Bounds.Height);
        Assert.Equal(15, node.Children[0].Bounds.Top);
        Assert.Equal(25, node.Children[0].Bounds.Bottom);
    }

    [Fact]
    public async Task Grid_Auto_MinWidth_ExpandsColumnSoChildCanAlignEnd()
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Auto, SizeLength.Fixed(30)),
            MinWidth = 100
        };
        grid.WithRowDefinitions(new RowDefinition(GridLength.Star()));
        var child = new Frame
        {
            Size = new Size(SizeLength.Fixed(20), SizeLength.Fixed(10)),
            HorizontalAlignment = Alignment.End
        };
        grid.AddElement(child, 0, 0);

        var node = await Measure(grid, new Rect(0, 0, 500, 500));

        Assert.Equal(100, node.Bounds.Width);
        Assert.Equal(80, node.Children[0].Bounds.Left);
        Assert.Equal(100, node.Children[0].Bounds.Right);
    }
}
