using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class PageRendererTests
{
    [Fact]
    public async Task Render_TwoPages_HeaderOnBoth_AncestorBackgroundOnBoth()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(40)).WithBackgroundColor(Colors.Blue))
            .WithDetail(b =>
            {
                var tall = new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(1600)), BackgroundColor = Colors.Red };
                b.AddElement(tall);          // taller than a page → 2+ pages
            })
            .Build();

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.True(pages.Count >= 2);
        // PageHeader (blue) at the top of EVERY page
        Assert.Equal(SKColors.Blue.Red, pages[0].GetPixel(100, 20).Red);
        Assert.Equal(SKColors.Blue.Red, pages[1].GetPixel(100, 20).Red);
        // The Frame's red background — on both pages
        Assert.Equal(SKColors.Red, pages[0].GetPixel(100, 400));
        Assert.Equal(SKColors.Red, pages[1].GetPixel(100, 200));
    }

    [Fact]
    public async Task Render_SinglePage_WhenContentFits()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) }))
            .Build();

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.Single(pages);
    }

    [Fact]
    public async Task Render_UsesReportBackgroundColor_OnPageSurface()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) }))
            .Build();
        design.BackgroundColor = Colors.LightGray;

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        // Page margin area is outside bands — only the report surface paints it.
        Assert.Equal(SKColors.LightGray, pages[0].GetPixel(3, 3));
    }

    [Fact]
    public async Task Render_UsesReportBorder_OnPageEdges()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) }))
            .Build();
        design.BorderColor = Colors.Black;
        design.BorderThickness = new Thickness(4);
        design.BorderLineStyle = BorderLineStyle.Solid;

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);
        var page = pages[0];

        // Stroke is centred on the edge: thickness 4 → sample at sw/2 = 2.
        Assert.Equal(SKColors.Black, page.GetPixel(100, 2));
        Assert.Equal(SKColors.Black, page.GetPixel(2, 100));
        Assert.Equal(SKColors.Black, page.GetPixel(page.Width - 3, 100));
        Assert.Equal(SKColors.Black, page.GetPixel(100, page.Height - 3));
    }

    [Fact]
    public async Task Render_ReportBackground_AppliesOnEveryPage()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b =>
            {
                b.AddElement(new Frame
                {
                    Size = new Size(SizeLength.Fill, SizeLength.Fixed(1600)),
                    BackgroundColor = Colors.Red
                });
            })
            .Build();
        design.BackgroundColor = Colors.LightGray;

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.True(pages.Count >= 2);
        Assert.Equal(SKColors.LightGray, pages[0].GetPixel(3, 3));
        Assert.Equal(SKColors.LightGray, pages[1].GetPixel(3, 3));
    }
}
