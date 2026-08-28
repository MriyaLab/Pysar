using System.Collections;
using Xunit;

namespace Pysar.Elements.Tests;

public class DetailDataSourceTests
{
    [Fact]
    public void WithDataSource_SetsDataSource()
    {
        var items = new[] { 1, 2, 3 };
        var detail = new DetailBand().WithDataSource(items);
        Assert.Same((IEnumerable)items, detail.DataSource);
    }

    [Fact]
    public void WithDataSource_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DetailBand().WithDataSource(null!));
    }

    private sealed record Person(string Name);

    [Fact]
    public void Build_ExpandsTemplate_OneRowPerRecord()
    {
        var people = new[] { new Person("Ada"), new Person("Bob") };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(people);
                d.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, "Name")));
            })
            .Build();

        // Detail now holds a single StackPanel with one row per record.
        var outer = Assert.IsType<StackPanel>(Assert.Single(design.Detail.Children));
        var stack = Assert.IsType<StackPanel>(Assert.Single(outer.Children));
        Assert.Equal(2, stack.Children.Count);

        // Each row's bound Text resolved against its record.
        var row0 = (Frame)stack.Children[0];
        var row1 = (Frame)stack.Children[1];
        Assert.Equal("Ada", ((Text)row0.Children[0]).Content);
        Assert.Equal("Bob", ((Text)row1.Children[0]).Content);
    }

    [Fact]
    public void Build_CalledTwice_Throws()
    {
        var people = new[] { new Person("Ada"), new Person("Bob") };
        var builder = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(people);
                d.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, "Name")));
            });

        builder.Build();
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());

        Assert.Equal(
            "This report has already been built. Create a new report instance for each build.",
            exception.Message);
    }

    [Fact]
    public void Build_NoDataSource_LeavesDetailStatic()
    {
        var design = ReportBuilder.Create("t")
            .WithDetail(d => d.AddElement(new Text { Content = "static" }))
            .Build();

        var text = Assert.IsType<Text>(Assert.Single(design.Detail.Children));
        Assert.Equal("static", text.Content);
    }
}

internal static class TestElementExtensions
{
    public static T Also<T>(this T element, Action<T> configure)
    {
        configure(element);
        return element;
    }
}
