using Pysar.Core.Abstractions;
using Xunit;

namespace Pysar.Skia.Tests;

/// <summary>
///     The default handler is what the console, workers, server-side rendering and the design-time
///     preview all run on, so the embedded fallback has to be there and not only on the UI
///     platforms - otherwise the preview shows a report without the fonts the application will have.
/// </summary>
public sealed class DefaultReportPlatformHandlerAssetChainTests
{
    private static byte[] Expected()
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Ubuntu-Regular.ttf"));

    [Fact]
    public void FileSystem_ReadsAnEmbeddedAssetThatIsNotOnDisk()
    {
        var handler = new DefaultReportPlatformHandler();

        Assert.Equal(Expected(), ((ISyncFileSystem)handler.FileSystem)
            .ReadFile("Fonts/Embedded-Ubuntu.ttf"));
    }

    [Fact]
    public void FileSystem_PrefersTheFileOnDiskOverTheEmbeddedCopy()
    {
        // The shared "Fonts/Ubuntu-Regular.ttf" fixture is byte-identical on disk and embedded, so
        // reading it back through that path would pass no matter which chain member answered.
        // Writing a distinct payload to a private temp root - not the real font, nothing decodes it
        // here - and reading it back through the handler's public surface is what actually proves
        // disk wins: had the embedded member answered instead, this would read the font's bytes.
        var dir = Directory.CreateTempSubdirectory("pysar-asset-chain-");
        try
        {
            var diskBytes = new byte[] { 1, 2, 3, 4, 5 };
            Directory.CreateDirectory(Path.Combine(dir.FullName, "Fonts"));
            File.WriteAllBytes(Path.Combine(dir.FullName, "Fonts", "Ubuntu-Regular.ttf"), diskBytes);

            var handler = new DefaultReportPlatformHandler(dir.FullName);

            Assert.True(handler.FileSystem.Exists("Fonts/Ubuntu-Regular.ttf"));
            Assert.Equal(diskBytes, ((ISyncFileSystem)handler.FileSystem)
                .ReadFile("Fonts/Ubuntu-Regular.ttf"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void FileSystem_IsNotChainedWhenTheCallerSuppliedOne()
    {
        // An explicit file system is the caller's whole answer; silently widening it would make
        // "read assets from exactly here" impossible to express.
        var handler = new DefaultReportPlatformHandler(new LocalFileSystem(AppContext.BaseDirectory));

        Assert.False(handler.FileSystem.Exists("Fonts/Embedded-Ubuntu.ttf"));
    }
}
