using Xunit;

namespace Pysar.Avalonia.Tests;

/// <summary>
///     Asset resolution end to end against a real Avalonia asset loader: the file system reads the
///     fonts and images a report asks for out of the application's own resources, which is what the
///     desktop sample used to be the only proof of.
/// </summary>
[Collection(HeadlessCollection.Name)]
public class AvaloniaAssetFileSystemTests(HeadlessSession session)
{
    private const string AvaresPath = "Fonts/Ubuntu-Regular.ttf";

    /// <summary>Declared as an EmbeddedResource with this exact LogicalName - see the csproj.</summary>
    private const string EmbeddedPath = "Fonts/Embedded-Ubuntu.ttf";

    private AvaloniaAssetFileSystem CreateFileSystem() => new(HeadlessApp.AssetAssemblyName);

    [Fact]
    public void ReadFile_ReadsAnAvaloniaResource()
        => session.Run(() =>
        {
            var bytes = CreateFileSystem().ReadFile(AvaresPath);

            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
        });

    [Fact]
    public void ReadFile_FallsBackToAPlainEmbeddedResource()
        => session.Run(() =>
        {
            var bytes = CreateFileSystem().ReadFile(EmbeddedPath);

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

            Assert.Equal(fileSystem.ReadFile(AvaresPath), fileSystem.ReadFile(path));
            Assert.True(fileSystem.Exists(path));
        });

    [Fact]
    public void Exists_IsTrueForBothPackagingsAndFalseForAnythingElse()
        => session.Run(() =>
        {
            var fileSystem = CreateFileSystem();

            Assert.True(fileSystem.Exists(AvaresPath));
            Assert.True(fileSystem.Exists(EmbeddedPath));
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
            var missingAssembly = new AvaloniaAssetFileSystem("No.Such.Assembly");

            Assert.Null(missingAssembly.ReadFile(AvaresPath));
            Assert.False(missingAssembly.Exists(AvaresPath));
        });

    [Fact]
    public void ReadFileAsync_AgreesWithTheSynchronousRead()
        => session.Run(async () =>
        {
            var fileSystem = CreateFileSystem();

            Assert.Equal(fileSystem.ReadFile(AvaresPath), await fileSystem.ReadFileAsync(AvaresPath));
        });
}
