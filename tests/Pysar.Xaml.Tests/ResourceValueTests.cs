using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ResourceValueTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void ValueResource_ReferencedByStaticResource()
    {
        var xaml = $"<Report {Root}>" +
                   "<Report.Resources><Color x:Key=\"Brand\">#2C3E50</Color></Report.Resources>" +
                   "<PageHeaderBand BackgroundColor=\"{StaticResource Brand}\"/>" +
                   "</Report>";
        var design = ReportXaml.Load(xaml);
        Assert.Equal(Color.FromHex("#2C3E50"), design.PageHeader!.BackgroundColor);
        Assert.Equal(Color.FromHex("#2C3E50"), design.Resources["Brand"]);
    }

    [Fact]
    public void UnknownStaticResource_Throws()
    {
        var xaml = $"<Report {Root}><PageHeaderBand BackgroundColor=\"{{StaticResource Nope}}\"/></Report>";
        Assert.Throws<XamlException>(() => ReportXaml.Load(xaml));
    }

    [Fact]
    public void ExternalResourceDictionary_ReferencedByStaticResource()
    {
        var directory = CreateTempDirectory();
        var resourcesPath = Path.Combine(directory, "InvoiceReport.Resources.xaml");
        var reportPath = Path.Combine(directory, "InvoiceReport.xaml");
        File.WriteAllText(
            resourcesPath,
            $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#2C3E50</Color></ResourceDictionary>");
        File.WriteAllText(
            reportPath,
            $"<Report {Root}>"
            + "<Report.Resources><ResourceDictionary Source=\"InvoiceReport.Resources.xaml\" /></Report.Resources>"
            + "<PageHeaderBand BackgroundColor=\"{StaticResource Brand}\"/>"
            + "</Report>");

        var design = ReportXaml.LoadFile(reportPath);

        Assert.Equal(Color.FromHex("#2C3E50"), design.PageHeader!.BackgroundColor);
        Assert.Equal(Color.FromHex("#2C3E50"), design.Resources["Brand"]);
    }

    [Fact]
    public void ExternalResourceDictionary_StyleCanReferencePreviousExternalValue()
    {
        var directory = CreateTempDirectory();
        var resourcesPath = Path.Combine(directory, "InvoiceReport.Resources.xaml");
        var reportPath = Path.Combine(directory, "InvoiceReport.xaml");
        File.WriteAllText(
            resourcesPath,
            $"<ResourceDictionary {Root}>"
            + "<Color x:Key=\"Brand\">#2C3E50</Color>"
            + "<Style x:Key=\"Heading\" TargetType=\"Text\">"
            + "<Setter Member=\"FontColor\" Value=\"{StaticResource Brand}\"/>"
            + "</Style>"
            + "</ResourceDictionary>");
        File.WriteAllText(
            reportPath,
            $"<Report {Root}>"
            + "<Report.Resources><ResourceDictionary Source=\"InvoiceReport.Resources.xaml\" /></Report.Resources>"
            + "<PageHeaderBand><Text x:Name=\"Title\" Style=\"{StaticResource Heading}\" /></PageHeaderBand>"
            + "</Report>");

        var design = ReportXaml.LoadFile(reportPath);
        var title = Assert.IsType<Text>(Assert.Single(design.PageHeader!.Children));

        Assert.Equal(Color.FromHex("#2C3E50"), title.Font.Color);
    }

    [Fact]
    public void LoadInto_WithBaseDirectory_ResolvesExternalResourceDictionary()
    {
        var directory = CreateTempDirectory();
        File.WriteAllText(
            Path.Combine(directory, "Report.Resources.xaml"),
            $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#2C3E50</Color></ResourceDictionary>");
        var xaml = $"<Report {Root}>"
                   + "<Report.Resources><ResourceDictionary Source=\"Report.Resources.xaml\" /></Report.Resources>"
                   + "<PageHeaderBand BackgroundColor=\"{StaticResource Brand}\"/>"
                   + "</Report>";
        var report = new Report();

        ReportXaml.LoadInto(report, xaml, directory);

        Assert.Equal(Color.FromHex("#2C3E50"), report.PageHeader!.BackgroundColor);
    }

    [Fact]
    public void MergedDictionaries_LoadBeforeLocalResources()
    {
        var directory = CreateTempDirectory();
        var colorsPath = Path.Combine(directory, "Colors.xaml");
        var stylesPath = Path.Combine(directory, "Styles.xaml");
        var reportPath = Path.Combine(directory, "Report.xaml");
        File.WriteAllText(
            colorsPath,
            $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#2C3E50</Color></ResourceDictionary>");
        File.WriteAllText(
            stylesPath,
            $"<ResourceDictionary {Root}>"
            + "<Style x:Key=\"Heading\" TargetType=\"Text\">"
            + "<Setter Member=\"FontColor\" Value=\"{StaticResource Brand}\"/>"
            + "</Style>"
            + "</ResourceDictionary>");
        File.WriteAllText(
            reportPath,
            $"<Report {Root}>"
            + "<Report.Resources>"
            + "<ResourceDictionary>"
            + "<ResourceDictionary.MergedDictionaries>"
            + "<ResourceDictionary Source=\"Colors.xaml\" />"
            + "<ResourceDictionary Source=\"Styles.xaml\" />"
            + "</ResourceDictionary.MergedDictionaries>"
            + "</ResourceDictionary>"
            + "</Report.Resources>"
            + "<PageHeaderBand><Text Style=\"{StaticResource Heading}\" /></PageHeaderBand>"
            + "</Report>");

        var design = ReportXaml.LoadFile(reportPath);
        var title = Assert.IsType<Text>(Assert.Single(design.PageHeader!.Children));

        Assert.Equal(Color.FromHex("#2C3E50"), title.Font.Color);
    }

    [Fact]
    public void ResourceDictionary_LocalResourceOverridesMergedResource()
    {
        var directory = CreateTempDirectory();
        var colorsPath = Path.Combine(directory, "Colors.xaml");
        var reportPath = Path.Combine(directory, "Report.xaml");
        File.WriteAllText(
            colorsPath,
            $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#2C3E50</Color></ResourceDictionary>");
        File.WriteAllText(
            reportPath,
            $"<Report {Root}>"
            + "<Report.Resources>"
            + "<ResourceDictionary>"
            + "<ResourceDictionary.MergedDictionaries>"
            + "<ResourceDictionary Source=\"Colors.xaml\" />"
            + "</ResourceDictionary.MergedDictionaries>"
            + "<Color x:Key=\"Brand\">#A9AEBA</Color>"
            + "</ResourceDictionary>"
            + "</Report.Resources>"
            + "<PageHeaderBand BackgroundColor=\"{StaticResource Brand}\"/>"
            + "</Report>");

        var design = ReportXaml.LoadFile(reportPath);

        Assert.Equal(Color.FromHex("#A9AEBA"), design.PageHeader!.BackgroundColor);
    }

    [Fact]
    public void TriggerSetter_CanUseStaticResource()
    {
        var xaml = $"<Report {Root}>"
                   + "<Report.Resources><Color x:Key=\"Highlight\">#F0F1F5</Color></Report.Resources>"
                   + "<DetailBand>"
                   + "<Text x:Name=\"Cell\" Content=\"Row\">"
                   + "<Text.Triggers>"
                   + "<DataTrigger Binding=\"UnitPrice\" CompareType=\"GreaterThanOrEqual\" Value=\"20\">"
                   + "<Setter Member=\"BackgroundColor\" Value=\"{StaticResource Highlight}\" />"
                   + "</DataTrigger>"
                   + "</Text.Triggers>"
                   + "</Text>"
                   + "</DetailBand>"
                   + "</Report>";
        var design = ReportXaml.Load(xaml);
        design.DataContext = new { UnitPrice = 25 };

        design.Build();

        var cell = Assert.IsType<Text>(Assert.Single(design.Detail.Children));
        Assert.Equal(Color.FromHex("#F0F1F5"), cell.BackgroundColor);
    }

    [Fact]
    public void ResourceDictionarySource_WithoutFileBase_Throws()
    {
        var xaml = $"<Report {Root}>"
                   + "<Report.Resources><ResourceDictionary Source=\"InvoiceReport.Resources.xaml\" /></Report.Resources>"
                   + "</Report>";

        var exception = Assert.Throws<XamlException>(() => ReportXaml.Load(xaml));
        Assert.Contains("ReportXaml.LoadFile", exception.Message);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "palmy-pysar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
