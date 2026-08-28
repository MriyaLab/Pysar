using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class DetailHeaderFooterRenderTests
{
    private sealed record Item(int I);

    [Fact]
    public async Task Render_HeaderOnceAndFooterAfterRows()
    {
        var items = Enumerable.Range(0, 3).Select(i => new Item(i)).ToArray();

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(d =>
            {
                d.WithDataSource(items);
                d.WithDetailHeader(h => h.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).WithBackgroundColor(Colors.Green));
                d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(30)), BackgroundColor = Colors.Red });
                d.WithDetailFooter(f => f.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).WithBackgroundColor(Colors.Blue));
            })
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        // content zone top = 10. Header [10..30] green, 3 rows of 30 [30..120] red, footer [120..140] blue.
        Assert.Equal(SKColors.Green, pages[0].GetPixel(100, 20));   // header at top
        Assert.Equal(SKColors.Red, pages[0].GetPixel(100, 60));     // a row
        Assert.Equal(SKColors.Blue, pages[0].GetPixel(100, 130));   // footer after rows
    }

    [Fact]
    public async Task Render_RepeatHeader_AppearsAtTopOfSecondPage()
    {
        // Enough rows to overflow one page; the header repeats at the top of page 2 above the first row.
        var items = Enumerable.Range(0, 60).Select(i => new Item(i)).ToArray();

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(d =>
            {
                d.WithDataSource(items);
                d.WithDetailHeader(h => h.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).WithBackgroundColor(Colors.Green));
                d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(30)), BackgroundColor = Colors.Red });
                d.WithRepeatDetailHeader();
            })
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        Assert.True(pages.Count >= 2, $"expected multiple pages, got {pages.Count}");
        // Page 2 content zone top = 10; the repeated header [10..30] is green, and the first row starts below it.
        Assert.Equal(SKColors.Green, pages[1].GetPixel(100, 20));   // repeated header on page 2
        Assert.Equal(SKColors.Red, pages[1].GetPixel(100, 40));     // first row below the repeated header
    }
}
