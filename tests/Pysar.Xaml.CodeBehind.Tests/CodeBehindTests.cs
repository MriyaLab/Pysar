using Pysar.Elements;
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
    public void ReportView_SourceBinding_CompilesAndBuildsTheTree()
    {
        // The deferred SetBinding line references an element local declared earlier in
        // InitializeComponent; if it fell out of scope this project would not compile.
        var view = new HeaderView { Title = "Invoice" };

        Assert.Same(view.CaptionField, view.Children[0]);
    }
}
