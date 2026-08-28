using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class DataTypeIgnoredTests
{
    [Fact]
    public void Load_WithXDataType_DoesNotThrowAndLoadsRoot()
    {
        const string xaml =
            "<Report xmlns=\"https://mriyalab.com/pysar\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "x:DataType=\"System:String\">" +
            "<DetailBand><Text x:DataType=\"System:Int32\" Content=\"Hello\" /></DetailBand>" +
            "</Report>";

        var report = ReportXaml.Load(xaml);

        Assert.NotNull(report);
    }

    [Fact]
    public void Load_With2009XamlNamespace_RecognizesDirectives()
    {
        // The 2009 XAML namespace (which defines x:DataType) must be accepted for x: directives
        // just like 2006, so x:Name is captured (not assigned as a property) and load succeeds.
        const string xaml =
            "<Report xmlns=\"https://mriyalab.com/pysar\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\" " +
            "xmlns:vm=\"clr-namespace:App\" x:DataType=\"vm:ReportViewModel\">" +
            "<DetailBand><Text x:Name=\"Cell\" Content=\"Hi\" /></DetailBand>" +
            "</Report>";

        var result = ReportXaml.LoadInto(new Pysar.Elements.Report(), xaml);

        Assert.True(result.Names.ContainsKey("Cell"));
    }

    [Fact]
    public void Load_WithDesignTimeAttributes_IgnoresThem()
    {
        // d:DataContext / mc:Ignorable exist only for IDE language services; loading must ignore
        // them rather than trying to assign them (the markup extension is not a runtime one).
        const string xaml =
            "<Report xmlns=\"https://mriyalab.com/pysar\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\" " +
            "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" " +
            "xmlns:reports=\"clr-namespace:App.Reports\" " +
            "mc:Ignorable=\"d\" " +
            "d:DataContext=\"{d:DesignInstance Type=reports:Invoice}\">" +
            "<DetailBand><Text Content=\"Hi\" /></DetailBand>" +
            "</Report>";

        var report = ReportXaml.Load(xaml);

        Assert.NotNull(report);
        Assert.Null(report.DataContext);
    }

    [Fact]
    public void Load_WithDataTypeOnlyClrNamespace_MissingAssembly_DoesNotThrow()
    {
        // A clr-namespace declared only so x:DataType can name a design-time type must not
        // force runtime assembly resolution: it is never used to instantiate anything.
        const string xaml =
            "<Report xmlns=\"https://mriyalab.com/pysar\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "xmlns:reports=\"clr-namespace:App.Reports\" " +
            "x:DataType=\"reports:Invoice\" />";

        var report = ReportXaml.Load(xaml);

        Assert.NotNull(report);
    }

    [Fact]
    public void Load_WithDesignDataContextOnNestedElement_IgnoresIt()
    {
        // Design-time hints may appear on any element; none of them reach the runtime tree.
        const string xaml =
            "<Report xmlns=\"https://mriyalab.com/pysar\" " +
            "xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\" " +
            "xmlns:reports=\"clr-namespace:App.Reports\">" +
            "<DetailBand DataSource=\"{Binding Items}\">" +
            "<Grid d:DataContext=\"{d:DesignInstance Type=reports:InvoiceItem}\" />" +
            "</DetailBand>" +
            "</Report>";

        var report = ReportXaml.Load(xaml);

        Assert.NotNull(report);
    }
}
