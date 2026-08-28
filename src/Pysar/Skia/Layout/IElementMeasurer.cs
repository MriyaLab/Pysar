using Pysar.Core.Abstractions;

namespace Pysar.Skia.Layout;

/// <summary>
///     Extension seam for custom elements that know their own content size. The measure counterpart
///     of <see cref="Pysar.Skia.Rendering.IElementDrawer"/>: a drawer says how an element is
///     painted into bounds already decided, this says what those bounds should be.
/// </summary>
/// <remarks>
///     Only consulted for a dimension the author left <c>Auto</c>. A fixed size is an instruction and
///     a filling one is a share of the parent - neither is a question for the element.
/// </remarks>
public interface IElementMeasurer
{
    /// <summary>
    ///     The size <paramref name="element"/>'s content needs within <paramref name="available"/>.
    /// </summary>
    /// <param name="available">
    ///     What is left after the element's own margin, in points. A dimension the author did not
    ///     leave <c>Auto</c> is already resolved in it, so an element that scales to its width can
    ///     read it. Only the <c>Auto</c> dimensions of the result are used.
    /// </param>
    (float Width, float Height) Measure(
        IReportElement element, (float Width, float Height) available, MeasureContext ctx);
}
