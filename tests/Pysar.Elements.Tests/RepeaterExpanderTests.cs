using System.Collections;
using Xunit;

namespace Pysar.Elements.Tests;

public class RepeaterExpanderTests
{
    private sealed record Person(string Name);

    [Fact]
    public void Build_WithData_TypedBuilder_BuildsPerRecordAndNestsDirectGroup()
    {
        var categories = new[]
        {
            new Category("Fruit", new[] { new Product("Apple"), new Product("Pear") }),
            new Category("Veg", new[] { new Product("Kale") }),
        };

        var design = ReportBuilder.Create("t")
            .WithDetail(d => d.WithData(categories, (category, row) => row
                .AddElement(new Text { Content = category.Name })          // real typed value, no binding
                .AddGroup(category.Products, g => g                        // direct collection, no path
                    .AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Product.Title)))))))
            .Build();

        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];
        Assert.Equal(2, rows.Children.Count);

        // Row 0 = Fruit: builder-built StackPanel [ Text("Fruit"), nested group stack ]
        var row0 = Assert.IsType<StackPanel>(rows.Children[0]);
        Assert.Equal("Fruit", ((Text)row0.Children[0]).Content);
        var nested0 = (StackPanel)((StackPanel)row0.Children[1]).Children[0];
        Assert.Equal(2, nested0.Children.Count);
        Assert.Equal("Apple", ((Text)((Frame)nested0.Children[0]).Children[0]).Content);

        var row1 = Assert.IsType<StackPanel>(rows.Children[1]);
        Assert.Equal("Veg", ((Text)row1.Children[0]).Content);
        var nested1 = (StackPanel)((StackPanel)row1.Children[1]).Children[0];
        Assert.Single(nested1.Children);
        Assert.Equal("Kale", ((Text)((Frame)nested1.Children[0]).Children[0]).Content);
    }

    [Fact]
    public void Build_WithData_ThreeLevelTypedBuilders()
    {
        var regions = new[]
        {
            new Region("North", new[]
            {
                new Category("Fruit", new[] { new Product("Apple"), new Product("Pear") }),
            }),
        };

        var design = ReportBuilder.Create("t")
            .WithDetail(d => d.WithData(regions, (region, rRow) => rRow
                .AddElement(new Text { Content = region.Name })
                .AddGroup(region.Categories, (cat, cRow) => cRow
                    .AddElement(new Text { Content = cat.Name })
                    .AddGroup(cat.Products, (prod, pRow) => pRow
                        .AddElement(new Text { Content = prod.Title })))))
            .Build();

        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];
        var regionRow = Assert.IsType<StackPanel>(rows.Children[0]);
        Assert.Equal("North", ((Text)regionRow.Children[0]).Content);

        var catRows = (StackPanel)((StackPanel)regionRow.Children[1]).Children[0];
        var catRow = Assert.IsType<StackPanel>(catRows.Children[0]);
        Assert.Equal("Fruit", ((Text)catRow.Children[0]).Content);

        var prodRows = (StackPanel)((StackPanel)catRow.Children[1]).Children[0];
        Assert.Equal(2, prodRows.Children.Count);
        Assert.Equal("Apple", ((Text)((StackPanel)prodRows.Children[0]).Children[0]).Content);
        Assert.Equal("Pear", ((Text)((StackPanel)prodRows.Children[1]).Children[0]).Content);
    }

    [Fact]
    public void Build_AddGroup_ExpandsNestedGroup()
    {
        var categories = new[] { new Category("Fruit", new[] { new Product("Apple"), new Product("Pear") }) };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(categories);
                d.AddElement(new StackPanel()
                    .AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Category.Name))))
                    .AddGroup(nameof(Category.Products), g => g
                        .AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Product.Title))))));
            })
            .Build();

        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];
        var wrapper = (StackPanel)((Frame)rows.Children[0]).Children[0];
        Assert.Equal("Fruit", ((Text)wrapper.Children[0]).Content);
        var nestedRows = (StackPanel)((StackPanel)wrapper.Children[1]).Children[0];
        Assert.Equal(2, nestedRows.Children.Count);
        Assert.Equal("Apple", ((Text)((Frame)nestedRows.Children[0]).Children[0]).Content);
        Assert.Equal("Pear", ((Text)((Frame)nestedRows.Children[1]).Children[0]).Content);
    }

    [Fact]
    public void WithDataSource_SetsDataSource()
    {
        var items = new[] { 1, 2, 3 };
        var rep = new Repeater().WithDataSource(items);
        Assert.Same((IEnumerable)items, rep.DataSource);
    }

    [Fact]
    public void WithDataSourcePath_SetsPath()
    {
        var rep = new Repeater().WithDataSourcePath("Products");
        Assert.Equal("Products", rep.DataSourcePath);
    }

    [Fact]
    public void WithHeaderAndFooter_CreateFrames()
    {
        var rep = new Repeater()
            .WithHeader(h => h.AddElement(new Text { Content = "H" }))
            .WithFooter(f => f.AddElement(new Text { Content = "F" }));
        Assert.NotNull(rep.Header);
        Assert.NotNull(rep.Footer);
    }

    [Fact]
    public void Clone_DeepCopiesHeaderFooterAndPath()
    {
        var rep = new Repeater()
            .WithDataSourcePath("Products")
            .WithHeader(h => h.AddElement(new Text { Content = "H" }));
        rep.AddElement(new Text { Content = "row" });

        var clone = (Repeater)rep.Clone();

        Assert.Equal("Products", clone.DataSourcePath);
        Assert.NotNull(clone.Header);
        Assert.NotSame(rep.Header, clone.Header);          // deep copy, not shared
        Assert.Single(clone.Children);
    }

    private sealed record Category(string Name, IEnumerable<Product> Products);
    private sealed record Product(string Title);
    private sealed record Region(string Name, IEnumerable<Category> Categories);

    private static Repeater NestedProductRepeater() =>
        new Repeater()
            .WithDataSourcePath(nameof(Category.Products))
            .Also(r => r.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Product.Title)))));

    [Fact]
    public void Build_NestedRepeater_ExpandsChildCollectionPerMaster()
    {
        var categories = new[]
        {
            new Category("Fruit", new[] { new Product("Apple"), new Product("Pear") }),
            new Category("Veg", new[] { new Product("Kale") }),
        };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(categories);
                d.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Category.Name))));
                d.AddElement(NestedProductRepeater());
            })
            .Build();

        var outer = Assert.IsType<StackPanel>(Assert.Single(design.Detail.Children));
        var rows = Assert.IsType<StackPanel>(Assert.Single(outer.Children));
        Assert.Equal(2, rows.Children.Count);

        var row0 = (Frame)rows.Children[0];
        Assert.Equal("Fruit", ((Text)row0.Children[0]).Content);
        var nestedOuter0 = Assert.IsType<StackPanel>(row0.Children[1]);
        var nestedRows0 = Assert.IsType<StackPanel>(Assert.Single(nestedOuter0.Children));
        Assert.Equal(2, nestedRows0.Children.Count);
        Assert.Equal("Apple", ((Text)((Frame)nestedRows0.Children[0]).Children[0]).Content);
        Assert.Equal("Pear", ((Text)((Frame)nestedRows0.Children[1]).Children[0]).Content);

        var row1 = (Frame)rows.Children[1];
        var nestedRows1 = (StackPanel)((StackPanel)row1.Children[1]).Children[0];
        Assert.Single(nestedRows1.Children);
        Assert.Equal("Kale", ((Text)((Frame)nestedRows1.Children[0]).Children[0]).Content);
    }

    [Fact]
    public void Build_TwoLevelNesting_ResolvesAllContexts()
    {
        var regions = new[]
        {
            new Region("North", new[]
            {
                new Category("Fruit", new[] { new Product("Apple") }),
            }),
        };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(regions);
                d.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Region.Name))));
                d.AddElement(new Repeater()
                    .WithDataSourcePath(nameof(Region.Categories))
                    .Also(cr =>
                    {
                        cr.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Category.Name))));
                        cr.AddElement(NestedProductRepeater());
                    }));
            })
            .Build();

        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];
        var regionRow = (Frame)rows.Children[0];
        Assert.Equal("North", ((Text)regionRow.Children[0]).Content);

        var catRows = (StackPanel)((StackPanel)regionRow.Children[1]).Children[0];
        var catRow = (Frame)catRows.Children[0];
        Assert.Equal("Fruit", ((Text)catRow.Children[0]).Content);

        var prodRows = (StackPanel)((StackPanel)catRow.Children[1]).Children[0];
        Assert.Equal("Apple", ((Text)((Frame)prodRows.Children[0]).Children[0]).Content);
    }

    [Fact]
    public void Build_NestedRepeater_EmptyCollection_StillEmitsHeaderFooter()
    {
        var categories = new[] { new Category("Empty", Array.Empty<Product>()) };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(categories);
                d.AddElement(new Repeater()
                    .WithDataSourcePath(nameof(Category.Products))
                    .WithHeader(h => h.AddElement(new Text { Content = "Title" }))
                    .WithFooter(f => f.AddElement(new Text { Content = "End" }))
                    .Also(r => r.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Product.Title))))));
            })
            .Build();

        var row0 = (Frame)((StackPanel)((StackPanel)design.Detail.Children[0]).Children[0]).Children[0];
        var nestedOuter = (StackPanel)row0.Children[0];   // [header, rows(empty), footer]
        Assert.Equal(3, nestedOuter.Children.Count);
        Assert.Equal("Title", ((Text)((Frame)nestedOuter.Children[0]).Children[0]).Content);
        Assert.Empty(((StackPanel)nestedOuter.Children[1]).Children);
        Assert.Equal("End", ((Text)((Frame)nestedOuter.Children[2]).Children[0]).Content);
    }

    [Fact]
    public void Build_NestedRepeater_HeaderBindsToMasterContext()
    {
        var categories = new[] { new Category("Fruit", new[] { new Product("Apple") }) };

        var design = ReportBuilder.Create("t")
            .WithDetail(d =>
            {
                d.WithDataSource(categories);
                d.AddElement(new Repeater()
                    .WithDataSourcePath(nameof(Category.Products))
                    .WithHeader(h => h.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Category.Name)))))
                    .Also(r => r.AddElement(new Text().Also(t => t.SetBinding(Text.ContentProperty, nameof(Product.Title))))));
            })
            .Build();

        var row0 = (Frame)((StackPanel)((StackPanel)design.Detail.Children[0]).Children[0]).Children[0];
        var nestedOuter = (StackPanel)row0.Children[0];
        var headerText = (Text)((Frame)nestedOuter.Children[0]).Children[0];
        Assert.Equal("Fruit", headerText.Content);   // resolved against the master Category
    }
}
