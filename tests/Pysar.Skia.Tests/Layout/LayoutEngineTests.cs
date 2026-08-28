using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class LayoutEngineTests
{
    private static readonly MeasureContext Ctx = new(scale: 1f);

    [Fact]
    public async Task Frame_FixedSize_YieldsFixedBounds()
    {
        var frame = new Frame { Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(100)) };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), Ctx, CancellationToken.None);
        Assert.Equal(new Rect(0, 0, 200, 100), node.Bounds);
    }

    [Fact]
    public async Task Frame_MeasureTwice_IdenticalAndModelUntouched()
    {
        var child = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill) };
        var frame = new Frame { Size = new Size(SizeLength.Fixed(300), SizeLength.Fixed(200)), Padding = new Thickness(20) };
        frame.AddElement(child);
        var c = new MeasureConstraint(new Rect(0, 0, 500, 500));

        var n1 = await LayoutEngine.MeasureAsync(frame, c, Ctx, CancellationToken.None);
        var n2 = await LayoutEngine.MeasureAsync(frame, c, Ctx, CancellationToken.None);

        Assert.Equal(n1.Bounds, n2.Bounds);
        Assert.Equal(n1.Children[0].Bounds, n2.Children[0].Bounds);
        Assert.True(child.Size.Width.IsFill);            // model NOT mutated
        Assert.True(child.Position.IsEmpty);
    }

    [Fact]
    public async Task Frame_FillHeight_GrowsToMaxOfAvailableAndContent()
    {
        // Fill = max(window, content) rule: an 800pt child in a 500pt window
        var tall = new Frame { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(800)) };
        var frame = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill) };
        frame.AddElement(tall);
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), Ctx, CancellationToken.None);
        Assert.Equal(800, node.Bounds.Height);
    }

    [Fact]
    public async Task Frame_FixedChild_PositionedByAlignment()
    {
        var child = new Frame
        {
            Size = new Size(SizeLength.Fixed(100), SizeLength.Fixed(100)),
            HorizontalAlignment = Pysar.Core.Enums.Alignment.Center,
            VerticalAlignment = Pysar.Core.Enums.Alignment.End
        };
        var frame = new Frame { Size = new Size(SizeLength.Fixed(400), SizeLength.Fixed(400)) };
        frame.AddElement(child);
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 400, 400)), Ctx, CancellationToken.None);
        Assert.Equal(new Rect(150, 300, 250, 400), node.Children[0].Bounds);
    }

    [Fact]
    public async Task Box_AutoLeaf_YieldsZeroSize()
    {
        var frame = new Frame { Size = Size.Auto };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(10, 20, 500, 500)), Ctx, CancellationToken.None);
        Assert.Equal(0, node.Bounds.Width);
        Assert.Equal(0, node.Bounds.Height);
        Assert.Equal(10, node.Bounds.Left);
        Assert.Equal(20, node.Bounds.Top);
    }

    [Fact]
    public async Task Text_WordWrap_AutoHeight_GrowsToAllLines_NoEllipsis()
    {
        // Regression: Auto-height WordWrap used availableRect.Height as a hard cap and appended
        // "..." when the wrapped block exceeded it — so a narrow column showed ellipsis instead of
        // growing the Auto Text to fit every line.
        var text = new Text
        {
            Content = "2200 Medical Center Blvd, Los Angeles, CA 90027",
            Font = new Font("Arial", 14),
            TextTrimming = TextTrimming.WordWrap,
            Size = Size.Auto
        };

        // Narrow width forces several wrap lines; short available height would previously clip.
        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 120, 20)), Ctx, CancellationToken.None);

        Assert.True(node.Bounds.Height > 20,
            $"Auto WordWrap should grow past the available height, got {node.Bounds.Height}");
        Assert.True(node.Bounds.Height >= 14 * 1.2f * 2,
            $"Expected at least two line boxes, got {node.Bounds.Height}");
    }

    [Fact]
    public async Task Text_WordWrap_AutoHeight_Render_DoesNotDrawEllipsis()
    {
        var text = new Text
        {
            Content = "2200 Medical Center Blvd, Los Angeles, CA 90027",
            Font = new Font("Arial", 14, Colors.Black),
            TextTrimming = TextTrimming.WordWrap,
            Size = Size.Auto
        };

        var frame = new Frame { Size = new Size(SizeLength.Fixed(120), SizeLength.Auto) };
        frame.AddElement(text);

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithDetail(d => d.AddElement(frame))
            .Build();

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);
        // Spot-check: a full multi-line block is taller than one line; ellipsis-only clip was ~one line.
        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);
        Assert.True(layout.Flow[0].Bounds.Height > 30);
        Assert.NotNull(pages);
        Assert.NotEqual(SKColors.Empty, pages[0].GetPixel(10, 10));
    }

    [Fact]
    public async Task Text_WordWrap_Auto_MaxHeight_EllipsizesAndCapsHeight()
    {
        var text = new Text
        {
            Content = "2200 Medical Center Blvd, Los Angeles, CA 90027",
            Font = new Font("Arial", 14),
            TextTrimming = TextTrimming.WordWrap,
            Size = Size.Auto,
            MaxHeight = 20 // ~one line at 14*1.2
        };
        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 120, 500)), Ctx, CancellationToken.None);
        Assert.True(node.Bounds.Height <= 20.01f, $"height {node.Bounds.Height}");
    }

    [Fact]
    public async Task Text_WordWrap_MinHeight_FloorsBox()
    {
        var text = new Text
        {
            Content = "Hi",
            Font = new Font("Arial", 14),
            Size = Size.Auto,
            MinHeight = 50
        };
        var node = await LayoutEngine.MeasureAsync(text,
            new MeasureConstraint(new Rect(0, 0, 200, 500)), Ctx, CancellationToken.None);
        Assert.Equal(50, node.Bounds.Height);
    }

    [Fact]
    public async Task Frame_FixedSize_MaxWidth_ClampsBorderBox()
    {
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(100)),
            MaxWidth = 80
        };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), Ctx, CancellationToken.None);
        Assert.Equal(80, node.Bounds.Width);
        Assert.Equal(100, node.Bounds.Height);
    }

    [Fact]
    public async Task Frame_FixedSize_MinHeight_FloorsBorderBox()
    {
        var frame = new Frame
        {
            Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(20)),
            MinHeight = 60
        };
        var node = await LayoutEngine.MeasureAsync(frame,
            new MeasureConstraint(new Rect(0, 0, 500, 500)), Ctx, CancellationToken.None);
        Assert.Equal(50, node.Bounds.Width);
        Assert.Equal(60, node.Bounds.Height);
    }

    [Fact]
    public async Task Frame_AutoChild_MaxWidth_EndAlign_UsesClampedWidth()
    {
        var child = new Frame
        {
            Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(40)),
            MaxWidth = 50,
            HorizontalAlignment = Alignment.End
        };
        var parent = new Frame { Size = new Size(SizeLength.Fixed(400), SizeLength.Fixed(100)) };
        parent.AddElement(child);
        var node = await LayoutEngine.MeasureAsync(parent,
            new MeasureConstraint(new Rect(0, 0, 400, 100)), Ctx, CancellationToken.None);
        Assert.Equal(new Rect(350, 0, 400, 40), node.Children[0].Bounds);
    }
}
