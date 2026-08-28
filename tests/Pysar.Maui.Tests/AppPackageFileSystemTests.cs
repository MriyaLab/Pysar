using Pysar.Core.Abstractions;
using Pysar.Maui;
using Pysar.Skia;
using Xunit;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;

namespace Pysar.Maui.Tests;

/// <summary>
///     Asset resolution from the application package, which is what the MAUI sample used to be the
///     only proof of. The package itself is the test's output directory - see
///     <c>AppPackageFileSystem.TestPackage.cs</c> for why that is the same seam a device fills.
/// </summary>
public class AppPackageFileSystemTests
{
    private const string FontPath = "Fonts/Ubuntu-Regular.ttf";

    [Fact]
    public void ReadFile_ReadsAPackagedAsset()
    {
        var bytes = new AppPackageFileSystem().ReadFile(FontPath);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Theory]
    [InlineData("Fonts\\Ubuntu-Regular.ttf")]
    [InlineData("/Fonts/Ubuntu-Regular.ttf")]
    public void ReadFile_AcceptsThePathSeparatorsAReportMayHaveWritten(string path)
    {
        var fileSystem = new AppPackageFileSystem();

        Assert.Equal(fileSystem.ReadFile(FontPath), fileSystem.ReadFile(path));
        Assert.True(fileSystem.Exists(path));
    }

    [Fact]
    public void MissingAssetsAreReportedRatherThanThrown()
    {
        var fileSystem = new AppPackageFileSystem();

        Assert.Null(fileSystem.ReadFile("Images/no-such-image.svg"));
        Assert.Null(fileSystem.ReadFile(string.Empty));
        Assert.False(fileSystem.Exists("Fonts/NoSuchFont.ttf"));
        Assert.False(fileSystem.Exists(null));
    }

    [Fact]
    public async Task ReadFileAsync_AgreesWithTheSynchronousRead()
    {
        var fileSystem = new AppPackageFileSystem();

        Assert.Equal(fileSystem.ReadFile(FontPath), await fileSystem.ReadFileAsync(FontPath));
    }

    [Fact]
    public void TheFileSystemIsSynchronous_WhichIsWhatExistsAndFontLoadingNeed()
    {
        // SkiaFontCollection reads through ISyncFileSystem and throws when a file system only
        // offers the asynchronous half, so the interface is part of the contract, not an extra.
        Assert.IsAssignableFrom<ISyncFileSystem>(new AppPackageFileSystem());
    }

    [Fact]
    public void ThePlatformHandler_ResolvesFontsThroughThePackage()
    {
        var handler = new MauiReportPlatformHandler();

        Assert.IsType<AppPackageFileSystem>(handler.FileSystem);

        // The registration a host performs at startup: the font travels from the package into the
        // collection the report renderer looks families up in.
        handler.FontCollection.AddFont(FontPath, "Ubuntu");

        var fonts = Assert.IsType<SkiaFontCollection>(handler.FontCollection);

        Assert.True(fonts.ContainsKey(SkiaFontCollection.GetCacheKey("Ubuntu", default)));
    }

    [Fact]
    public void RegisteringAFontThatIsNotInThePackage_SaysSoAtRegistrationTime()
    {
        IFileSystem fileSystem = new AppPackageFileSystem();
        var fonts = new SkiaFontCollection(fileSystem);

        Assert.Throws<FileNotFoundException>(() => fonts.AddFont("Fonts/NoSuchFont.ttf", "Missing"));
    }
}
