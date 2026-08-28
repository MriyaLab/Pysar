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
}
