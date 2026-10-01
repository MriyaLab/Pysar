using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Skia.Helpers;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests;

public class FontCacheTests
{
    private static SKTypeface LoadUbuntu()
        => SKTypeface.FromStream(
               new MemoryStream(
                   File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "Ubuntu-Regular.ttf"))))
           ?? throw new InvalidOperationException("test font could not be decoded");

    private sealed class Fonts : Dictionary<string, object>, IFontCollection
    {
        public IFontCollection AddFont(string filename, string? alias = null, FontStyle style = FontStyle.Normal)
            => this;
    }

    [Fact]
    public void GetTypeface_ReturnsAFontRegisteredBeforeTheCacheWasBuilt()
    {
        var typeface = LoadUbuntu();
        var fonts = new Fonts { ["Ubuntu|Normal"] = typeface };

        var cache = new FontCache(fonts);

        Assert.Same(typeface, cache.GetTypeface(new Font("Ubuntu", 12)));
    }

    [Fact]
    public void GetTypeface_ReturnsAFontRegisteredAfterTheCacheWasBuilt()
    {
        var fonts = new Fonts();
        var cache = new FontCache(fonts);

        // Registration order is not something the cache gets to dictate: a host that adds a font
        // after the first glyph was measured must still see it.
        var typeface = LoadUbuntu();
        fonts["Ubuntu|Normal"] = typeface;

        Assert.Same(typeface, cache.GetTypeface(new Font("Ubuntu", 12)));
    }

    [Fact]
    public void GetTypeface_UnknownFamily_FallsBackToASystemTypefaceRatherThanThrowing()
    {
        var cache = new FontCache(new Fonts());

        Assert.NotNull(cache.GetTypeface(new Font("NoSuchFamily-PysarTest", 12)));
    }

    [Fact]
    public void GetTypeface_UsesTheFallbackWhenTheFamilyIsNotRegistered()
    {
        var expected = LoadUbuntu();
        var fonts = new SkiaFontCollection(new EmptyFileSystem());
        fonts.SetTypefaceFallback((family, style) =>
            family == "Ubuntu" && style == FontStyle.Normal ? expected : null);

        var cache = new FontCache(fonts);

        Assert.Same(expected, cache.GetTypeface(new Font("Ubuntu", 12)));
    }

    [Fact]
    public void GetTypeface_ExplicitRegistrationSupersedesTheFallback()
    {
        var fromFallback = LoadUbuntu();
        var fonts = new SkiaFontCollection(new OutputFontFileSystem());
        fonts.SetTypefaceFallback((family, style) =>
            family == "Ubuntu" && style == FontStyle.Normal ? fromFallback : null);
        var cache = new FontCache(fonts);
        Assert.Same(fromFallback, cache.GetTypeface(new Font("Ubuntu", 12)));

        fonts.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");

        var registered = fonts[SkiaFontCollection.GetCacheKey("Ubuntu", FontStyle.Normal)];
        Assert.NotSame(fromFallback, registered);
        Assert.Same(registered, cache.GetTypeface(new Font("Ubuntu", 12)));
    }

    [Fact]
    public void GetTypeface_NullFallbackFallsThroughToASystemTypeface()
    {
        var fonts = new SkiaFontCollection(new EmptyFileSystem());
        fonts.SetTypefaceFallback((_, _) => null);
        var cache = new FontCache(fonts);

        Assert.NotNull(cache.GetTypeface(new Font("NoSuchFamily-PysarTest", 12)));
    }

    private sealed class EmptyFileSystem : IFileSystem, ISyncFileSystem
    {
        public byte[]? ReadFile(string filePath) => null;

        public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult<byte[]?>(null);

        public bool Exists([NotNullWhen(true)] string? filePath) => false;
    }

    private sealed class OutputFontFileSystem : IFileSystem, ISyncFileSystem
    {
        private const string FontPath = "Fonts/Ubuntu-Regular.ttf";

        public byte[]? ReadFile(string filePath)
            => filePath == FontPath
                ? File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, FontPath))
                : null;

        public Task<byte[]?> ReadFileAsync(string filePath)
            => throw new NotSupportedException();

        public bool Exists([NotNullWhen(true)] string? filePath) => filePath == FontPath;
    }
}
