using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class PageClippingTests
{
    // A4 portrait with margin 10 → content window height = 842 - 20 = 822.
    // Three fixed 300pt rows (bottoms at 300/600/900) cut at 600, so the slice is 600pt,
    // shorter than the 822pt window. The remaining 222pt must be blank — not a bleed of row 2.
    [Fact]
    public async Task Render_CutSnapsToRow_NoBleedOfNextRowIntoWindow()
    {
        var grid = new Grid { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(
            new RowDefinition(GridLength.Fixed(300)),
            new RowDefinition(GridLength.Fixed(300)),
            new RowDefinition(GridLength.Fixed(300)));
        grid.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill), BackgroundColor = Colors.Red }, 0, 0);
        grid.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill), BackgroundColor = Colors.Blue }, 1, 0);
        grid.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill), BackgroundColor = Colors.Green }, 2, 0);

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(grid))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        Assert.Equal(2, pages.Count);
        // Ribbon y≈700 is inside row 2 (green), but past the row-1 cut at 600 → must be blank on page 1.
        // Page y = contentZone.Top(10) + (700 - sliceStart 0) = 710.
        Assert.Equal(SKColors.White, pages[0].GetPixel(100, 710));
        // Row 2 (green) appears in full on page 2, near its top.
        Assert.Equal(SKColors.Green, pages[1].GetPixel(100, 60));
    }
}
