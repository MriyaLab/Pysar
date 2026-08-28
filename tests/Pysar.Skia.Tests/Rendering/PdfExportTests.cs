using System.Text;
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
        var pageCount = System.Text.RegularExpressions.Regex.Matches(pdf, "/Type\\s*/Page[^s]").Count;
        Assert.True(pageCount >= 2, $"expected >=2 pages, got {pageCount}");
    }
}
