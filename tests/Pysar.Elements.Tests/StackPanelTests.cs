using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Xunit;

namespace Pysar.Elements.Tests;

public class StackPanelTests
{
    [Fact]
    public void StackPanel_IsContainer_SupportsChildrenAndClear()
    {
        var stack = new StackPanel();
        stack.AddElement(new Text { Content = "a" });
        stack.AddElement(new Text { Content = "b" });
        Assert.Equal(2, stack.Children.Count);
        Assert.IsAssignableFrom<IReportContainer>(stack);

        stack.ClearElements();
        Assert.Empty(stack.Children);
    }

    [Fact]
    public void StackPanel_DefaultOrientation_IsVertical()
    {
        Assert.Equal(StackOrientation.Vertical, new StackPanel().Orientation);
    }

    [Fact]
    public void WithOrientation_SetsOrientation()
    {
        var stack = new StackPanel().WithOrientation(StackOrientation.Horizontal);
        Assert.Equal(StackOrientation.Horizontal, stack.Orientation);
    }

    [Fact]
    public void Clone_PreservesOrientation()
    {
        var stack = new StackPanel { Orientation = StackOrientation.Horizontal };
        var clone = (StackPanel)stack.Clone();
        Assert.Equal(StackOrientation.Horizontal, clone.Orientation);
    }

    [Fact]
    public void DefaultSpacing_IsZero()
        => Assert.Equal(0f, new StackPanel().Spacing);

    [Fact]
    public void WithSpacing_SetsSpacing()
        => Assert.Equal(12f, new StackPanel().WithSpacing(12).Spacing);

    [Fact]
    public void Clone_PreservesSpacing()
    {
        var clone = (StackPanel)new StackPanel { Spacing = 8 }.Clone();
        Assert.Equal(8f, clone.Spacing);
    }
}
