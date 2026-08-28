using Pysar.Elements;
using Xunit;

namespace Pysar.Binding.Tests;

public class ImageSourceBindingTests
{
    [Fact]
    public void ResolveBindings_FilePath_FromImageContext()
    {
        var source = new FileImageSource();
        source.SetBinding(FileImageSource.FilePathProperty, "LogoPath");
        var image = new Image { Source = source };

        new BindingEngine().ResolveBindings([image], new { LogoPath = "logo.png" });

        Assert.Equal("logo.png", source.FilePath);
    }

    [Fact]
    public void ResolveBindings_Uri_FromImageContext()
    {
        var source = new UriImageSource();
        source.SetBinding(UriImageSource.UriProperty, "LogoUri");
        var image = new Image { Source = source };
        var uri = new Uri("https://example.com/logo.png");

        new BindingEngine().ResolveBindings([image], new { LogoUri = uri });

        Assert.Equal(uri, source.Uri);
    }

    [Fact]
    public void ResolveBindings_ResourceName_FromImageContext()
    {
        var source = new ResourceImageSource();
        source.SetBinding(ResourceImageSource.ResourceNameProperty, "LogoResource");
        var image = new Image { Source = source };

        new BindingEngine().ResolveBindings([image], new { LogoResource = "App.logo.png" });

        Assert.Equal("App.logo.png", source.ResourceName);
    }
}
