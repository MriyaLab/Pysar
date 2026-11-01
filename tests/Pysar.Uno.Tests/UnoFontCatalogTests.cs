using System.Reflection;
using Pysar.Core.Enums;
using Pysar.Uno;
using Xunit;

namespace Pysar.Uno.Tests;

public class UnoFontCatalogTests
{
    private static Assembly Assets => typeof(UnoFontCatalogTests).Assembly;

    [Fact]
    public void TryResolve_FamilyNameAndNormal_ReturnsTheEmbeddedFace()
    {
        var typeface = UnoFontCatalog.TryResolve(Assets, "Ubuntu", FontStyle.Normal);

        Assert.NotNull(typeface);
        Assert.Equal("Ubuntu", typeface.FamilyName);
    }

    [Fact]
    public void TryResolve_BoldAgainstARegularFile_ReturnsNull()
    {
        Assert.Null(UnoFontCatalog.TryResolve(Assets, "Ubuntu", FontStyle.Bold));
    }

    [Fact]
    public void TryResolve_UnknownFamily_ReturnsNull()
    {
        Assert.Null(UnoFontCatalog.TryResolve(Assets, "NoSuchFamily", FontStyle.Normal));
    }
    [Fact]
    public void TryResolve_AnotherAssembly_DoesNotReuseTheFirstAssemblyFont()
    {
        Assert.NotNull(UnoFontCatalog.TryResolve(Assets, "Ubuntu", FontStyle.Normal));

        Assert.Null(UnoFontCatalog.TryResolve(typeof(object).Assembly, "Ubuntu", FontStyle.Normal));
    }
}
