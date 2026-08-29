using Pysar.Xaml.Model;
using Pysar.Xaml.Model.Tooling;
using Xunit;

namespace Pysar.Xaml.Model.Tests;

public class DataTypeHintTests
{
    private const string DesignerNamespaces =
        "xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\" " +
        "xmlns:vm=\"clr-namespace:App\" ";

    private static XamlObjectNode Parse(string xaml)
        => new XamlParser().Parse(xaml).Root;

    [Fact]
    public void Read_ReturnsType_FromDesignInstance()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "d:DataContext=\"{d:DesignInstance Type=vm:ReportViewModel}\" />");

        Assert.Equal("vm:ReportViewModel", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_ReturnsType_FromPositionalDesignInstance()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "d:DataContext=\"{d:DesignInstance vm:ReportViewModel}\" />");

        Assert.Equal("vm:ReportViewModel", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_IgnoresOtherDesignInstanceArguments()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "d:DataContext=\"{d:DesignInstance IsDesignTimeCreatable=True, Type=vm:ReportViewModel}\" />");

        Assert.Equal("vm:ReportViewModel", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_ReturnsEmpty_WhenDesignContextCleared()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "d:DataContext=\"\" />");

        Assert.Equal("", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_ReturnsNull_WhenAbsent()
    {
        var node = Parse("<Report xmlns=\"https://mriyalab.com/pysar\" />");

        Assert.Null(DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_ReturnsType_FromDirective()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "xmlns:vm=\"clr-namespace:App\" x:DataType=\"vm:ReportViewModel\" />");

        Assert.Equal("vm:ReportViewModel", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_ReturnsType_FromDirective_In2009Namespace()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2009/xaml\" " +
            "xmlns:vm=\"clr-namespace:App\" x:DataType=\"vm:ReportViewModel\" />");

        Assert.Equal("vm:ReportViewModel", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_DirectiveWins_OverDesignContext()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "d:DataContext=\"{d:DesignInstance Type=vm:Design}\" x:DataType=\"vm:Directive\" />");

        Assert.Equal("vm:Directive", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_EmptyDirectiveClears_EvenWithDesignContext()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "d:DataContext=\"{d:DesignInstance Type=vm:Design}\" x:DataType=\"\" />");

        Assert.Equal("", DataTypeHint.Read(node));
    }

    [Fact]
    public void Read_FallsBackToDesignContext_WhenDirectiveIsNotLiteral()
    {
        // {x:Type …} is not a form this dialect supports, so the directive is treated as absent
        // rather than as a clear — otherwise it would silently suppress validation of the subtree.
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "d:DataContext=\"{d:DesignInstance Type=vm:Design}\" x:DataType=\"{x:Type vm:Directive}\" />");

        Assert.Equal("vm:Design", DataTypeHint.Read(node));
    }

    [Fact]
    public void FindSource_ReturnsTheMemberReadTookTheValueFrom()
    {
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" " + DesignerNamespaces +
            "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" " +
            "d:DataContext=\"{d:DesignInstance Type=vm:Design}\" x:DataType=\"{x:Type vm:Directive}\" />");

        Assert.Same(DataTypeHint.FindDesignDataContext(node), DataTypeHint.FindSource(node));
    }

    [Fact]
    public void FindDesignDataContext_ReturnsNull_WhenOnlyRuntimeDataContextPresent()
    {
        // A plain DataContext="{Binding ...}" is a runtime binding, not a design-time hint.
        var node = Parse(
            "<Report xmlns=\"https://mriyalab.com/pysar\" DataContext=\"{Binding Customer}\" />");

        Assert.Null(DataTypeHint.FindDesignDataContext(node));
        Assert.Null(DataTypeHint.Read(node));
    }
}
