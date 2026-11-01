using Pysar.Core.Enums;
using Pysar.Maui;
using Xunit;

namespace Pysar.Maui.Tests;

public class MauiFontCatalogTests
{
    private const string FontFile = "Fonts/Ubuntu-Regular.ttf";

    [Fact]
    public void TryResolve_AliasAndNormal_ReturnsThePackagedFace()
    {
        var catalog = new MauiFontCatalog();
        catalog.Register(FontFile, "OpenSansRegular", assembly: null);

        var typeface = catalog.TryResolve("OpenSansRegular", FontStyle.Normal, new AppPackageFileSystem());

        Assert.NotNull(typeface);
        Assert.Equal("Ubuntu", typeface.FamilyName);
    }

    [Fact]
    public void TryResolve_BoldAgainstASingleAlias_ReturnsNull()
    {
        var catalog = new MauiFontCatalog();
        catalog.Register(FontFile, "OpenSansRegular", assembly: null);

        Assert.Null(catalog.TryResolve("OpenSansRegular", FontStyle.Bold, new AppPackageFileSystem()));
    }

    [Fact]
    public void TryResolve_UnknownAlias_ReturnsNull()
    {
        var catalog = new MauiFontCatalog();

        Assert.Null(catalog.TryResolve("NoSuchAlias", FontStyle.Normal, new AppPackageFileSystem()));
    }

    [Fact]
    public void TryResolve_MissingPackageFile_ReturnsNull()
    {
        var catalog = new MauiFontCatalog();
        catalog.Register("Fonts/NoSuchFont.ttf", "Missing", assembly: null);

        Assert.Null(catalog.TryResolve("Missing", FontStyle.Normal, new AppPackageFileSystem()));
    }

    [Fact]
    public void TryResolve_EmbeddedRegistration_ReadsTheAssemblyResource()
    {
        var catalog = new MauiFontCatalog();
        catalog.Register("Ubuntu-Regular.ttf", "EmbeddedUbuntu", typeof(MauiFontCatalogTests).Assembly);

        var typeface = catalog.TryResolve("EmbeddedUbuntu", FontStyle.Normal, new AppPackageFileSystem());

        Assert.NotNull(typeface);
        Assert.Equal("Ubuntu", typeface.FamilyName);
    }
}
