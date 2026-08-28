using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Skia.Helpers;
using Pysar.Skia.Layout;
using SkiaSharp;

namespace Pysar.Skia.Rendering;

/// <summary>
///     Thin draw phase over a <see cref="LayoutNode"/> tree. Reads absolute coordinates from the node
///     (never from the element), paints background/border, then resolves a drawer uniformly:
///     a registered <see cref="IElementDrawer"/> for the element's exact type (built-in Text/Image
///     drawers live in the default registry alongside custom ones), otherwise the structural container
///     fallback (clip + draw children in ZIndex order). Only containers — identified by the
///     <see cref="IReportContainer"/> interface rather than an exact type — are handled structurally.
/// </summary>
public static class ElementDrawer
{
    private static readonly DrawerRegistry DefaultDrawers = DrawerRegistry.CreateDefault();

    public static void Draw(LayoutNode node, RenderContext ctx, DrawerRegistry? drawers = null)
    {
        var element = node.Element;
        if (!element.IsVisible) return;

        // Region tiles pass a padded cull rect in the same point space as Bounds; skip work that
        // cannot affect any pixel of the tile (shadows/bleed are covered by the pad).
        if (ctx.CullBoundsPt is { } cull && !Intersects(node.Bounds, cull))
            return;

        var boundsPx = node.Bounds.ToSkiaRect(ctx.Scale);
        RenderHelper.DrawBackground(ctx.Canvas, element.BackgroundColor.ToSkiaColor(), boundsPx);
        RenderHelper.DrawBorder(ctx.Canvas, element.BorderColor.ToSkiaColor(), element.BorderThickness,
            element.BorderLineStyle, boundsPx, ctx.Scale);

        var registry = drawers ?? DefaultDrawers;

        // Built-in and custom leaf drawers are looked up the same way, by exact type.
        if (registry.TryGet(element.GetType(), out var drawer))
        {
            drawer.Draw(node, ctx);
            return;
        }

        // Containers are polymorphic (any IReportContainer), so they can't be keyed by exact type.
        if (element is IReportContainer container)
            DrawContainer(node, container, ctx, registry);
    }

    private static void DrawContainer(LayoutNode node, IReportContainer container, RenderContext ctx, DrawerRegistry registry)
    {
        var clipped = container.IsClippedToBounds;
        if (clipped)
        {
            // node.Bounds is already the border-box (margin is outside it after measurement), so clip the
            // children directly to it. Re-insetting by the margin would double-count it and cut off content
            // whenever the container has a non-zero margin.
            ctx.Canvas.Save();
            ctx.Canvas.ClipRect(node.Bounds.ToSkiaRect(ctx.Scale));
        }

        try
        {
            foreach (var child in node.Children.OrderBy(n => n.Element.ZIndex))
                Draw(child, ctx, registry);
        }
        finally
        {
            if (clipped) ctx.Canvas.Restore();
        }
    }

    private static bool Intersects(Rect bounds, SKRect cull) =>
        bounds.Left < cull.Right
        && bounds.Right > cull.Left
        && bounds.Top < cull.Bottom
        && bounds.Bottom > cull.Top;
}
