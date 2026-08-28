using Pysar.Elements;
using Pysar.Skia.Layout;

namespace Pysar.Skia.Rendering;

/// <summary>Built-in drawer for <see cref="Image"/> — places the loaded bitmap into the node's bounds.</summary>
internal sealed class ImageDrawer : IElementDrawer
{
    public void Draw(LayoutNode node, RenderContext ctx) =>
        ImageRenderer.Draw((Image)node.Element, node.Bounds, ctx);
}
