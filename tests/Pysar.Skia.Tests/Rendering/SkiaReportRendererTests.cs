using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Skia;
using Pysar.Skia.Helpers;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class SkiaReportRendererTests
{
    // A custom element outside the built-in set (the QRCode analog).
    private sealed class Badge : ReportContainer<Badge> { }

    private sealed class BadgeDrawer : IElementDrawer
    {
        public void Draw(LayoutNode node, RenderContext ctx)
        {
            using var paint = new SKPaint { Color = SKColors.Green };
            ctx.Canvas.DrawRect(node.Bounds.ToSkiaRect(ctx.Scale), paint);
        }
    }

    [Fact]
    public async Task Render_CustomElement_UsesRegisteredDrawer()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(new Badge { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)) }))
            .Build();

        var renderer = new SkiaReportRenderer().WithDrawer<Badge>(new BadgeDrawer());
        var pages = (await renderer.RenderPageAsync(design, scale: 1f)).ToList();

        Assert.Single(pages);
        // Badge sits at the detail's top-left, offset by the page margin (10,10).
        Assert.Equal(SKColors.Green, pages[0].GetPixel(20, 20));
    }

    [Fact]
    public async Task Render_WithoutDrawer_CustomElementFallsBackToContainer()
    {
        // No drawer registered → the element is treated as a plain container (no custom paint, no crash).
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(new Badge
            {
                Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)),
                BackgroundColor = Colors.Red
            }))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        Assert.Single(pages);
        Assert.Equal(SKColors.Red, pages[0].GetPixel(20, 20)); // background still painted by ElementDrawer
    }

    [Fact]
    public async Task Render_BandApi_ProducesPages()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(30)).WithBackgroundColor(Colors.Blue))
            .WithDetail(b => b.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2000)) }))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        Assert.True(pages.Count >= 2);
        Assert.Equal(SKColors.Blue.Red, pages[0].GetPixel(100, 15).Red); // header repeats on each page
        Assert.Equal(SKColors.Blue.Red, pages[1].GetPixel(100, 15).Red);
    }
}
