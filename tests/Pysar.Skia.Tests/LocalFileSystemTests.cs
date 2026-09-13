using Xunit;

namespace Pysar.Skia.Tests;

public class LocalFileSystemTests
{
    /// <summary>An asset copied to the test output directory, which is what relative paths resolve against.</summary>
    private const string AssetPath = "Fonts/Ubuntu-Regular.ttf";

    [Fact]
    public void ReadFile_ResolvesTheRelativePathAgainstTheApplicationDirectory()
    {
        var fileSystem = new LocalFileSystem();

        var bytes = fileSystem.ReadFile(AssetPath);

        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Ubuntu-Regular.ttf")), bytes);
    }

    [Fact]
    public async Task ReadFileAsync_ResolvesTheRelativePathAgainstTheApplicationDirectory()
    {
        var fileSystem = new LocalFileSystem();

        var bytes = await fileSystem.ReadFileAsync(AssetPath);

        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Ubuntu-Regular.ttf")), bytes);
    }

    [Fact]
    public void ReadFile_ReturnsNullWhenTheFileDoesNotExist()
    {
        var fileSystem = new LocalFileSystem();

        Assert.Null(fileSystem.ReadFile("Fonts/Missing.ttf"));
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsNullWhenTheFileDoesNotExist()
    {
        var fileSystem = new LocalFileSystem();

        Assert.Null(await fileSystem.ReadFileAsync("Fonts/Missing.ttf"));
    }

    [Fact]
    public void Exists_IsTrueForAFileInTheApplicationDirectory()
    {
        var fileSystem = new LocalFileSystem();

        Assert.True(fileSystem.Exists(AssetPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Fonts/Missing.ttf")]
    public void Exists_IsFalseWhenThereIsNoSuchFile(string? filePath)
    {
        var fileSystem = new LocalFileSystem();

        Assert.False(fileSystem.Exists(filePath));
    }

    [Fact]
    public void ReadFile_WithAnExplicitRootDirectory_ResolvesAgainstThatDirectory()
    {
        using var root = new TemporaryDirectory();
        root.WriteFile("Images/logo.svg", [1, 2, 3]);
        var fileSystem = new LocalFileSystem(root.Path);

        Assert.Equal([1, 2, 3], fileSystem.ReadFile("Images/logo.svg"));
    }

    [Fact]
    public void Exists_WithAnExplicitRootDirectory_IgnoresTheApplicationDirectory()
    {
        using var root = new TemporaryDirectory();
        var fileSystem = new LocalFileSystem(root.Path);

        Assert.False(fileSystem.Exists(AssetPath));
    }

    [Fact]
    public void ReadFile_WithAnAbsolutePath_ReadsThatFile()
    {
        using var root = new TemporaryDirectory();
        var absolutePath = root.WriteFile("Images/logo.svg", [4, 5, 6]);
        var fileSystem = new LocalFileSystem();

        Assert.Equal([4, 5, 6], fileSystem.ReadFile(absolutePath));
    }

    /// <summary>A directory outside the test output, so a root other than the application directory can be exercised.</summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string WriteFile(string relativePath, byte[] contents)
        {
            var fullPath = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, contents);

            return fullPath;
        }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
