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
///     An <see cref="IRoundedElement"/> rounds its background and border, and its child clip follows
///     the same rounded box.
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
        if (ctx.CullBoundsPt is { } cull && !Intersects(node.Bounds, cull, element.Rotation))
            return;

        var opacity = Math.Clamp(element.Opacity, 0f, 1f);
        var rotation = element.Rotation;
        SKPaint? layerPaint = null;
        var saved = false;
        if (opacity < 1f)
        {
            var alpha = (byte)Math.Clamp((int)MathF.Round(opacity * 255f), 0, 255);
            layerPaint = new SKPaint { Color = SKColors.White.WithAlpha(alpha) };
            ctx.Canvas.SaveLayer(layerPaint);
            saved = true;
        }
        else if (rotation != 0f)
        {
            ctx.Canvas.Save();
            saved = true;
        }

        try
        {
            var boundsPx = node.Bounds.ToSkiaRect(ctx.Scale);
            if (rotation != 0f)
                ctx.Canvas.RotateDegrees(rotation, boundsPx.MidX, boundsPx.MidY);

            var radius = (element as IRoundedElement)?.CornerRadius ?? CornerRadius.Zero;
            RenderHelper.DrawBackground(ctx.Canvas, element.BackgroundColor.ToSkiaColor(), boundsPx, radius,
                element.BorderThickness, ctx.Scale);
            RenderHelper.DrawBorder(ctx.Canvas, element.BorderColor.ToSkiaColor(), element.BorderThickness,
                element.BorderLineStyle, boundsPx, ctx.Scale, radius);

            var registry = drawers ?? DefaultDrawers;

            // Built-in and custom leaf drawers are looked up the same way, by exact type.
            if (registry.TryGet(element.GetType(), out var drawer))
            {
                drawer.Draw(node, ctx);
                return;
            }

            // Containers are polymorphic (any IReportContainer), so they can't be keyed by exact type.
            if (element is IReportContainer container)
                DrawContainer(node, container, ctx, registry, radius);
        }
        finally
        {
            if (saved) ctx.Canvas.Restore();
            layerPaint?.Dispose();
        }
    }

    private static void DrawContainer(
        LayoutNode node, IReportContainer container, RenderContext ctx, DrawerRegistry registry, CornerRadius radius)
    {
        var clipped = container.IsClippedToBounds;
        if (clipped)
        {
            // node.Bounds is already the border-box (margin is outside it after measurement), so clip the
            // children directly to it. Re-insetting by the margin would double-count it and cut off content
            // whenever the container has a non-zero margin.
            var boundsPx = node.Bounds.ToSkiaRect(ctx.Scale);
            ctx.Canvas.Save();
            if (radius.IsZero)
                ctx.Canvas.ClipRect(boundsPx);
            else
                // Rounded: clip to the inner (padding-box) arc, so a child's square corner cannot
                // paint over the border curve. With no border this is the border-box arc itself.
                ctx.Canvas.ClipRoundRect(
                    RenderHelper.ToInnerRoundRect(boundsPx, radius, node.Element.BorderThickness, ctx.Scale),
                    SKClipOperation.Intersect, antialias: true);
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

    private static bool Intersects(Rect bounds, SKRect cull, float rotation = 0f)
    {
        if (rotation == 0f)
        {
            return bounds.Left < cull.Right
                && bounds.Right > cull.Left
                && bounds.Top < cull.Bottom
                && bounds.Bottom > cull.Top;
        }

        var cx = (bounds.Left + bounds.Right) * 0.5f;
        var cy = (bounds.Top + bounds.Bottom) * 0.5f;
        var rad = rotation * (MathF.PI / 180f);
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        ReadOnlySpan<(float X, float Y)> corners =
        [
            (bounds.Left, bounds.Top),
            (bounds.Right, bounds.Top),
            (bounds.Right, bounds.Bottom),
            (bounds.Left, bounds.Bottom)
        ];
        foreach (var (x, y) in corners)
        {
            var dx = x - cx;
            var dy = y - cy;
            var rx = cx + dx * cos - dy * sin;
            var ry = cy + dx * sin + dy * cos;
            if (rx < minX) minX = rx;
            if (ry < minY) minY = ry;
            if (rx > maxX) maxX = rx;
            if (ry > maxY) maxY = ry;
        }

        return minX < cull.Right
            && maxX > cull.Left
            && minY < cull.Bottom
            && maxY > cull.Top;
    }
}
