using Pysar.Core.Enums;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class LoadIntoTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private sealed class MyReport : Report { }

    [Fact]
    public void LoadInto_PopulatesExistingRoot_AndReturnsNames()
    {
        var report = new MyReport();
        var xaml = $"<Report {Root} x:Class=\"Whatever.MyReport\">" +
                   "<PageFormat Size=\"A4\" Orientation=\"Landscape\" Margin=\"30\"/>" +
                   "<PageHeaderBand x:Name=\"header\"/></Report>";

        var result = ReportXaml.LoadInto(report, xaml);

        Assert.Same(report, result.Root);
        Assert.Equal(Orientation.Landscape, report.PageFormat.Orientation);
        Assert.NotNull(report.PageHeader);
        Assert.True(result.Names.ContainsKey("header"));
        Assert.IsType<PageHeaderBand>(result.Names["header"]);
    }

    /// <summary>
    /// The Report-typed overloads are kept deliberately alongside the ReportObject ones: PreviewHost is
    /// bundled inside the IDE plugins and runs against the Pysar build of the user's project, so
    /// dropping them is a binary-breaking change that surfaces as "Method not found" at preview time.
    /// Do not "de-duplicate" them away.
    /// </summary>
    [Fact]
    public void LoadInto_ReportTypedOverload_BindsToTheReportOverloadAndWorks()
    {
        var report = new MyReport();
        var xaml = $"<Report {Root}><PageHeaderBand x:Name=\"header\"/></Report>";

        // Statically typed as Report — resolves to LoadInto(Report, string), which must delegate
        // rather than recurse.
        var result = ReportXaml.LoadInto(report, xaml);

        Assert.Same(report, result.Root);
        Assert.NotNull(report.PageHeader);
    }

    [Fact]
    public void LoadInto_ReportObjectTypedOverload_AcceptsAComponentRoot()
    {
        var view = new ReportView();
        var xaml = $"<ReportView {Root}><Text Content=\"hi\"/></ReportView>";

        var result = ReportXaml.LoadInto(view, xaml);

        Assert.Same(view, result.Root);
        Assert.Single(view.Children);
    }
}
