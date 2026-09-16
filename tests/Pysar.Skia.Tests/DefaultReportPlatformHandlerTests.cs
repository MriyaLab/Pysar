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
        // Absence used to be the proxy for "the root argument took effect", back when the
        // disk-backed constructors read from disk alone. They now also fall back to embedded
        // report assets (see DefaultReportPlatformHandlerAssetChainTests), and this test project's
        // own assembly embeds "Fonts/Ubuntu-Regular.ttf" - so asserting Exists == false for that
        // path no longer isolates the root directory's effect, it just happens to also be true
        // before the embedded member is even consulted. Assert positively instead: a file actually
        // written under the given root is found there, and a name that is genuinely nowhere - not
        // on disk, not embedded - is genuinely missing.
        var dir = Directory.CreateTempSubdirectory("pysar-root-dir-");
        try
        {
            Directory.CreateDirectory(Path.Combine(dir.FullName, "Fonts"));
            File.WriteAllBytes(Path.Combine(dir.FullName, "Fonts", "Ubuntu-Regular.ttf"), [1, 2, 3]);

            var handler = new DefaultReportPlatformHandler(dir.FullName);

            Assert.True(handler.FileSystem.Exists(FontPath));
            Assert.False(handler.FileSystem.Exists("Fonts/Nowhere.ttf"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
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
