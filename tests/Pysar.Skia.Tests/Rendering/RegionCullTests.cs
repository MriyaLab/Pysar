using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

/// <summary>
///     Region culling must keep pixel output identical to a full-page draw cropped to the same
///     rectangle, while skipping bands that cannot contribute any ink to the requested region.
/// </summary>
public class RegionCullTests
{
    private const float Scale = 1f;
    private const float Margin = 20f;

    [Fact]
    public async Task RegionRender_MatchesFullPageCrop()
    {
        var session = await ReportRenderSession.CreateAsync(BuildStackedFramesReport());
        var page = session.PageSizePt;
        var fullRegion = new SKRect(0, 0, page.Width, page.Height);

        // Mid-page strip over the blue frame only (red ends ~170, green starts ~320 at margin 20).
        var partRegion = new SKRect(40, 200, 400, 300);

        using var full = await session.RenderRegionAsync(0, fullRegion, Scale);
        using var part = await session.RenderRegionAsync(0, partRegion, Scale);

        var left = (int)MathF.Round(partRegion.Left * Scale);
        var top = (int)MathF.Round(partRegion.Top * Scale);

        Assert.Equal((int)MathF.Round(partRegion.Width * Scale), part.Width);
        Assert.Equal((int)MathF.Round(partRegion.Height * Scale), part.Height);

        var differences = 0;
        for (var x = 0; x < part.Width; x++)
        for (var y = 0; y < part.Height; y++)
            if (part.GetPixel(x, y) != full.GetPixel(left + x, top + y))
                differences++;

        Assert.Equal(0, differences);
    }

    [Fact]
    public async Task RegionRender_SkipsFlowBandsOutsideTheVisibleRegion()
    {
        var probe = new DrawCountingDrawer();
        var drawers = DrawerRegistry.CreateDefault();
        drawers.Register<Frame>(probe);

        var session = await ReportRenderSession.CreateAsync(BuildStackedFramesReport(), drawers);
        // Only the middle (blue) frame intersects this strip (see BuildStackedFramesReport).
        var partRegion = new SKRect(40, 200, 400, 300);

        using var _ = await session.RenderRegionAsync(0, partRegion, Scale);

        Assert.Equal(1, probe.DrawCount);
    }

    /// <summary>
    ///     Three full-width solid frames stacked in the detail: red, blue, green.
    ///     With margin 20, content starts at y=20; each frame is 150pt tall → blue at page y≈170–320.
    /// </summary>
    private static Report BuildStackedFramesReport()
    {
        var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        stack.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(150)),
            BackgroundColor = Colors.Red
        });
        stack.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(150)),
            BackgroundColor = Colors.Blue
        });
        stack.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(150)),
            BackgroundColor = Colors.Green
        });

        return ReportBuilder.Create("region-cull")
            .WithPageFormat(new PageFormat { Margin = new Thickness(Margin), Size = PageSize.A4 })
            .WithDetail(d => d.AddElement(stack))
            .Build();
    }

    private sealed class DrawCountingDrawer : IElementDrawer
    {
        private int _drawCount;

        public int DrawCount => _drawCount;

        public void Draw(LayoutNode node, RenderContext ctx)
        {
            Interlocked.Increment(ref _drawCount);
            // Background is painted by ElementDrawer before the leaf drawer runs.
        }
    }
}
