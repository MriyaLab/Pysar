using Pysar.Elements;
using Pysar.Elements.Base;
using Xunit;

namespace Pysar.Elements.Tests;

public class ReportViewTests
{
    [Fact]
    public void ReportView_IsAContainerWithResources()
    {
        var view = new ReportView();
        view.AddElement(new Text { Content = "hello" });

        Assert.Single(view.Children);
        Assert.NotNull(view.Resources);
        Assert.IsAssignableFrom<IResourceHost>(view);
        Assert.IsAssignableFrom<IResourceHost>(new Report());
    }

    [Fact]
    public void Clone_RepointsSourceBindingsAtTheClone()
    {
        var view = new ReportView { Name = "original" };
        var text = new Text();
        text.SetBinding(Text.ContentProperty, new Pysar.Binding.BindingInfo("Name", source: view));
        view.AddElement(text);

        var clone = (ReportView)view.Clone();
        clone.Name = "clone";

        var clonedText = Assert.IsType<Text>(clone.Children[0]);
        Assert.True(clonedText.TryGetBinding(Text.ContentProperty, out var binding));
        Assert.Same(clone, binding!.Source);

        new Pysar.Binding.BindingEngine().ResolveBindings(clone.Children, null);
        Assert.Equal("clone", clonedText.Content);
    }

    [Fact]
    public void Clone_RepointsSourceBindingsOnNestedDescendants()
    {
        var view = new ReportView { Name = "original" };
        var panel = new StackPanel();
        var text = new Text();
        text.SetBinding(Text.ContentProperty, new Pysar.Binding.BindingInfo("Name", source: view));
        panel.AddElement(text);
        view.AddElement(panel);

        var clone = (ReportView)view.Clone();
        clone.Name = "clone";

        var clonedPanel = Assert.IsType<StackPanel>(clone.Children[0]);
        var clonedText = Assert.IsType<Text>(clonedPanel.Children[0]);
        Assert.True(clonedText.TryGetBinding(Text.ContentProperty, out var binding));
        Assert.Same(clone, binding!.Source);
    }
}
