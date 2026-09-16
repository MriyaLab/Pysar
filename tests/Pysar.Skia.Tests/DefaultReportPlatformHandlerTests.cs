using Pysar.Core.Abstractions;
using Xunit;

namespace Pysar.Skia.Tests;

public class DefaultReportPlatformHandlerTests
{
    private const string FontPath = "Fonts/Ubuntu-Regular.ttf";

    [Fact]
    public void FileSystem_ReadsAssetsFromTheApplicationDirectory()
    {
        var handler = new DefaultReportPlatformHandler();

        Assert.True(handler.FileSystem.Exists(FontPath));
    }

    [Fact]
    public void FontCollection_LoadsTypefacesThroughTheHandlersFileSystem()
    {
        var handler = new DefaultReportPlatformHandler();

        handler.FontCollection.AddFont(FontPath, "Ubuntu");

        Assert.True(((SkiaFontCollection)handler.FontCollection).ContainsKey("Ubuntu|Normal"));
    }

    [Fact]
    public void FileSystem_WithAnExplicitRootDirectory_ResolvesAssetsAgainstThatDirectory()
    {
        var handler = new DefaultReportPlatformHandler(Path.GetTempPath());

        Assert.False(handler.FileSystem.Exists(FontPath));
    }

    [Fact]
    public void FileSystem_WithAnExplicitFileSystem_IsTheOneTheHandlerExposes()
    {
        IFileSystem files = new LocalFileSystem(Path.GetTempPath());

        var handler = new DefaultReportPlatformHandler(files);

        Assert.Same(files, handler.FileSystem);
        Assert.IsType<SkiaFontCollection>(handler.FontCollection);
    }

    [Fact]
    public void RejectsAMissingFileSystem()
        => Assert.Throws<ArgumentNullException>(() => new DefaultReportPlatformHandler((IFileSystem)null!));
}
