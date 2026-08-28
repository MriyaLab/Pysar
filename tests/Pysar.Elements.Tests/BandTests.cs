using Pysar.Core.Enums;
using Xunit;

namespace Pysar.Elements.Tests;

public class BandTests
{
    [Fact]
    public void Band_Defaults_AreNoneAndFalse()
    {
        var band = new DetailBand();
        Assert.Equal(PageBreakMode.None, band.PageBreak);
        Assert.False(band.KeepTogether);
    }

    [Fact]
    public void Band_IsFrame_SupportsChildren()
    {
        var band = new ReportHeaderBand();
        band.AddElement(new Text { Content = "x" });
        Assert.Single(band.Children);
    }

    [Fact]
    public void Band_Setters_RoundTrip()
    {
        var band = new DetailBand { PageBreak = PageBreakMode.After, KeepTogether = true };
        Assert.Equal(PageBreakMode.After, band.PageBreak);
        Assert.True(band.KeepTogether);
    }

    [Fact]
    public void BandCollection_SecondBandOfSameType_Throws()
    {
        var design = new Report();
        design.Bands.Add(new PageHeaderBand());
        Assert.Throws<InvalidOperationException>(() => design.Bands.Add(new PageHeaderBand()));
    }

    [Fact]
    public void BandCollection_AddNull_Throws()
    {
        var design = new Report();
        Assert.Throws<ArgumentNullException>(() => design.Bands.Add(null!));
    }

    [Fact]
    public void BandCollection_DifferentBandTypes_Coexist()
    {
        // A fresh Report already contains a DetailBand; adding a PageHeaderBand gives 2.
        var design = new Report();
        design.Bands.Add(new PageHeaderBand());
        Assert.Equal(2, design.Bands.Count);
    }

    [Fact]
    public void BandCollection_Add_SetsParent()
    {
        var design = new Report();
        var band = new PageHeaderBand();
        design.Bands.Add(band);
        Assert.Same(design, band.ParentElement);
    }

    [Fact]
    public void BandCollection_GetBand_ReturnsTypedOrNull()
    {
        var design = new Report();
        design.Bands.Add(new ReportFooterBand());
        Assert.NotNull(design.Bands.GetBand<ReportFooterBand>());
        Assert.Null(design.Bands.GetBand<PageHeaderBand>());
    }

    [Fact]
    public void Report_Detail_AutoCreated()
    {
        var design = new Report();
        Assert.NotNull(design.Detail);
        Assert.Same(design.Detail, design.Bands.GetBand<DetailBand>());
    }

    [Fact]
    public void Report_TypedAccessors_ReflectBands()
    {
        var design = new Report();
        var header = new PageHeaderBand();
        design.Bands.Add(header);
        Assert.Same(header, design.PageHeader);
        Assert.Null(design.ReportFooter);
    }

    [Fact]
    public void Builder_WithBands_ConfiguresBands()
    {
        var design = ReportBuilder.Create("t")
            .WithPageHeader(b => b.AddElement(new Text { Content = "hdr" }))
            .WithDetail(b => b.AddElement(new Text { Content = "body" }))
            .Build();

        Assert.NotNull(design.PageHeader);
        Assert.Single(design.PageHeader!.Children);
        Assert.Single(design.Detail.Children);
    }

    [Fact]
    public void Builder_Build_EnsuresDetailExists()
    {
        var design = ReportBuilder.Create("t").Build();
        Assert.NotNull(design.Bands.GetBand<DetailBand>());
    }

    [Fact]
    public void Builder_WithBand_CalledTwice_ReusesSameBand()
    {
        var design = ReportBuilder.Create("t")
            .WithDetail(b => b.AddElement(new Text { Content = "a" }))
            .WithDetail(b => b.AddElement(new Text { Content = "b" }))
            .Build();

        Assert.Equal(2, design.Detail.Children.Count);
    }

    [Fact]
    public void Builder_Build_ResolvesBindingsInsideBands()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "CustomerName");

        ReportBuilder.Create("t")
            .WithDetail(b =>
            {
                b.DataContext = new { CustomerName = "John Doe" };
                b.AddElement(text);
            })
            .Build();

        Assert.Equal("John Doe", text.Content);
    }

    [Fact]
    public void Builder_WithBand_NullConfigure_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ReportBuilder.Create("t").WithDetail(null!));
    }
}
