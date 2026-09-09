using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;

namespace Pysar.Skia.Layout;

/// <summary>
///     Resolves an element's top-left corner within an available rect:
///     explicit Position (At) is an offset from the rect's origin; otherwise
///     Horizontal/Vertical alignment applies. Default alignment is Start.
/// </summary>
internal static class PositionResolver
{
    public static (float Left, float Top) Resolve(IReportElement element, float width, float height, Rect availableRect)
    {
        if (!element.Position.IsEmpty)
            return (availableRect.Left + (element.Position.X ?? 0),
                    availableRect.Top + (element.Position.Y ?? 0));

        var left = element.HorizontalAlignment switch
        {
            Alignment.Center => availableRect.Left + (availableRect.Width - width) / 2,
            Alignment.End => availableRect.Right - width,
            _ => availableRect.Left
        };
        var top = element.VerticalAlignment switch
        {
            Alignment.Center => availableRect.Top + (availableRect.Height - height) / 2,
            Alignment.End => availableRect.Bottom - height,
            _ => availableRect.Top
        };
        return (left, top);
    }

    /// <summary>
    ///     The room a child needs inside a parent axis that shrink-wraps (Auto): its margins, an
    ///     explicit <see cref="IReportElement.Position"/> offset and its own size. Alignment is left
    ///     out by design - an Auto axis has no free space, so resolving Center/End against the
    ///     parent's probe window would fold that window's slack into the parent's box.
    /// </summary>
    public static (float Width, float Height) ShrinkWrapExtent(IReportElement element, float width, float height)
    {
        var offsetX = element.Position.IsEmpty ? 0f : element.Position.X ?? 0f;
        var offsetY = element.Position.IsEmpty ? 0f : element.Position.Y ?? 0f;
        return (element.Margin.Left + offsetX + width + element.Margin.Right,
                element.Margin.Top + offsetY + height + element.Margin.Bottom);
    }
}
