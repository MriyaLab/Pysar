using Pysar.Core.Abstractions;

namespace Pysar.Skia.Layout;

/// <summary>Measurement services. No canvas — the measure phase never draws.</summary>
/// <remarks>
///     One context measures one thing at a time. It was stateless until it began remembering probe
///     sizes, and the state it now carries - the dictionary and the depth counter below - is plain,
///     so two threads measuring through the same instance would race the counter and corrupt the
///     dictionary.
///     <para>
///     That is not theoretical in this codebase: a viewer renders cells on several threads at once,
///     and each of them can reach <c>PageBandResolver</c>, which re-measures the page bands through
///     the one context the report was measured with. What keeps those calls apart today is
///     <c>ReportRenderSession</c>'s <c>_resolveGate</c>, held across the whole resolve. That
///     semaphore exists to freeze each page's bands before they are cached, and this type's safety
///     is a second thing it happens to buy - so anything that narrows it has to give this its own
///     protection instead.
///     </para>
/// </remarks>
public sealed class MeasureContext(float scale)
{
    public float Scale { get; } = scale;

    /// <summary>
    ///     Measurers registered for custom element types. Empty unless the application registered
    ///     one, in which case an <c>Auto</c> dimension on such an element is asked of it rather than
    ///     resolved to zero.
    /// </summary>
    public MeasurerRegistry Measurers { get; init; } = new();

    /// <summary>
    ///     Sizes already established for a child a container probed, within the current measure.
    /// </summary>
    /// <remarks>
    ///     Only sizes, never nodes. A container probes a child to size its tracks and then measures
    ///     it again to place it, and the two want different things: the probe reads a width or a
    ///     height and throws the node away, while the placement needs a node whose bounds are the
    ///     cell it actually landed in. Handing the placement a remembered node would alias one
    ///     element's geometry into several places in the tree - so the node is always built afresh
    ///     and only the answer to "how big is this" is kept.
    /// </remarks>
    private readonly Dictionary<ProbeKey, (float Width, float Height)> _probeSizes = [];

    /// <summary>
    ///     How deep the engine currently is inside <see cref="LayoutEngine.Measure"/>, so the
    ///     outermost call can be told from the recursion it starts.
    /// </summary>
    private int _depth;

    /// <summary>
    ///     A child is identified by reference, not by value: the repeater deep-clones its row
    ///     template once per data item before measurement ever runs, so two rows showing different
    ///     figures are two objects and cannot collide here. No element type overrides
    ///     <c>Equals</c>, which is what keeps the default comparer's answer reference identity.
    /// </summary>
    private readonly record struct ProbeKey(IReportElement Element, MeasureConstraint Constraint);

    /// <summary>Notes that a measure has begun. Pair with <see cref="EndMeasure"/>.</summary>
    internal void BeginMeasure() => _depth++;

    /// <summary>
    ///     Notes that a measure has finished, discarding the probe sizes once the outermost one
    ///     returns.
    /// </summary>
    /// <remarks>
    ///     The cache lasts exactly one outer measure, and the engine enforces that itself rather
    ///     than asking callers to remember it. That is load-bearing: a context outlives the layout
    ///     it was built for - <c>PageBandResolver</c> keeps the one the report was measured with and
    ///     re-measures the page bands through it after stamping each new page number onto them. A
    ///     cache that survived would answer page two with the width of page one's footer, and the
    ///     elements are the same objects under the same constraint, so nothing else would catch it.
    /// </remarks>
    internal void EndMeasure()
    {
        _depth--;

        if (_depth <= 0)
        {
            _depth = 0;
            _probeSizes.Clear();
        }
    }

    internal bool TryGetProbeSize(
        IReportElement element, MeasureConstraint constraint, out (float Width, float Height) size)
        => _probeSizes.TryGetValue(new ProbeKey(element, constraint), out size);

    /// <summary>
    ///     Remembers a probe's answer, unless there is no measure in progress to remember it for.
    /// </summary>
    /// <remarks>
    ///     The guard is what makes the one-measure lifetime hold however this is called. A probe
    ///     issued outside any measure runs its own <see cref="LayoutEngine.Measure"/>, and that
    ///     call takes the depth from zero to one and back, clearing the cache as it returns - so the
    ///     write that followed would land in a dictionary nothing will clear again, and the next
    ///     probe of that element would read a size belonging to an earlier layout. Refusing to store
    ///     costs such a caller nothing but a re-measure, where caching it would be silently wrong.
    /// </remarks>
    internal void StoreProbeSize(
        IReportElement element, MeasureConstraint constraint, (float Width, float Height) size)
    {
        if (_depth <= 0)
            return;

        _probeSizes[new ProbeKey(element, constraint)] = size;
    }
}
