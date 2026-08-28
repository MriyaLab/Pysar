using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class MarginRenderTests
{
    [Fact]
    public async Task ReportHeader_NegativeHorizontalMargin_BleedsToPageEdges()
    {
        // A4 with 30pt margins; a ReportHeader with Margin(-30,0) should span the full page width.
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
            .WithReportHeader(rh => rh
                .WithSize(SizeLength.Fill, SizeLength.Fixed(100))
                .WithMargin(-30, 0)
                .WithBackgroundColor(Colors.Blue))
            .WithDetail(d => d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) }))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        // The header sits just below the top margin (content zone top = 30); it spans y[30..130].
        // With the negative side margins it now reaches x=0 and x=pageWidth (595), into the page margins.
        Assert.Equal(SKColors.Blue, pages[0].GetPixel(3, 40));    // left page margin — was white before the fix
        Assert.Equal(SKColors.Blue, pages[0].GetPixel(590, 40));  // right page margin
    }

    [Fact]
    public async Task NestedFrame_NegativeHorizontalMargin_BleedsToPageEdges()
    {
        // Full-bleed child inside a page band/grid (not the band itself) must still cover the page edge.
        // ApplyEdgeBleed used to grow only the band box, so nested fills left an AA hairline on the right.
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(50), Size = PageSize.A4 })
            .WithPageHeader(header =>
            {
                header.IsClippedToBounds = false;
                header.WithSize(SizeLength.Fill, SizeLength.Fill);
                header.WithBackgroundColor(Colors.Blue);
                var grid = new Grid
                {
                    Size = new Size(SizeLength.Fill, SizeLength.Fill),
                    IsClippedToBounds = false,
                    RowDefinitions = { new RowDefinition(GridLength.Fixed(150)) }
                };
                grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
                grid.AddElement(new Frame
                {
                    Size = new Size(SizeLength.Fill, SizeLength.Fill),
                    Margin = new Thickness(-50, 0),
                    BackgroundColor = Colors.Chocolate,
                    IsClippedToBounds = false
                }, 0, 0);
                header.AddElement(grid);
            })
            .Build();

        // High scale exposes AA hairlines at the page edge that scale=1 can hide.
        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 4f)).ToList();
        var page = pages[0];
        // Row sits below content-zone top (50pt → 200px); sample mid-row.
        const int y = 120 * 4;
        Assert.Equal(SKColors.Chocolate, page.GetPixel(1, y));
        Assert.Equal(SKColors.Chocolate, page.GetPixel(page.Width - 1, y));
    }

    [Fact]
    public async Task ReportHeader_NoMargin_StaysWithinContentZone()
    {
        // Regression guard: without a negative margin, the header must NOT bleed into the page margin.
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
            .WithReportHeader(rh => rh
                .WithSize(SizeLength.Fill, SizeLength.Fixed(100))
                .WithBackgroundColor(Colors.Blue))
            .WithDetail(d => d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(50)) }))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        Assert.NotEqual(SKColors.Blue, pages[0].GetPixel(3, 40));   // left margin stays white
        Assert.Equal(SKColors.Blue, pages[0].GetPixel(100, 40));    // inside content zone is blue
    }

    [Fact]
    public async Task Text_TopMargin_BackgroundAndGlyphsShareTheSameBox()
    {
        // Regression: TextDrawer used to re-apply Margin on top of Bounds (already margin-adjusted),
        // so the background sat at Bounds while glyphs were shifted down by Margin.Top again.
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithReportHeader(h => h
                .WithSize(SizeLength.Fill, SizeLength.Fixed(120))
                .WithBackgroundColor(Colors.Cyan)
                .AddElement(new Text
                {
                    Content = "PREPARED BY:",
                    BackgroundColor = Colors.Red,
                    Font = new Font("Arial", 20, Colors.Black, FontStyle.Bold),
                    Size = new Size(SizeLength.Fixed(200), SizeLength.Fixed(30)),
                    Margin = new Thickness(0, 40, 0, 0)
                }))
            .WithDetail(d => d.WithSize(SizeLength.Fill, SizeLength.Fixed(10)))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();
        var page = pages[0];

        // Border-box: top=40, height=30 → y[40..70]. Mid-box y=55 must be red (bg) or black (glyph).
        var mid = page.GetPixel(20, 55);
        Assert.True(
            mid.Equals(SKColors.Red) || (mid.Red < 40 && mid.Green < 40 && mid.Blue < 40),
            $"Expected red background or black text at (20,55), got {mid}");
        // Margin gap above the box stays cyan.
        Assert.Equal(SKColors.Cyan, page.GetPixel(20, 20));
    }

    [Fact]
    public async Task ReportHeader_NegativeTopMargin_CancelsPageHeaderBottomMargin()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(50), Size = PageSize.A4 })
            .WithPageHeader(header => header
                .WithSize(SizeLength.Fill, SizeLength.Fixed(25))
                .WithMargin(-50, -50, -50, 20)
                .WithBackgroundColor(Colors.Blue))
            .WithReportHeader(header => header
                .WithSize(SizeLength.Fill, SizeLength.Fixed(100))
                .WithMargin(-50, -20, -50, 0)
                .WithBackgroundColor(Colors.Red))
            .WithDetail(detail => detail.WithSize(SizeLength.Fill, SizeLength.Fixed(50)))
            .Build();

        var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();

        Assert.Equal(SKColors.Blue, pages[0].GetPixel(100, 24));
        Assert.Equal(SKColors.Red, pages[0].GetPixel(100, 25));
        Assert.Equal(SKColors.Red, pages[0].GetPixel(100, 44));
    }
}
