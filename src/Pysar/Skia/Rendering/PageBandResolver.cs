using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;

namespace Pysar.Skia.Rendering;

/// <summary>
///     Produces the per-page <see cref="LayoutNode"/> for <see cref="PageHeaderBand"/> and
///     <see cref="PageFooterBand"/>: it writes the page number onto the report, re-resolves the bands'
///     bindings and re-measures them.
///     <para>
///     Re-measurement affects the bands' <i>content</i> only. The height they reserve in the content
///     zone was fixed by the one-time measurement in <see cref="ReportLayoutEngine"/> and is never
///     recomputed — that is what keeps pagination independent of the page count it produces.
///     </para>
/// </summary>
internal sealed class PageBandResolver(Report design, ReportLayout layout, MeasureContext measure)
{
    // Either reason is enough to need the per-page pass: bindings to re-resolve, or a report that
    // drives its page bands from code. Both are fixed for the life of a render, so decide once.
    private readonly bool _perPageWork =
        CarriesBinding(design.PageHeader) || CarriesBinding(design.PageFooter) || design.HandlesPageChanged;

    /// <summary>
    ///     Applies <paramref name="pageNumber"/> / <paramref name="pageCount"/> and returns the page
    ///     bands measured for that page. Returns the base layout's nodes unchanged when no page band
    ///     carries a binding — the common case, which then costs nothing.
    ///     <para>
    ///     The returned nodes alias the live design elements rather than snapshotting them, so each
    ///     page must be drawn before the next call: calls are neither reorderable nor safe to run
    ///     concurrently. A node from call N will silently show call N+1's content if drawing is
    ///     deferred past the next <see cref="ResolveAsync"/>.
    ///     </para>
    /// </summary>
    public async Task<(LayoutNode? Header, LayoutNode? Footer)> ResolveAsync(
        int pageNumber, int pageCount, CancellationToken ct)
    {
        if (!_perPageWork)
            return (layout.PageHeader, layout.PageFooter);

        Stamp(design, pageNumber, pageCount);
        ReportLayoutEngine.ResolvePageBands(design);
        // After the bindings, so a hand-written value is not overwritten; before the measure, so it is.
        await design.RaisePageChangedAsync(pageNumber, ct);

        return (await MeasureBandAsync(design.PageHeader, ct),
                await MeasureBandAsync(design.PageFooter, ct));
    }

    /// <summary>
    ///     Re-measures one page band with the same constraint <see cref="ReportLayoutEngine"/> used, so
    ///     the node's geometry stays comparable to the reserved one.
    /// </summary>
    private async Task<LayoutNode?> MeasureBandAsync(Band? band, CancellationToken ct)
    {
        if (band is null) return null;

        var zone = layout.ContentZone;
        return await LayoutEngine.MeasureAsync(band,
            new MeasureConstraint(new Rect(0, 0, zone.Width, zone.Height),
                WidthOverride: SizeLength.Fill, IgnorePosition: true), measure, ct);
    }

    /// <summary>
    ///     Writes the page position onto the report, where <c>{Binding PageNumber, Source={x:Reference Root}}</c>
    ///     reads it. Shared with the seeding measurement so both go through one writer.
    /// </summary>
    internal static void Stamp(Report design, int pageNumber, int pageCount)
    {
        design.PageNumber = pageNumber;
        design.PageCount = pageCount;
    }

    /// <summary>True when the band or any descendant carries a binding.</summary>
    private static bool CarriesBinding(IReportElement? element)
    {
        if (element is null) return false;
        if (element is BindableObject { HasBindings: true }) return true;

        return element is IReportContainer container
               && container.Children.Any(CarriesBinding);
    }
}
