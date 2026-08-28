using Pysar.Binding;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class ReportLayoutEngineTests
{
    [Fact]
    public async Task Measure_PageHeaderNegativeTopMargin_ReservesBledBottomNotFullHeight()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(200)).WithMargin(-30, -30, -30, 0))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        // Top margin -30 shifts the 200pt header box up, so its bottom sits at 170 relative to the content
        // zone top. The flow reserves that bottom (170) and follows right after — no gap, not the full 200.
        Assert.Equal(170, layout.PageHeaderHeight);
    }

    [Fact]
    public async Task Measure_PageFooterNegativeBottomMargin_BleedsDownAndReservesLess()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(30)).WithMargin(-30, 0, -30, -30))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        // Bottom margin -30 pushes the 30pt footer fully into the bottom page margin → its margin-box
        // reserves 0 within the content zone (Bounds.Bottom 30 + marginBottom -30), so the flow keeps
        // the full height and the footer sits at the very bottom edge.
        Assert.Equal(0, layout.PageFooterHeight);
    }

    [Fact]
    public async Task Measure_FlowBandBottomMargin_AddsGapBeforeNextBand()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithReportHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)).WithMargin(0, 0, 0, 20))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(50)))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        // ReportHeader box is [0,100]; its bottom margin (20) must push the Detail down to flow y=120.
        Assert.Equal(120, layout.Flow[1].Bounds.Top);
    }

    [Fact]
    public async Task Measure_TemplateFirst_FlowGetsReducedWindow()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(40)))
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(30)))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        var contentH = design.PageFormat.GetPageSizePt().Height - 60;   // minus top+bottom margins
        Assert.Equal(40, layout.PageHeaderHeight);
        Assert.Equal(30, layout.PageFooterHeight);
        Assert.Equal(contentH - 70, layout.ContentWindowHeight);
        Assert.Equal(100, layout.Flow[^1].Bounds.Height);
    }

    [Fact]
    public async Task Measure_ReportHeaderAuto_WithDefaultFillGrid_SizesToContentNotWindow()
    {
        // Regression: Auto flow bands used to pass the full content window to children, so a default
        // Height=Fill Grid (even with only Auto rows) claimed the whole page and the Auto band
        // inflated to match — Auto behaved like Fill.
        var grid = new Grid { BackgroundColor = Colors.Red };
        grid.WithColumnDefinitions("*");
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(40)),
            BackgroundColor = Colors.Blue
        }, 0, 0);
        grid.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(60)),
            BackgroundColor = Colors.Green
        }, 1, 0);

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithReportHeader(h =>
            {
                h.Size = new Size(SizeLength.Fill, SizeLength.Auto);
                h.BackgroundColor = Colors.Red;
                h.AddElement(grid);
            })
            .WithDetail(d => d.WithSize(SizeLength.Fill, SizeLength.Fixed(20)))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);
        var header = layout.Flow[0];

        Assert.True(header.Bounds.Height < layout.ContentWindowHeight / 2,
            $"Auto header should size to content, got {header.Bounds.Height} vs window {layout.ContentWindowHeight}");
        Assert.Equal(100, header.Bounds.Height); // 40 + 60
        Assert.Equal(100, layout.Flow[1].Bounds.Top); // Detail follows the header, not the page bottom
    }

    [Fact]
    public async Task Measure_ReportHeaderAuto_WithFillFrameChild_SizesToContent()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithReportHeader(h =>
            {
                h.Size = new Size(SizeLength.Fill, SizeLength.Auto);
                h.AddElement(new Frame
                {
                    Size = new Size(SizeLength.Fill, SizeLength.Fixed(80)),
                    BackgroundColor = Colors.Red
                });
            })
            .WithDetail(d => d.WithSize(SizeLength.Fill, SizeLength.Fixed(10)))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        Assert.Equal(80, layout.Flow[0].Bounds.Height);
        Assert.Equal(80, layout.Flow[1].Bounds.Top);
    }

    [Fact]
    public async Task Measure_ReservesExceedPage_Throws()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(500)))
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(500)))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None));
    }

    [Fact]
    public async Task Measure_EmptyFlow_FullHeightPageHeader_AllowsZeroContentWindow()
    {
        // Template-only report: empty Detail, PageHeader Fill claims the whole content zone.
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(50), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fill)
                .AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fill) }))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        var contentH = design.PageFormat.GetPageSizePt().Height - 100;
        Assert.Equal(contentH, layout.PageHeaderHeight);
        Assert.Equal(0, layout.ContentWindowHeight);
        Assert.Equal(0, layout.FlowHeight);
    }

    [Fact]
    public async Task Measure_NonEmptyFlow_FullHeightPageHeader_StillThrows()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(50), Size = PageSize.A4 })
            .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fill))
            .WithDetail(b => b.AddElement(new Text { Content = "row" }))
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None));
    }

    [Fact]
    public async Task Measure_FlowBands_StackSequentially()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithReportHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(200)))
            .WithReportFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(50)))
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        Assert.Equal(0, layout.Flow[0].Bounds.Top);      // ReportHeader
        Assert.Equal(100, layout.Flow[1].Bounds.Top);    // Detail immediately after
        Assert.Equal(300, layout.Flow[2].Bounds.Top);    // ReportFooter
    }

    [Fact]
    public async Task Measure_ResolvesPageBandBindingsAgainstTheReport()
    {
        var footerText = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).AddElement(footerText))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();
        footerText.SetBinding(Text.ContentProperty, new BindingInfo("PageNumber", source: design));
        design.PageNumber = 7;

        await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        // Reserved header/footer heights must be measured from resolved content, not placeholders.
        Assert.Equal("7", footerText.Content);
    }

    [Fact]
    public async Task Measure_LeavesPageBandsReadingReportData()
    {
        // The page number travels by explicit source, so nothing hijacks the data context — an unsourced
        // binding in a page band still reads the report's own data, exactly as it did before paging.
        var footerText = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        footerText.SetBinding(Text.ContentProperty, "CompanyName");

        var design = ReportBuilder.Create("t")
            .WithDataContext(new { CompanyName = "Northwind" })
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).AddElement(footerText))
            .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
            .Build();

        await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        Assert.Equal("Northwind", footerText.Content);
    }
}
