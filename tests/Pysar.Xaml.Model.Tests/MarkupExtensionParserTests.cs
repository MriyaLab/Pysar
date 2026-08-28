using Pysar.Xaml.Model;
using Xunit;

namespace Pysar.Xaml.Model.Tests;

public class MarkupExtensionParserTests
{
    private static readonly XamlSourceSpan Span = new(3, 7);

    [Fact]
    public void Parse_Literal_ReturnsLiteral()
    {
        var literal = Assert.IsType<XamlLiteralNode>(MarkupExtensionParser.Parse("Hello", Span));

        Assert.Equal("Hello", literal.Text);
        Assert.Equal(3, literal.Span.Line);
        Assert.Equal(7, literal.Span.Column);
    }

    [Fact]
    public void Parse_BindingWithQuotedFormat_PreservesNestedMarkupCharacters()
    {
        var binding = Assert.IsType<XamlBindingNode>(
            MarkupExtensionParser.Parse(
                "{Binding Path=Total, StringFormat='Total: {0:N2}, USD'}",
                Span));

        Assert.Equal("Total", binding.Path);
        Assert.Equal("Total: {0:N2}, USD", binding.StringFormat);
    }

    [Fact]
    public void Parse_StaticResource_ReturnsResourceKey()
    {
        var resource = Assert.IsType<XamlStaticResourceNode>(
            MarkupExtensionParser.Parse("{StaticResource BrandColor}", Span));

        Assert.Equal("BrandColor", resource.Key);
    }

    [Fact]
    public void Parse_UnsupportedMarkup_PreservesOriginalText()
    {
        var markup = Assert.IsType<XamlUnknownMarkupExtensionNode>(
            MarkupExtensionParser.Parse("{x:Static Colors.Red}", Span));

        Assert.Equal("{x:Static Colors.Red}", markup.Text);
    }

    [Fact]
    public void Binding_WithReferenceSource_CapturesSourceName()
    {
        var markup = "{Binding Title, Source={x:Reference root}}";
        var binding = Assert.IsType<XamlBindingNode>(
            MarkupExtensionParser.Parse(markup, new XamlSourceSpan(0, markup.Length)));

        Assert.Equal("Title", binding.Path);
        Assert.Equal("root", binding.SourceName);
    }

    [Fact]
    public void Binding_WithReferenceSourceAndStringFormat_CapturesBoth()
    {
        var markup = "{Binding Total, Source={x:Reference root}, StringFormat='{0:C}'}";
        var binding = Assert.IsType<XamlBindingNode>(
            MarkupExtensionParser.Parse(markup, new XamlSourceSpan(0, markup.Length)));

        Assert.Equal("root", binding.SourceName);
        Assert.Equal("{0:C}", binding.StringFormat);
    }

    [Fact]
    public void Binding_WithoutSource_LeavesSourceNameNull()
    {
        var markup = "{Binding Title}";
        var binding = Assert.IsType<XamlBindingNode>(
            MarkupExtensionParser.Parse(markup, new XamlSourceSpan(0, markup.Length)));

        Assert.Null(binding.SourceName);
    }
}
