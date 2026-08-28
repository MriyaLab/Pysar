using Pysar.Binding;
using Pysar.Elements;
using Xunit;

namespace Pysar.Xaml.Tests;

public class BindingSourceTests
{
    private const string Root =
        "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void Source_ForwardReferenceToRoot_Resolves()
    {
        // 'root' names the element that CONTAINS the binding's target, so this can only work
        // because source bindings are applied after the whole tree is built.
        var view = XamlTestHost.BuildElement<ReportView>(
            $"<ReportView {Root} x:Name=\"root\" Name=\"Header\">" +
            "<Text Content=\"{Binding Name, Source={x:Reference root}}\"/></ReportView>");

        var text = Assert.IsType<Text>(view.Children[0]);
        Assert.True(text.TryGetBinding(Text.ContentProperty, out var binding));
        Assert.Same(view, binding!.Source);

        new BindingEngine().ResolveBindings(view.Children, null);
        Assert.Equal("Header", text.Content);
    }

    [Fact]
    public void Source_UnknownName_Throws()
    {
        var exception = Assert.Throws<XamlException>(() => XamlTestHost.BuildElement<ReportView>(
            $"<ReportView {Root}><Text Content=\"{{Binding Name, Source={{x:Reference nope}}}}\"/></ReportView>"));

        Assert.Contains("nope", exception.Message);
    }
}
