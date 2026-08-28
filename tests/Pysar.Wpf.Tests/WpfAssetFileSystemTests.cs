using Xunit;

namespace Pysar.Wpf.Tests;

/// <summary>
///     Asset resolution against a real WPF application: the file system reads the fonts and images a
///     report asks for out of the application's own resources, which is what the desktop sample used
///     to be the only proof of.
/// </summary>
[Collection(WpfCollection.Name)]
public class WpfAssetFileSystemTests(WpfSession session)
{
    /// <summary>Declared as an EmbeddedResource with this exact LogicalName - see the csproj.</summary>
    private const string EmbeddedPath = "Fonts/Ubuntu-Regular.ttf";

    /// <summary>Declared as a WPF Resource under this path - see the csproj.</summary>
    private const string PackedPath = "Fonts/Packed-Ubuntu.ttf";

    private static WpfAssetFileSystem CreateFileSystem() => new(WpfSession.AssetAssemblyName);

    [Fact]
    public void ReadFile_ReadsAnEmbeddedResource()
        => session.Run(() =>
        {
            var bytes = CreateFileSystem().ReadFile(EmbeddedPath);

            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
        });

    [Fact]
    public void ReadFile_FallsBackToAPackUri()
        => session.Run(() =>
        {
            var bytes = CreateFileSystem().ReadFile(PackedPath);

            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
        });

    [Theory]
    [InlineData("Fonts\\Ubuntu-Regular.ttf")]
    [InlineData("/Fonts/Ubuntu-Regular.ttf")]
    public void ReadFile_AcceptsThePathSeparatorsAReportMayHaveWritten(string path)
        => session.Run(() =>
        {
            var fileSystem = CreateFileSystem();

            Assert.Equal(fileSystem.ReadFile(EmbeddedPath), fileSystem.ReadFile(path));
            Assert.True(fileSystem.Exists(path));
        });

    [Fact]
    public void Exists_IsTrueForBothPackagingsAndFalseForAnythingElse()
        => session.Run(() =>
        {
            var fileSystem = CreateFileSystem();

            Assert.True(fileSystem.Exists(EmbeddedPath));
            Assert.True(fileSystem.Exists(PackedPath));
            Assert.False(fileSystem.Exists("Fonts/NoSuchFont.ttf"));
        });

    [Fact]
    public void MissingAssetsAreReportedRatherThanThrown()
        => session.Run(() =>
        {
            var fileSystem = CreateFileSystem();

            Assert.Null(fileSystem.ReadFile("Images/no-such-image.svg"));
            Assert.Null(fileSystem.ReadFile(string.Empty));
            Assert.False(fileSystem.Exists(null));

            // An assembly that is not loaded at all must answer the same way, not throw out of
            // Assembly.Load.
            var missingAssembly = new WpfAssetFileSystem("No.Such.Assembly");

            Assert.Null(missingAssembly.ReadFile(EmbeddedPath));
            Assert.False(missingAssembly.Exists(EmbeddedPath));
        });

    [Fact]
    public void ReadFileAsync_AgreesWithTheSynchronousRead()
        => session.Run(async () =>
        {
            var fileSystem = CreateFileSystem();

            Assert.Equal(fileSystem.ReadFile(EmbeddedPath), await fileSystem.ReadFileAsync(EmbeddedPath));
        });
}
