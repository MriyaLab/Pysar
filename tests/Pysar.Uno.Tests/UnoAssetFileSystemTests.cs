using System.Reflection;
using Pysar.Core.Abstractions;
using Xunit;

namespace Pysar.Uno.Tests;

/// <summary>
///     Asset resolution as an Uno application packages it: an embedded resource whose LogicalName
///     is the path the report asks for, read synchronously because font registration cannot await.
/// </summary>
public class UnoAssetFileSystemTests
{
    /// <summary>Declared as an EmbeddedResource with this exact LogicalName - see the csproj.</summary>
    private const string FontPath = "Fonts/Ubuntu-Regular.ttf";

    private static UnoAssetFileSystem CreateFileSystem()
        => new(Assembly.GetExecutingAssembly());

    [Fact]
    public void IsASynchronousFileSystem()
    {
        // SkiaFontCollection.AddFont, ResourceDictionary Source and ImageRenderer all require this;
        // without it they throw rather than falling back to an await.
        Assert.IsAssignableFrom<ISyncFileSystem>(CreateFileSystem());
        Assert.IsAssignableFrom<IFileSystem>(CreateFileSystem());
    }

    [Fact]
    public void ReadFile_ReadsAnEmbeddedResource()
    {
        var bytes = CreateFileSystem().ReadFile(FontPath);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsTheSameBytes()
    {
        var fileSystem = CreateFileSystem();

        Assert.Equal(fileSystem.ReadFile(FontPath), await fileSystem.ReadFileAsync(FontPath));
    }

    [Theory]
    [InlineData("Fonts\\Ubuntu-Regular.ttf")]
    [InlineData("/Fonts/Ubuntu-Regular.ttf")]
    public void ReadFile_AcceptsThePathSeparatorsAReportMayHaveWritten(string path)
    {
        var fileSystem = CreateFileSystem();

        Assert.Equal(fileSystem.ReadFile(FontPath), fileSystem.ReadFile(path));
        Assert.True(fileSystem.Exists(path));
    }

    [Fact]
    public void Exists_IsTrueForAPackagedAssetAndFalseForAnythingElse()
    {
        var fileSystem = CreateFileSystem();

        Assert.True(fileSystem.Exists(FontPath));
        Assert.False(fileSystem.Exists("Fonts/NoSuchFont.ttf"));
    }

    [Fact]
    public void MissingAssetsAreReportedRatherThanThrown()
    {
        var fileSystem = CreateFileSystem();

        Assert.Null(fileSystem.ReadFile("Images/no-such-image.svg"));
        Assert.Null(fileSystem.ReadFile(string.Empty));
        Assert.False(fileSystem.Exists(null));
    }

    [Fact]
    public void Preload_ServesContentTheAssemblyDoesNotCarry()
    {
        var fileSystem = CreateFileSystem();
        var content = new byte[] { 1, 2, 3 };

        fileSystem.Preload([new KeyValuePair<string, byte[]>("Images/logo.svg", content)]);

        Assert.Equal(content, fileSystem.ReadFile("Images/logo.svg"));
        Assert.True(fileSystem.Exists("Images/logo.svg"));
    }

    [Fact]
    public void Preload_NormalizesPathsTheSameWayReadsDo()
    {
        var fileSystem = CreateFileSystem();
        var content = new byte[] { 1, 2, 3 };

        fileSystem.Preload([new KeyValuePair<string, byte[]>("\\Images\\logo.svg", content)]);

        Assert.Equal(content, fileSystem.ReadFile("Images/logo.svg"));
    }

    [Fact]
    public void Preload_TakesPrecedenceOverAnEmbeddedResourceAtTheSamePath()
    {
        // An application that has replaced a packaged asset at runtime must see its replacement,
        // not the copy compiled into the assembly.
        var fileSystem = CreateFileSystem();
        var replacement = new byte[] { 9, 9, 9 };

        fileSystem.Preload([new KeyValuePair<string, byte[]>(FontPath, replacement)]);

        Assert.Equal(replacement, fileSystem.ReadFile(FontPath));
    }

    [Fact]
    public void Preload_RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => CreateFileSystem().Preload(null!));
}
