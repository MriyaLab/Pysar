using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ImageSourceXamlTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\"";

    [Fact]
    public void PropertyElement_FileImageSource_SetsFilePath()
    {
        var image = XamlTestHost.BuildElement<Image>(
            $"<Image {Root}><Image.Source><FileImageSource FilePath=\"logo.png\"/></Image.Source></Image>");

        var source = Assert.IsType<FileImageSource>(image.Source);
        Assert.Equal("logo.png", source.FilePath);
    }

    [Fact]
    public void PropertyElement_UriImageSource_SetsUri()
    {
        var image = XamlTestHost.BuildElement<Image>(
            $"<Image {Root}><Image.Source><UriImageSource Uri=\"https://example.com/a.png\"/></Image.Source></Image>");

        var source = Assert.IsType<UriImageSource>(image.Source);
        Assert.Equal(new Uri("https://example.com/a.png"), source.Uri);
    }

    [Fact]
    public void PropertyElement_ResourceImageSource_SetsResourceName()
    {
        var image = XamlTestHost.BuildElement<Image>(
            $"<Image {Root}><Image.Source><ResourceImageSource ResourceName=\"App.logo.png\"/></Image.Source></Image>");

        var source = Assert.IsType<ResourceImageSource>(image.Source);
        Assert.Equal("App.logo.png", source.ResourceName);
    }

    [Fact]
    public void PropertyElement_FilePathBinding_ResolvesFromImageContext()
    {
        var image = XamlTestHost.BuildElement<Image>(
            $"<Image {Root}><Image.Source><FileImageSource FilePath=\"{{Binding LogoPath}}\"/></Image.Source></Image>");

        image.DataContext = new { LogoPath = "bound.png" };
        new Pysar.Binding.BindingEngine().ResolveBindings([image]);

        Assert.Equal("bound.png", Assert.IsType<FileImageSource>(image.Source).FilePath);
    }
}
