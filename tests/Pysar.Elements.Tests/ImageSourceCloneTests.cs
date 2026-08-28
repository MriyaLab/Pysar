using Pysar.Binding;
using Xunit;

namespace Pysar.Elements.Tests;

public class ImageSourceCloneTests
{
    [Fact]
    public void FileImageSource_Parameterless_DefaultsToEmptyPath()
    {
        var source = new FileImageSource();
        Assert.Equal(string.Empty, source.FilePath);
    }

    [Fact]
    public void FileImageSource_ConvenienceCtor_SetsFilePath()
    {
        var source = new FileImageSource("logo.png");
        Assert.Equal("logo.png", source.FilePath);
    }

    [Fact]
    public void FileImageSource_ConvenienceCtor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new FileImageSource(null!));
    }

    [Fact]
    public void FileImageSource_Clone_CopiesPathAndBindingsIndependently()
    {
        var source = new FileImageSource("a.png");
        source.SetBinding(FileImageSource.FilePathProperty, "LogoPath");

        var clone = (FileImageSource)source.Clone();
        clone.FilePath = "b.png";
        new BindingEngine().ResolveBindings(clone, new { LogoPath = "bound.png" });

        Assert.Equal("a.png", source.FilePath);
        Assert.Equal("bound.png", clone.FilePath);
        Assert.NotSame(source, clone);
    }

    [Fact]
    public void UriImageSource_Parameterless_DefaultsToNullUri()
    {
        Assert.Null(new UriImageSource().Uri);
    }

    [Fact]
    public void UriImageSource_ConvenienceCtor_SetsUri()
    {
        var uri = new Uri("https://example.com/a.png");
        Assert.Equal(uri, new UriImageSource(uri).Uri);
    }

    [Fact]
    public void UriImageSource_ConvenienceCtor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UriImageSource(null!));
    }

    [Fact]
    public void UriImageSource_Clone_CopiesUriIndependently()
    {
        var source = new UriImageSource(new Uri("https://example.com/a.png"));
        var clone = (UriImageSource)source.Clone();
        clone.Uri = new Uri("https://example.com/b.png");

        Assert.Equal(new Uri("https://example.com/a.png"), source.Uri);
        Assert.Equal(new Uri("https://example.com/b.png"), clone.Uri);
    }

    [Fact]
    public void ResourceImageSource_Parameterless_DefaultsToEmptyName()
    {
        Assert.Equal(string.Empty, new ResourceImageSource().ResourceName);
    }

    [Fact]
    public void ResourceImageSource_ConvenienceCtor_SetsResourceName()
    {
        Assert.Equal("App.logo.png", new ResourceImageSource("App.logo.png").ResourceName);
    }

    [Fact]
    public void ResourceImageSource_ConvenienceCtor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ResourceImageSource(null!));
    }

    [Fact]
    public void ResourceImageSource_Clone_CopiesNameIndependently()
    {
        var source = new ResourceImageSource("a");
        var clone = (ResourceImageSource)source.Clone();
        clone.ResourceName = "b";

        Assert.Equal("a", source.ResourceName);
        Assert.Equal("b", clone.ResourceName);
    }

    [Fact]
    public void StreamImageSource_Parameterless_DefaultsToNullProvider()
    {
        Assert.Null(new StreamImageSource().StreamProvider);
    }

    [Fact]
    public void StreamImageSource_ConvenienceCtor_SetsProvider()
    {
        Func<Stream> provider = () => Stream.Null;
        Assert.Same(provider, new StreamImageSource(provider).StreamProvider);
    }

    [Fact]
    public void StreamImageSource_ConvenienceCtor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new StreamImageSource(null!));
    }

    [Fact]
    public void StreamImageSource_Clone_SharesProviderDelegateIndependentlyOfInstance()
    {
        Func<Stream> provider = () => Stream.Null;
        var source = new StreamImageSource(provider);
        var clone = (StreamImageSource)source.Clone();

        Assert.Same(provider, clone.StreamProvider);
        Assert.NotSame(source, clone);
        clone.StreamProvider = () => Stream.Null;
        Assert.Same(provider, source.StreamProvider);
    }

    [Fact]
    public void ImageClone_DoesNotShareFileImageSource()
    {
        var image = new Image { Source = new FileImageSource("a.png") };
        var clone = (Image)image.Clone();
        ((FileImageSource)clone.Source!).FilePath = "b.png";

        Assert.Equal("a.png", ((FileImageSource)image.Source!).FilePath);
        Assert.NotSame(image.Source, clone.Source);
    }
}
