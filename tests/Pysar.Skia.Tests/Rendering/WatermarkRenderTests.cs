using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class WatermarkRenderTests
{
    [Fact]
    public async Task Render_Watermark_PaintsUnderContent_OnFullPage()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithWatermark(w => w.WithBackgroundColor(Colors.Red))
            .WithDetail(d => d.AddElement(new Frame
            {
                Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)),
                BackgroundColor = Colors.Blue
            }))
            .Build();

        var page = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).First();

        Assert.Equal(SKColors.Red, page.GetPixel(5, 5));
        Assert.Equal(SKColors.Blue, page.GetPixel(20, 20));
        Assert.Equal(SKColors.Red, page.GetPixel(300, 400));
    }

    [Fact]
    public async Task Render_Watermark_Front_PaintsOverContent()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithWatermark(w =>
            {
                w.Layer = WatermarkLayer.Front;
                w.WithBackgroundColor(Colors.Red);
            })
            .WithDetail(d => d.AddElement(new Frame
            {
                Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)),
                BackgroundColor = Colors.Blue
            }))
            .Build();

        var page = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).First();

        Assert.Equal(SKColors.Red, page.GetPixel(5, 5));
        Assert.Equal(SKColors.Red, page.GetPixel(20, 20));
    }

    [Fact]
    public async Task Render_Watermark_RepeatsOnEveryPage()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithWatermark(w => w.WithBackgroundColor(Colors.Red))
            .WithDetail(d => d.AddElement(new Frame
            {
                Size = new Size(SizeLength.Fill, SizeLength.Fixed(2000))
            }))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();
        Assert.True(pages.Count >= 2);
        Assert.Equal(SKColors.Red, pages[0].GetPixel(300, 400));
        Assert.Equal(SKColors.Red, pages[1].GetPixel(300, 400));
    }

    [Fact]
    public async Task Render_Watermark_Hidden_DoesNotPaint()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithWatermark(w =>
            {
                w.WithBackgroundColor(Colors.Red);
                w.IsVisible = false;
            })
            .WithDetail(d => d.AddElement(new Frame
            {
                Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)),
                BackgroundColor = Colors.Blue
            }))
            .Build();

        var page = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).First();
        Assert.Equal(SKColors.White, page.GetPixel(300, 400));
        Assert.Equal(SKColors.Blue, page.GetPixel(20, 20));
    }
}
