using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests;

public class SkiaFontCollectionTests
{
    private const string FontPath = "Fonts/Ubuntu-Regular.ttf";

    private static byte[] FontBytes()
        => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Ubuntu-Regular.ttf"));

    [Fact]
    public void AddFont_StoresTypefaceUnderTheAliasAndStyleKey()
    {
        var fonts = new SkiaFontCollection(new FakeSyncFileSystem((FontPath, FontBytes())));

        fonts.AddFont(FontPath, "Ubuntu", FontStyle.Bold);

        Assert.True(fonts.ContainsKey("Ubuntu|Bold"));
        Assert.IsAssignableFrom<SKTypeface>(fonts["Ubuntu|Bold"]);
    }

    [Fact]
    public void AddFont_WithoutAlias_UsesTheFileNameAsKey()
    {
        var fonts = new SkiaFontCollection(new FakeSyncFileSystem((FontPath, FontBytes())));

        fonts.AddFont(FontPath);

        Assert.True(fonts.ContainsKey($"{FontPath}|Normal"));
    }

    [Fact]
    public void AddFont_SameAliasAndStyleTwice_ReadsTheFileOnce()
    {
        var fileSystem = new FakeSyncFileSystem((FontPath, FontBytes()));
        var fonts = new SkiaFontCollection(fileSystem);

        fonts.AddFont(FontPath, "Ubuntu");
        fonts.AddFont(FontPath, "Ubuntu");

        Assert.Equal(1, fileSystem.SyncReadCount);
        Assert.Single(fonts);
    }

    [Fact]
    public void AddFont_SameAliasDifferentStyles_KeepsBothEntries()
    {
        var fonts = new SkiaFontCollection(new FakeSyncFileSystem((FontPath, FontBytes())));

        fonts.AddFont(FontPath, "Ubuntu");
        fonts.AddFont(FontPath, "Ubuntu", FontStyle.Italic);

        Assert.True(fonts.ContainsKey("Ubuntu|Normal"));
        Assert.True(fonts.ContainsKey("Ubuntu|Italic"));
    }

    [Fact]
    public void AddFont_MissingFile_ThrowsWithThePath()
    {
        var fonts = new SkiaFontCollection(new FakeSyncFileSystem());

        var exception = Assert.Throws<FileNotFoundException>(
            () => { fonts.AddFont("Fonts/Missing.ttf", "Missing"); });

        Assert.Contains("Fonts/Missing.ttf", exception.Message);
    }

    [Fact]
    public void AddFont_UndecodableFile_Throws()
    {
        var fonts = new SkiaFontCollection(new FakeSyncFileSystem((FontPath, [1, 2, 3, 4])));

        Assert.Throws<InvalidOperationException>(() => { fonts.AddFont(FontPath, "Ubuntu"); });
    }

    [Fact]
    public void AddFont_PrefersTheSynchronousReadWhenTheFileSystemOffersOne()
    {
        var fileSystem = new FakeSyncFileSystem((FontPath, FontBytes()));
        var fonts = new SkiaFontCollection(fileSystem);

        fonts.AddFont(FontPath, "Ubuntu");

        Assert.Equal(1, fileSystem.SyncReadCount);
        Assert.Equal(0, fileSystem.ReadCount);
    }

    [Fact]
    public void AddFont_AsyncOnlyFileSystem_ThrowsInvalidOperationException()
    {
        var fonts = new SkiaFontCollection(new FakeFileSystem((FontPath, FontBytes())));
        var ex = Assert.Throws<InvalidOperationException>(() => fonts.AddFont(FontPath, "Ubuntu"));
        Assert.Contains("ISyncFileSystem", ex.Message, StringComparison.Ordinal);
    }

    private class FakeFileSystem : IFileSystem
    {
        private readonly Dictionary<string, byte[]> _files;

        public FakeFileSystem(params (string Path, byte[] Content)[] files)
            => _files = files.ToDictionary(file => file.Path, file => file.Content);

        public int ReadCount { get; private set; }

        public Task<byte[]?> ReadFileAsync(string filePath)
        {
            ReadCount++;
            return Task.FromResult(Read(filePath));
        }

        public bool Exists([NotNullWhen(true)] string? filePath)
            => filePath is not null && _files.ContainsKey(filePath);

        protected byte[]? Read(string filePath) => _files.GetValueOrDefault(filePath);
    }

    private sealed class FakeSyncFileSystem : FakeFileSystem, ISyncFileSystem
    {
        public FakeSyncFileSystem(params (string Path, byte[] Content)[] files) : base(files) { }

        public int SyncReadCount { get; private set; }

        public byte[]? ReadFile(string filePath)
        {
            SyncReadCount++;
            return Read(filePath);
        }
    }
}
