using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class PdfExportTests
{
    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [Fact]
    public async Task RenderToPdf_ProducesValidPdf()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms);

        var bytes = ms.ToArray();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Latin1(bytes[..4]));
    }

    [Fact]
    public async Task RenderToPdfBytes_ProducesValidPdf()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        var bytes = await new SkiaReportRenderer().RenderToPdfBytesAsync(design);

        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Latin1(bytes[..4]));
    }

    [Fact]
    public async Task RenderToPdf_EmbedsTextAsVectorFont_NotRasterImage()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Vector text", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms);

        var pdf = Latin1(ms.ToArray());
        // Vector text is embedded as a font object; the raster path would carry an image instead.
        Assert.Contains("/Font", pdf);
    }

    [Fact]
    public async Task RenderToPdf_TallContent_EmitsMultiplePages()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2000)) }))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms);

        var pdf = Latin1(ms.ToArray());
        var pageCount = Regex.Matches(pdf, "/Type\\s*/Page[^s]").Count;
        Assert.True(pageCount >= 2, $"expected >=2 pages, got {pageCount}");
    }

    [Theory]
    [InlineData(Orientation.Portrait, 595.5, 842)]
    [InlineData(Orientation.Landscape, 842, 595.5)]
    public async Task RenderToPdf_WritesMediaBoxMatchingPageFormat(
        Orientation orientation, float width, float height)
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat
            {
                Margin = new Thickness(10),
                Size = PageSize.A4,
                Orientation = orientation
            })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms);

        var pdf = Latin1(ms.ToArray());
        var match = Regex.Match(pdf, @"MediaBox\s*\[([^\]]+)\]");
        Assert.True(match.Success, "PDF has no MediaBox");

        var numbers = match.Groups[1].Value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => double.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();

        Assert.Equal(4, numbers.Length);
        Assert.Equal(0, numbers[0], 0);
        Assert.Equal(0, numbers[1], 0);
        Assert.Equal(width, numbers[2], 0);
        Assert.Equal(height, numbers[3], 0);
        Assert.Equal(width > height, numbers[2] > numbers[3]);
    }
}
