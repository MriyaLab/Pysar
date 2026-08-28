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
}
