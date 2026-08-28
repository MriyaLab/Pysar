using Pysar.Blazor;
using Pysar.Core.Abstractions;
using Xunit;

namespace Pysar.Blazor.Tests;

public class PreloadedFileSystemTests
{
    private static PreloadedFileSystem Subject()
        => PreloadedFileSystem.From(new Dictionary<string, byte[]>
        {
            ["Fonts/Ubuntu-Regular.ttf"] = [1, 2, 3]
        });

    [Fact]
    public void AKnownFile_ReadsBack()
        => Assert.Equal(new byte[] { 1, 2, 3 }, Subject().ReadFile("Fonts/Ubuntu-Regular.ttf"));

    [Fact]
    public void AnUnknownFile_IsNull()
        => Assert.Null(Subject().ReadFile("Fonts/Missing.ttf"));

    [Fact]
    public void ABackslashPath_FindsTheSameFile()
        => Assert.NotNull(Subject().ReadFile("Fonts\\Ubuntu-Regular.ttf"));

    [Fact]
    public void ALeadingSlash_IsIgnored()
        => Assert.True(Subject().Exists("/Fonts/Ubuntu-Regular.ttf"));

    [Fact]
    public void ItIsSynchronouslyReadable()
    {
        // Not a style preference. SkiaFontCollection falls back to blocking on ReadFileAsync when
        // a file system does not offer this interface, and on the browser's single thread that
        // block never returns.
        Assert.IsAssignableFrom<ISyncFileSystem>(Subject());
    }

    [Fact]
    public async Task TheAsyncReadAgrees_WithTheSyncOne()
        => Assert.Equal(
            Subject().ReadFile("Fonts/Ubuntu-Regular.ttf"),
            await Subject().ReadFileAsync("Fonts/Ubuntu-Regular.ttf"));
}
