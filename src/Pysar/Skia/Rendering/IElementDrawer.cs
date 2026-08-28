using Pysar.Skia.Layout;

namespace Pysar.Skia.Rendering;

/// <summary>
///     Extension seam for custom elements. A drawer paints one element type into its already-measured
///     bounds (<c>node.Bounds</c>); it never measures or mutates the model. Background and border are
///     painted by <see cref="ElementDrawer"/> before the drawer runs, so a drawer only adds content.
/// </summary>
public interface IElementDrawer
{
    void Draw(LayoutNode node, RenderContext ctx);
}
