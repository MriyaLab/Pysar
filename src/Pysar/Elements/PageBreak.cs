using Pysar.Binding;
using Pysar.Core.Structs;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
///     An invisible, zero-height flow marker that forces a page break at its position: content after the
///     marker starts on a new page. Place it anywhere in the flow (e.g. at the end of a group footer so
///     the next group prints on a fresh page). The paginator cuts at the marker's top edge; the marker
///     itself draws nothing.
/// </summary>
public sealed class PageBreak : ReportElement<PageBreak>
{
    public PageBreak()
    {
        // Full width so it stacks like a normal block, but no height — it only marks a y position.
        // At Default precedence: a type's own default must not shadow a later style (see Text).
        using var defaults = PushWritePrecedence(ValuePrecedence.Default);
        Size = new Size(SizeLength.Fill, SizeLength.Fixed(0));
    }
}
