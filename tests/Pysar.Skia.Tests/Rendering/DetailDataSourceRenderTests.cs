using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class DetailDataSourceRenderTests
{
    private sealed record Row(int Index);

    [Fact]
    public async Task Render_ManyRecords_ProducesMultiplePages()
    {
        // ~40 rows of 100pt each far exceed one A4 content window → multiple pages.
        var rows = Enumerable.Range(0, 40).Select(i => new Row(i)).ToArray();

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(20), Size = PageSize.A4 })
            .WithDetail(d =>
            {
                d.WithDataSource(rows);
                d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(100)) });
            })
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        Assert.True(pages.Count >= 2, $"expected multiple pages, got {pages.Count}");
    }

    [Fact]
    public async Task Render_Records_StackRowsVertically()
    {
        // Two records → two 50pt row frames stacked; both painted red at their stacked positions.
        var records = new[] { new Row(0), new Row(1) };

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(d =>
            {
                d.WithDataSource(records);
                d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)), BackgroundColor = Colors.Red });
            })
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        // Content zone top = margin 10. Row 0 spans y[10..60], row 1 y[60..110].
        Assert.Equal(SKColors.Red, pages[0].GetPixel(100, 30));  // inside row 0
        Assert.Equal(SKColors.Red, pages[0].GetPixel(100, 90));  // inside row 1 (stacked below)
        Assert.NotEqual(SKColors.Red, pages[0].GetPixel(100, 200)); // blank below the two rows
    }
}
