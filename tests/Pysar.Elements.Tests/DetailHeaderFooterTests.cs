using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class DetailHeaderFooterTests
{
    [Fact]
    public void WithDetailHeaderFooter_SetFramesAndRepeatFlag()
    {
        var detail = new DetailBand()
            .WithDetailHeader(h => h.WithBackgroundColor(Colors.Red))
            .WithDetailFooter(f => f.WithBackgroundColor(Colors.Blue))
            .WithRepeatDetailHeader();

        Assert.NotNull(detail.DetailHeader);
        Assert.NotNull(detail.DetailFooter);
        Assert.True(detail.RepeatDetailHeaderOnEveryPage);
        Assert.Equal(Colors.Red, detail.DetailHeader!.BackgroundColor);
        Assert.Equal(Colors.Blue, detail.DetailFooter!.BackgroundColor);
    }

    private sealed record Item(string N);

    [Fact]
    public void Build_WithHeaderAndFooter_WrapsRowsInOuterStack()
    {
        var items = new[] { new Item("a"), new Item("b") };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(items);
                d.WithDetailHeader(h => h.AddText("Name", _ => { }));
                d.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, "N")));
                d.WithDetailFooter(f => f.AddText("Total", _ => { }));
            })
            .Build();

        var outer = Assert.IsType<StackPanel>(Assert.Single(design.Detail.Children));
        Assert.Equal(3, outer.Children.Count);                 // header, rows, footer
        Assert.Same(design.Detail.DetailHeader, outer.Children[0]);
        var rows = Assert.IsType<StackPanel>(outer.Children[1]);
        Assert.Equal(2, rows.Children.Count);                  // one row per item
        Assert.Same(design.Detail.DetailFooter, outer.Children[2]);
    }

    [Fact]
    public void Build_NoHeaderFooter_OuterStackHoldsOnlyRows()
    {
        var items = new[] { new Item("a") };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(items);
                d.AddElement(new Text { Content = "x" });
            })
            .Build();

        var outer = Assert.IsType<StackPanel>(Assert.Single(design.Detail.Children));
        var rows = Assert.IsType<StackPanel>(Assert.Single(outer.Children));
        Assert.Single(rows.Children);
    }
}
