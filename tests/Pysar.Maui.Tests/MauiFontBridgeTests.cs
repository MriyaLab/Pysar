using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui;
using Pysar.Core.Enums;
using Pysar.Maui;
using Pysar.Skia;
using Xunit;

namespace Pysar.Maui.Tests;

public class MauiFontBridgeTests
{
    [Fact]
    public void Install_ResolvedRegistrarFeedsTheAliasIntoTheReportFontFallback()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFontRegistrar>(new RememberingFontRegistrar());
        var fonts = new SkiaFontCollection(new AppPackageFileSystem());

        MauiFontBridge.Install(services, fonts, new AppPackageFileSystem());

        var registrar = services.BuildServiceProvider().GetRequiredService<IFontRegistrar>();
        registrar.Register("Fonts/Ubuntu-Regular.ttf", "OpenSansRegular");

        Assert.True(fonts.TryResolveFallback("OpenSansRegular", FontStyle.Normal, out var typeface));
        Assert.Equal("Ubuntu", typeface.FamilyName);
        Assert.Equal("OpenSansRegular", registrar.GetFont("OpenSansRegular"));
    }

    [Fact]
    public void Install_BeforeConfigureFonts_StaysTheRegistrarFontsAreRegisteredOn()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEmbeddedFontLoader>(new EmbeddedFontLoader());
        var fonts = new SkiaFontCollection(new AppPackageFileSystem());

        MauiFontBridge.Install(services, fonts, new AppPackageFileSystem());
        services.TryAddSingleton<IFontRegistrar>(new RememberingFontRegistrar());

        var registrar = services.BuildServiceProvider().GetRequiredService<IFontRegistrar>();
        registrar.Register("Fonts/Ubuntu-Regular.ttf", "OpenSansRegular");

        Assert.True(fonts.TryResolveFallback("OpenSansRegular", FontStyle.Normal, out _));
    }

    private sealed class RememberingFontRegistrar : IFontRegistrar
    {
        public void Register(string filename, string? alias, System.Reflection.Assembly assembly) { }

        public void Register(string filename, string? alias) { }

        public string? GetFont(string font) => font;
    }
}
