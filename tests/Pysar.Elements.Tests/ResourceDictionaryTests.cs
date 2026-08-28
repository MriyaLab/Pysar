using Xunit;

namespace Pysar.Elements.Tests;

public class ResourceDictionaryTests
{
    [Fact]
    public void ResourceDictionary_IsCompatibleWithStandardXamlResourceDictionary()
    {
        Assert.IsAssignableFrom<System.Windows.ResourceDictionary>(new ResourceDictionary());
    }

    [Fact]
    public void StandardResourceDictionary_ExposesWpfStyleResourceShape()
    {
        var dictionary = new System.Windows.ResourceDictionary();
        var merged = new System.Windows.ResourceDictionary();

        dictionary.Source = new Uri("Styles/ReportColors.xaml", UriKind.Relative);
        dictionary.MergedDictionaries.Add(merged);
        dictionary["DarkGray"] = "value";

        Assert.Equal("Styles/ReportColors.xaml", dictionary.Source.OriginalString);
        Assert.Same(merged, Assert.Single(dictionary.MergedDictionaries));
        Assert.Equal("value", dictionary["DarkGray"]);
    }

    [Fact]
    public void StaticResourceExtension_ExposesResourceKey()
    {
        var extension = new System.Windows.StaticResourceExtension("DarkGray");

        Assert.Equal("DarkGray", extension.ResourceKey);
    }
}
