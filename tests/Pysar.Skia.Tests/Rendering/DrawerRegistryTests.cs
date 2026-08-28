using Pysar.Elements;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class DrawerRegistryTests
{
    [Fact]
    public void CreateDefault_RegistersBuiltInDrawers()
    {
        var registry = DrawerRegistry.CreateDefault();

        Assert.True(registry.TryGet(typeof(Text), out _));
        Assert.True(registry.TryGet(typeof(Image), out _));
    }

    [Fact]
    public void Register_OverridesBuiltInDrawer()
    {
        var registry = DrawerRegistry.CreateDefault();
        var custom = new NoopDrawer();

        registry.Register<Text>(custom);

        Assert.True(registry.TryGet(typeof(Text), out var resolved));
        Assert.Same(custom, resolved);
    }

    private sealed class NoopDrawer : IElementDrawer
    {
        public void Draw(Pysar.Skia.Layout.LayoutNode node, RenderContext ctx) { }
    }
}
