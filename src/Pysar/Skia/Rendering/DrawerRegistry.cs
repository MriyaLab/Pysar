using Pysar.Core.Abstractions;

namespace Pysar.Skia.Rendering;

/// <summary>
///     Maps element types to their custom <see cref="IElementDrawer"/>. Populated via
///     <c>SkiaReportRenderer.WithDrawer&lt;T&gt;</c>. Types without a registered drawer fall back
///     to the built-in draw logic in <see cref="ElementDrawer"/>.
/// </summary>
public sealed class DrawerRegistry
{
    private readonly Dictionary<Type, IElementDrawer> _drawers = new();

    public int Count => _drawers.Count;

    /// <summary>A registry pre-seeded with the built-in leaf drawers (Text, Image).</summary>
    public static DrawerRegistry CreateDefault()
    {
        var registry = new DrawerRegistry();
        registry.Register<Elements.Text>(new TextDrawer());
        registry.Register<Elements.Image>(new ImageDrawer());
        return registry;
    }

    public void Register<T>(IElementDrawer drawer) where T : IReportElement
    {
        ArgumentNullException.ThrowIfNull(drawer);
        _drawers[typeof(T)] = drawer;
    }

    public bool TryGet(Type type, out IElementDrawer drawer) => _drawers.TryGetValue(type, out drawer!);
}
