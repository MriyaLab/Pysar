using Pysar.Xaml.Model;
using Xunit;

namespace Pysar.Xaml.Model.Tests;

public class XamlParserTests
{
    [Fact]
    public void Parse_CapturesNamespacesObjectsMembersAndLocations()
    {
        const string xaml = """
                            <Report xmlns="https://mriyalab.com/pysar"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                              <Detail x:Name="Rows" DataSource="{Binding Items}">
                                <Text Grid.Row="1" Content="{Binding Name}" />
                              </Detail>
                            </Report>
                            """;

        var document = new XamlParser().Parse(xaml);

        Assert.Equal("Report", document.Root.Type.LocalName);
        Assert.Equal(2, document.Namespaces.Count);
        var detail = Assert.Single(document.Root.Children);
        Assert.Equal("Detail", detail.Type.LocalName);
        Assert.Contains(detail.Members, member =>
            member.Kind == XamlMemberKind.Directive && member.Name.LocalName == "Name");
        var dataSource = Assert.Single(
            detail.Members,
            member => member.Name.LocalName == "DataSource");
        Assert.Equal("Items", Assert.IsType<XamlBindingNode>(dataSource.Value).Path);

        var text = Assert.Single(detail.Children);
        var row = Assert.Single(
            text.Members,
            member => member.Kind == XamlMemberKind.AttachedProperty);
        Assert.Equal("Grid", row.Name.OwnerName);
        Assert.Equal("Row", row.Name.LocalName);
        Assert.True(text.Span.Line > 0);
        Assert.True(text.Span.Column > 0);
    }

    [Fact]
    public void Parse_ClassifiesPropertyElementsSeparatelyFromContentChildren()
    {
        const string xaml = """
                            <Text xmlns="https://mriyalab.com/pysar">
                              <Text.Font Family="Ubuntu" />
                            </Text>
                            """;

        var root = new XamlParser().Parse(xaml).Root;

        Assert.Empty(root.Children);
        var font = Assert.Single(root.Members);
        Assert.Equal(XamlMemberKind.PropertyElement, font.Kind);
        Assert.Equal("Text", font.Name.OwnerName);
        Assert.Equal("Font", font.Name.LocalName);
        var value = Assert.Single(font.Objects);
        Assert.Equal("Text.Font", value.Type.LocalName);
        Assert.Contains(value.Members, member => member.Name.LocalName == "Family");
    }

    [Fact]
    public void Parse_PreservesTextContent()
    {
        const string xaml = """<Text xmlns="https://mriyalab.com/pysar">Hello report</Text>""";

        var root = new XamlParser().Parse(xaml).Root;

        Assert.Equal("Hello report", root.TextContent);
    }
}
