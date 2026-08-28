using Pysar.Binding;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class PageBandResolverTests
{
    /// <summary>
    ///     A report whose footer holds one Text bound to <paramref name="path"/> on the report itself —
    ///     the <c>Source={x:Reference Root}</c> form, expressed against the object model.
    /// </summary>
    private static (Report Design, Text FooterText) BuildReportWithBoundFooter(string path)
    {
        var footerText = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).AddElement(footerText))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();
        footerText.SetBinding(Text.ContentProperty, new BindingInfo(path, source: design));

        return (design, footerText);
    }

    /// <summary>
    ///     Wires up an already-built <paramref name="design"/> into a measured <see cref="ReportLayout"/>
    ///     and the resolver under test, so each test only has to describe its own report shape.
    /// </summary>
    private static async Task<(PageBandResolver Resolver, ReportLayout Layout)> CreateAsync(Report design)
    {
        var measure = new MeasureContext(1f);
        var layout = await ReportLayoutEngine.MeasureAsync(design, measure, CancellationToken.None);

        return (new PageBandResolver(design, layout, measure), layout);
    }

    private static async Task<(PageBandResolver Resolver, Text FooterText, ReportLayout Layout)>
        CreateWithBoundFooterAsync(string path)
    {
        var (design, footerText) = BuildReportWithBoundFooter(path);
        var (resolver, layout) = await CreateAsync(design);

        return (resolver, footerText, layout);
    }

    [Fact]
    public async Task ResolveAsync_AppliesTheCurrentPageNumber()
    {
        var (resolver, footerText, _) = await CreateWithBoundFooterAsync("PageNumber");

        await resolver.ResolveAsync(2, 3, CancellationToken.None);
        Assert.Equal("2", footerText.Content);

        await resolver.ResolveAsync(3, 3, CancellationToken.None);
        Assert.Equal("3", footerText.Content);
    }

    [Fact]
    public async Task ResolveAsync_AppliesTheTotalPageCount()
    {
        var (resolver, footerText, _) = await CreateWithBoundFooterAsync("PageCount");

        await resolver.ResolveAsync(1, 7, CancellationToken.None);

        Assert.Equal("7", footerText.Content);
    }

    [Fact]
    public async Task ResolveAsync_LeavesUnsourcedBindingsReadingReportData()
    {
        // No page context hijacks the band's data context, so report data stays directly reachable.
        var footerText = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        footerText.SetBinding(Text.ContentProperty, "CompanyName");

        var design = ReportBuilder.Create("t")
            .WithDataContext(new { CompanyName = "Northwind" })
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).AddElement(footerText))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();

        var (resolver, _) = await CreateAsync(design);

        await resolver.ResolveAsync(1, 1, CancellationToken.None);

        Assert.Equal("Northwind", footerText.Content);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsAFreshlyMeasuredFooterNode()
    {
        var (resolver, _, layout) = await CreateWithBoundFooterAsync("PageNumber");

        var (_, footer) = await resolver.ResolveAsync(2, 3, CancellationToken.None);

        Assert.NotNull(footer);
        Assert.IsType<PageFooterBand>(footer!.Element);
        Assert.NotSame(layout.PageFooter, footer);
    }

    [Fact]
    public async Task ResolveAsync_WithoutBindings_ReturnsTheBaseLayoutNodes()
    {
        // Fast path: no work, and the very same node instances come back.
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)))
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();

        var (resolver, layout) = await CreateAsync(design);

        var (header, footer) = await resolver.ResolveAsync(2, 5, CancellationToken.None);

        Assert.Same(layout.PageHeader, header);
        Assert.Same(layout.PageFooter, footer);
    }

    [Fact]
    public async Task ResolveAsync_WithOneBoundBand_AlsoReMeasuresTheUnboundBand()
    {
        // The unbound PageHeader shares the resolver's slow path with the bound PageFooter, so it
        // must come back freshly measured too, not as the cached base-layout instance.
        var footerText = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)))
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).AddElement(footerText))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();
        footerText.SetBinding(Text.ContentProperty, new BindingInfo("PageNumber", source: design));

        var (resolver, layout) = await CreateAsync(design);

        var (header, footer) = await resolver.ResolveAsync(2, 5, CancellationToken.None);

        Assert.NotSame(layout.PageHeader, header);
        Assert.NotSame(layout.PageFooter, footer);
    }

    [Fact]
    public async Task ResolveAsync_WithNoPageBands_ReturnsNulls()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();

        var (resolver, _) = await CreateAsync(design);

        var (header, footer) = await resolver.ResolveAsync(1, 1, CancellationToken.None);

        Assert.Null(header);
        Assert.Null(footer);
    }
}
