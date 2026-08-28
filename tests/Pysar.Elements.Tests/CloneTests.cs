using Pysar.Binding;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class CloneTests
{
    [Fact]
    public void Clone_CopiesValues_Independently()
    {
        var text = new Text { Content = "hello", Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(20)) };
        var clone = (Text)text.Clone();

        Assert.Equal("hello", clone.Content);
        Assert.Equal(text.Size, clone.Size);

        clone.Content = "changed";
        Assert.Equal("hello", text.Content); // original untouched
    }

    [Fact]
    public void Clone_PreservesPendingBindings_ResolvableAgainstContext()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "Name");
        var clone = (Text)text.Clone();

        new BindingEngine().ResolveBindings(clone, new { Name = "Ada" });

        Assert.Equal("Ada", clone.Content);
        Assert.Equal(string.Empty, text.Content); // original not resolved
    }

    [Fact]
    public void Clone_Container_DeepCopiesChildren()
    {
        var frame = new Frame();
        frame.AddElement(new Text { Content = "child" });
        var clone = (Frame)frame.Clone();

        Assert.Single(clone.Children);
        Assert.NotSame(frame.Children[0], clone.Children[0]);
        Assert.Equal("child", ((Text)clone.Children[0]).Content);
    }

    [Fact]
    public void Clone_Grid_CopiesDefinitionsAndAttachedProps()
    {
        var grid = new Grid();
        grid.WithColumnDefinitions(new ColumnDefinition(GridLength.Star(1)));
        grid.WithRowDefinitions(new RowDefinition(GridLength.Fixed(30)), new RowDefinition(GridLength.Fixed(40)));
        grid.AddElement(new Text { Content = "cell" }, row: 1, column: 0);

        var clone = (Grid)grid.Clone();

        Assert.Single(clone.ColumnDefinitions);
        Assert.Equal(2, clone.RowDefinitions.Count);
        Assert.NotSame(grid.ColumnDefinitions, clone.ColumnDefinitions); // independent list
        Assert.Equal(1, GridAttached.GetRow(clone.Children[0]));         // attached prop copied
    }

    [Fact]
    public void Clone_CopiesMinMaxSize()
    {
        var text = new Text();
        text.MinWidth = 10;
        text.MaxHeight = 30;
        var clone = (Text)text.Clone();
        Assert.Equal(text.MinSize, clone.MinSize);
        Assert.Equal(text.MaxSize, clone.MaxSize);
        clone.MinWidth = 99;
        Assert.Equal(MinMaxLength.Fixed(10), text.MinWidth);
    }
}
