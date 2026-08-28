using Xunit;

namespace Pysar.Elements.Tests;

public class BoundDataSourceTests
{
    private sealed record Person(string Name);
    private sealed record ReportVm(IReadOnlyList<Person> People);

    [Fact]
    public void Build_BoundRootDataSource_ResolvesFromReportDataContext()
    {
        var vm = new ReportVm(new[] { new Person("Ada"), new Person("Bob") });

        var design = ReportBuilder.Create("t")
            .WithDataContext(vm)
            .WithDetail(d =>
            {
                // Root data source is BOUND (not a literal) — resolved from the report data context
                // before expansion.
                d.SetBinding(DetailBand.DataSourceProperty, nameof(ReportVm.People));
                d.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Person.Name))));
            })
            .Build();

        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];
        Assert.Equal(2, rows.Children.Count);
        Assert.Equal("Ada", ((Text)((Frame)rows.Children[0]).Children[0]).Content);
        Assert.Equal("Bob", ((Text)((Frame)rows.Children[1]).Children[0]).Content);
    }

    private sealed record Category(string Name, IReadOnlyList<Person> Members);

    [Fact]
    public void Build_NestedDataSourceBinding_MauiItemsSourceStyle_ResolvesPerItem()
    {
        var categories = new[]
        {
            new Category("A", new[] { new Person("Ada"), new Person("Bob") }),
            new Category("B", new[] { new Person("Cleo") }),
        };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(categories);
                // Nested items are BOUND (SetBinding on the repeater's DataSource) — resolved against each
                // category at expansion. This is what AddGroup(path) now does under the hood.
                d.AddElement(new StackPanel()
                    .AddGroup(nameof(Category.Members), g => g
                        .AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Person.Name))))));
            })
            .Build();

        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];

        // category row Frame -> template StackPanel -> group outer StackPanel -> members rows StackPanel
        var wrapperA = (StackPanel)((Frame)rows.Children[0]).Children[0];
        var membersA = (StackPanel)((StackPanel)wrapperA.Children[0]).Children[0];
        Assert.Equal(2, membersA.Children.Count);
        Assert.Equal("Ada", ((Text)((Frame)membersA.Children[0]).Children[0]).Content);

        var wrapperB = (StackPanel)((Frame)rows.Children[1]).Children[0];
        var membersB = (StackPanel)((StackPanel)wrapperB.Children[0]).Children[0];
        Assert.Equal("Cleo", ((Text)((Frame)Assert.Single(membersB.Children)).Children[0]).Content);
    }

    [Fact]
    public void Build_LiteralDataSource_StillWorks()
    {
        var people = new[] { new Person("Cleo") };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(people);
                d.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Person.Name))));
            })
            .Build();

        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];
        Assert.Equal("Cleo", ((Text)((Frame)Assert.Single(rows.Children)).Children[0]).Content);
    }
}
