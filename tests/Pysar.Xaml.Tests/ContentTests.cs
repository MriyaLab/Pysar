using Pysar.Core.Enums;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ContentTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\"";

    [Fact]
    public void Container_ChildElements_GoToChildren()
    {
        var panel = XamlTestHost.BuildElement<StackPanel>(
            $"<StackPanel {Root}><Text Content=\"a\"/><Text Content=\"b\"/></StackPanel>");
        Assert.Equal(2, panel.Children.Count);
        Assert.Equal("a", ((Text)panel.Children[0]).Content);
    }

    [Fact]
    public void Text_InnerText_GoesToContent()
    {
        var text = XamlTestHost.BuildElement<Text>($"<Text {Root}>Hello</Text>");
        Assert.Equal("Hello", text.Content);
    }

    [Fact]
    public void Grid_AttachedRowColumn_Set()
    {
        var grid = XamlTestHost.BuildElement<Grid>(
            $"<Grid {Root}><Frame Grid.Row=\"1\" Grid.Column=\"2\"/></Grid>");
        var frame = grid.Children[0];
        Assert.Equal(1, GridAttached.GetRow(frame));
        Assert.Equal(2, GridAttached.GetColumn(frame));
        Assert.Equal(1, Grid.GetRow(frame));
        Assert.Equal(2, Grid.GetColumn(frame));
    }

    [Fact]
    public void StackPanel_OrientationAttribute_Parsed()
    {
        var panel = XamlTestHost.BuildElement<StackPanel>(
            $"<StackPanel {Root} Orientation=\"Horizontal\"/>");
        Assert.Equal(StackOrientation.Horizontal, panel.Orientation);
    }

    [Fact]
    public void StackPanel_SpacingAttribute_Parsed()
    {
        var panel = XamlTestHost.BuildElement<StackPanel>(
            $"<StackPanel {Root} Spacing=\"12\"/>");
        Assert.Equal(12f, panel.Spacing);
    }
}
