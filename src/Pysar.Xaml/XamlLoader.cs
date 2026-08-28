using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Xaml.Model;

namespace Pysar.Xaml;

/// <summary>Coordinates parsing and runtime construction of a report object tree.</summary>
internal sealed class XamlLoader
{
    public XamlLoadResult Load(Stream xaml, ReportObject? existingRoot = null, string? baseDirectory = null)
    {
        var document = new XamlParser().Parse(xaml);
        var context = new XamlLoadContext(baseDirectory);
        var factory = new XamlObjectFactory(context);
        var root = factory.Build(document.Root, existingRoot);
        context.ApplyDeferredBindings(factory.BuildBindingInfo);
        return new XamlLoadResult(root, context.Names);
    }

    public ResourceDictionary LoadDictionary(Stream xaml, string baseDirectory)
    {
        var document = new XamlParser().Parse(xaml);
        if (document.Root.Type.LocalName != "ResourceDictionary")
            throw new XamlException(
                $"Resource dictionary root must be ResourceDictionary, got '{document.Root.Type.LocalName}'.");

        var context = new XamlLoadContext(baseDirectory);
        var factory = new XamlObjectFactory(context);
        return (ResourceDictionary)factory.Build(document.Root);
    }

    internal static object BuildStandalone(string xaml)
    {
        var document = new XamlParser().Parse(xaml);
        var context = new XamlLoadContext();
        var factory = new XamlObjectFactory(context);
        var root = factory.Build(document.Root);
        context.ApplyDeferredBindings(factory.BuildBindingInfo);
        return root;
    }
}
