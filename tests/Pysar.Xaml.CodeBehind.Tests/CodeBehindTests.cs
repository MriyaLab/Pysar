using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.CodeBehind.Tests;

public class CodeBehindTests
{
    private sealed record Row(string Value);
    private sealed record Vm(IReadOnlyList<Row> Rows);

    [Fact]
    public void GeneratedComponent_LoadsXaml_PopulatesFields_BindsAndBuilds()
    {
        var report = new SalesReport { DataContext = new Vm(new[] { new Row("x0"), new Row("x1") }) };

        Assert.NotNull(report.HeaderField);
        Assert.IsType<PageHeaderBand>(report.HeaderField);

        report.Build();

        var rows = (StackPanel)((StackPanel)report.Detail.Children[0]).Children[0];
        Assert.Equal(2, rows.Children.Count);
        Assert.Equal("x0", ((Text)((Frame)rows.Children[0]).Children[0]).Content);
    }

    [Fact]
    public void ThemedReport_FallbackPath_AppliesImplicitStyle()
    {
        var r = new ThemedReport();
        r.Build();
        Assert.Equal(13f, r.LabelField.FontSize);   // implicit style applied via runtime fallback (resources)
    }

    [Fact]
    public void GeneratedTree_MatchesRuntimeLoad_AfterBuild()
    {
        var vm = new Vm(new[] { new Row("x0"), new Row("x1") });
        var generated = new SalesReport { DataContext = vm };
        generated.Build();

        var loaded = ReportXaml.Load(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SalesReport.rxaml")));
        loaded.DataContext = vm;
        loaded.Build();

        Assert.Equal(generated.PageFormat.Size, loaded.PageFormat.Size);
        Assert.Equal(generated.PageHeader!.BackgroundColor, loaded.PageHeader!.BackgroundColor);

        static string Cell(Report report)
        {
            var rows = (StackPanel)((StackPanel)report.Detail.Children[0]).Children[0];
            return ((Text)((Frame)rows.Children[0]).Children[0]).Content;
        }

        Assert.Equal(Cell(generated), Cell(loaded));
        Assert.Equal("x0", Cell(generated));
    }

    [Fact]
    public void ReportView_SourceBinding_CompilesAndBuildsTheTree()
    {
        // The deferred SetBinding line references an element local declared earlier in
        // InitializeComponent; if it fell out of scope this project would not compile.
        var view = new HeaderView { Title = "Invoice" };

        Assert.Same(view.CaptionField, view.Children[0]);
    }
}
