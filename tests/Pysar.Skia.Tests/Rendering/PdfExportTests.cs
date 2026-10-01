using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Export;
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

    [Fact]
    public async Task RenderToPdf_PdfA_WritesPdfA2bPartIdentification()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms, new PdfExportOptions { PdfA = true });

        Assert.Contains("pdfaid:part>2", Latin1(ms.ToArray()));
    }

    [Fact]
    public async Task RenderToPdf_PdfA_WritesPdfA2bConformance()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms, new PdfExportOptions { PdfA = true });

        Assert.Contains("pdfaid:conformance>B", Latin1(ms.ToArray()));
    }

    [Fact]
    public async Task RenderToPdf_WithoutPdfA_OmitsPdfAIdentification()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms);

        Assert.DoesNotContain("pdfaid:", Latin1(ms.ToArray()));
    }

    [Fact]
    public async Task RenderToPdf_PdfAFalse_OmitsPdfAIdentification()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await new SkiaReportRenderer().RenderToPdfAsync(design, ms, new PdfExportOptions { PdfA = false });

        Assert.DoesNotContain("pdfaid:", Latin1(ms.ToArray()));
    }

    [Fact]
    public async Task RenderToPdfBytes_PdfA_WritesPdfA2bPartIdentification()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        var bytes = await new SkiaReportRenderer().RenderToPdfBytesAsync(
            design, new PdfExportOptions { PdfA = true });

        Assert.Contains("pdfaid:part>2", Latin1(bytes));
    }

    [Fact]
    public async Task SavePdf_PdfA_WritesPdfA2bPartIdentification()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pysar-pdfa-{Guid.NewGuid():N}.pdf");
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        await new SkiaReportRenderer().SavePdfAsync(design, path, new PdfExportOptions { PdfA = true });

        Assert.Contains("pdfaid:part>2", Latin1(File.ReadAllBytes(path)));
    }

    [Fact]
    public async Task RenderToPdf_NullOptions_Throws()
    {
        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new SkiaReportRenderer().RenderToPdfAsync(design, ms, null!));
    }
}
