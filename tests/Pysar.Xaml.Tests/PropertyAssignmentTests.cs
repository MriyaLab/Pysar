using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class PropertyAssignmentTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void Assign_Literal_BackgroundColor()
    {
        var text = XamlTestHost.BuildElement<Text>($"<Text {Root} BackgroundColor=\"#ECECEC\" />");
        Assert.Equal(Color.FromHex("#ECECEC"), text.BackgroundColor);
    }

    [Fact]
    public void Assign_Literal_Size()
    {
        var text = XamlTestHost.BuildElement<Text>($"<Text {Root} Size=\"Fill,60\" />");
        Assert.True(text.Size.Width.IsFill);
        Assert.Equal(60f, text.Size.Height.Value);
    }

    [Fact]
    public void Assign_Literal_MinWidthMaxHeight()
    {
        var text = XamlTestHost.BuildElement<Text>(
            $"<Text {Root} MinWidth=\"10\" MaxHeight=\"40\" Content=\"x\" />");
        Assert.Equal(MinMaxLength.Fixed(10), text.MinWidth);
        Assert.Equal(MinMaxLength.Fixed(40), text.MaxHeight);
    }

    private sealed record Person(string Name);

    [Fact]
    public void Assign_Binding_ResolvesAfterBuild()
    {
        var text = XamlTestHost.BuildElement<Text>($"<Text {Root} Content=\"{{Binding Name}}\" />");
        text.DataContext = new Person("Ada");
        new Pysar.Binding.BindingEngine().ResolveBindings([text]);
        Assert.Equal("Ada", text.Content);
    }
}
