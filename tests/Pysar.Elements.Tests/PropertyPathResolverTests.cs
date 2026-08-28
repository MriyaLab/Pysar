using Xunit;

namespace Pysar.Elements.Tests;

public class PropertyPathResolverTests
{
    private sealed record Inner(string Name);
    private sealed record Outer(Inner Child);

    [Fact]
    public void Resolve_SimpleProperty_ReturnsValue()
    {
        var src = new Inner("Ada");
        Assert.Equal("Ada", PropertyPathResolver.Resolve(src, "Name"));
    }

    [Fact]
    public void Resolve_DottedPath_WalksNestedProperties()
    {
        var src = new Outer(new Inner("Bob"));
        Assert.Equal("Bob", PropertyPathResolver.Resolve(src, "Child.Name"));
    }

    [Fact]
    public void Resolve_Dictionary_LooksUpKey()
    {
        var src = new Dictionary<string, object> { ["Name"] = "Cleo" };
        Assert.Equal("Cleo", PropertyPathResolver.Resolve(src, "Name"));
    }

    [Fact]
    public void Resolve_MissingSegment_ReturnsNull()
    {
        var src = new Inner("Ada");
        Assert.Null(PropertyPathResolver.Resolve(src, "Nope"));
    }

    [Fact]
    public void Resolve_NullSourceOrEmptyPath_ReturnsNull()
    {
        Assert.Null(PropertyPathResolver.Resolve(null, "Name"));
        Assert.Null(PropertyPathResolver.Resolve(new Inner("Ada"), ""));
    }
}
