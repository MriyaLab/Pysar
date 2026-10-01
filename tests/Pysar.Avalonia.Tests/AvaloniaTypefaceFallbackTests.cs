using Avalonia.Media;
using Avalonia.Media.Fonts;
using Pysar.Avalonia;
using Pysar.Core.Enums;
using Pysar.Skia;
using SkiaSharp;
using Xunit;
using AvaloniaTypeface = Avalonia.Media.Typeface;
using FontStyle = Pysar.Core.Enums.FontStyle;

namespace Pysar.Avalonia.Tests;

[Collection(HeadlessCollection.Name)]
public class AvaloniaTypefaceFallbackTests(HeadlessSession session)
{
    [Fact]
    public void TryResolve_ReturnsTheFaceFontManagerAlreadyKnows()
        => session.Run(() =>
        {
            var collection = RegisterUbuntu();
            try
            {
                const string family = "fonts:Ubuntu#Ubuntu";

                Assert.True(FontManager.Current.TryGetGlyphTypeface(
                    new AvaloniaTypeface(family), out var glyph));
                Assert.Equal("Ubuntu", glyph.FamilyName);

                var resolved = AvaloniaTypefaceFallback.TryResolve(family, FontStyle.Normal);

                Assert.NotNull(resolved);
                Assert.Equal(glyph.FamilyName, resolved.FamilyName);
            }
            finally
            {
                FontManager.Current.RemoveFontCollection(collection.Key);
            }
        });

    [Fact]
    public void TryResolve_UnknownFamily_ReturnsNull()
        => session.Run(() =>
            Assert.Null(AvaloniaTypefaceFallback.TryResolve("NoSuchFamily-PysarTest", FontStyle.Normal)));

    [Fact]
    public void UsePysar_InstallsTheFallbackOnTheAmbientCollection()
        => session.Run(() =>
        {
            var collection = RegisterUbuntu();
            try
            {
                var fonts = Assert.IsType<SkiaFontCollection>(Pysar.Core.ReportPlatformHandler.FontCollection);

                Assert.True(fonts.TryResolveFallback("fonts:Ubuntu#Ubuntu", FontStyle.Normal, out var face));
                Assert.NotNull(face);
            }
            finally
            {
                FontManager.Current.RemoveFontCollection(collection.Key);
            }
        });

    private static EmbeddedFontCollection RegisterUbuntu()
    {
        var collection = new EmbeddedFontCollection(
            new Uri("fonts:Ubuntu", UriKind.Absolute),
            new Uri($"avares://{HeadlessApp.AssetAssemblyName}/Fonts"));
        FontManager.Current.AddFontCollection(collection);
        return collection;
    }
}
