using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Skia.Layout;

/// <summary>
///     Report-level measurement. Produces two ribbons: the template ribbon (PageHeader/PageFooter,
///     repeated on every page) and the flow ribbon (ReportHeader → Detail → ReportFooter, stacked
///     and sliced across pages). Template bands are measured first so the flow gets the reduced window.
/// </summary>
public sealed record ReportLayout(
    LayoutNode? PageHeader, float PageHeaderHeight,
    LayoutNode? PageFooter, float PageFooterHeight,
    IReadOnlyList<LayoutNode> Flow,
    float FlowHeight,
    float ContentWindowHeight,
    Rect ContentZone,
    LayoutNode? RepeatDetailHeader = null,
    float RepeatDetailHeaderHeight = 0f);

public static class ReportLayoutEngine
{
    /// <summary>Measures the report into its template and flow ribbons.</summary>
    public static async Task<ReportLayout> MeasureAsync(
        Report design, MeasureContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(design);

        var page = design.PageFormat.GetPageSizePt();
        var m = design.PageFormat.Margin;
        var zone = new Rect(m.Left, m.Top, page.Width - m.Right, page.Height - m.Bottom);

        // Page-band bindings resolve before measuring, so the reserved header/footer heights below
        // reflect real content rather than empty placeholders. Repeating what Build() already did is
        // harmless — resolution is idempotent — and it picks up the page number the renderer just set.
        ResolvePageBands(design);

        // 1. Template ribbon first: bands measured in local (0,0) coordinates, position ignored.
        var header = design.PageHeader is null ? null
            : await LayoutEngine.MeasureAsync(design.PageHeader,
                new MeasureConstraint(new Rect(0, 0, zone.Width, zone.Height),
                    WidthOverride: SizeLength.Fill, IgnorePosition: true), ctx, ct);
        var footer = design.PageFooter is null ? null
            : await LayoutEngine.MeasureAsync(design.PageFooter,
                new MeasureConstraint(new Rect(0, 0, zone.Width, zone.Height),
                    WidthOverride: SizeLength.Fill, IgnorePosition: true), ctx, ct);

        // The template bands are anchored at the content-zone top/bottom and measured from origin y=0.
        // The height they reserve within the content zone is their margin-box height: Bounds.Bottom (box
        // bottom, which already reflects a shifted-up box from a negative top margin) plus the bottom
        // margin. A negative outer margin (top for the header, bottom for the footer) bleeds the band into
        // the page margin and reserves less, so the flow follows without a gap.
        var headerH = (header?.Bounds.Bottom ?? 0) + (design.PageHeader?.Margin.Bottom ?? 0);
        var footerH = (footer?.Bounds.Bottom ?? 0) + (design.PageFooter?.Margin.Bottom ?? 0);
        var windowH = zone.Height - headerH - footerH;
        // Template-only reports (empty flow) may let a page band claim the whole content zone.
        // A negative window still means header+footer oversubscribed the page and is always an error.
        if (windowH < 0 || (windowH == 0 && !IsFlowEmpty(design)))
            throw new InvalidOperationException(
                $"PageHeader ({headerH}pt) + PageFooter ({footerH}pt) leave no room in content zone ({zone.Height}pt).");

        // 2. Flow ribbon: y=0 is the flow start; a band's Fill height grows to the window height.
        var flow = new List<LayoutNode>();
        float y = 0;
        foreach (Band? band in new Band?[] { design.ReportHeader, design.Detail, design.ReportFooter })
        {
            if (band is null) continue;
            ct.ThrowIfCancellationRequested();
            // A flow band sizes to its content: the flow is a variable-height ribbon sliced across
            // pages, so a Fill height (stretch to the window) would inflate every band to a full page
            // and force spurious page breaks. Fixed/Auto heights are honored as-is.
            var heightOverride = band.Size.Height.IsFill ? (SizeLength?)SizeLength.Auto : null;
            var node = await LayoutEngine.MeasureAsync(band,
                new MeasureConstraint(new Rect(0, y, zone.Width, y + windowH),
                    WidthOverride: SizeLength.Fill, HeightOverride: heightOverride, IgnorePosition: true), ctx, ct);
            flow.Add(node);
            // Advance past the band's bottom margin so the next band gets that gap (the box bottom alone
            // omits the bottom margin because a Fixed-height band is measured top-anchored).
            y = node.Bounds.Bottom + band.Margin.Bottom;
        }

        LayoutNode? repeatHeader = null;
        var repeatHeaderHeight = 0f;
        if (design.Detail.RepeatDetailHeaderOnEveryPage && design.Detail.DetailHeader is not null)
        {
            var detailNode = flow.FirstOrDefault(n => ReferenceEquals(n.Element, design.Detail));
            repeatHeader = detailNode is null ? null : FindNode(detailNode, design.Detail.DetailHeader);
            repeatHeaderHeight = repeatHeader?.Bounds.Height ?? 0f;
        }

        return new ReportLayout(header, headerH, footer, footerH, flow, y, windowH, zone,
            repeatHeader, repeatHeaderHeight);
    }

    private static LayoutNode? FindNode(LayoutNode root, IReportElement target)
    {
        if (ReferenceEquals(root.Element, target)) return root;
        foreach (var child in root.Children)
        {
            var found = FindNode(child, target);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>
    ///     True when the flow ribbon will not place any content: no report header/footer with children
    ///     or a fixed height, and an empty detail (typical after expand with no data and no row template).
    /// </summary>
    private static bool IsFlowEmpty(Report design) =>
        !HasFlowBandContent(design.ReportHeader)
        && !HasFlowBandContent(design.Detail)
        && !HasFlowBandContent(design.ReportFooter);

    private static bool HasFlowBandContent(Band? band)
    {
        if (band is null)
            return false;
        if (band.Children.Count > 0)
            return true;
        return band.Size.Height.IsFixed && band.Size.Height.Value > 0;
    }

    /// <summary>
    ///     Re-resolves the page bands' bindings. The report's own data context is the fallback — the same
    ///     one <see cref="Report.Build"/> uses — so an unsourced <c>{Binding CompanyName}</c> in a page band
    ///     keeps reading report data. The page number takes the other route, an explicit
    ///     <c>Source={x:Reference Root}</c> pointing at the report itself, which is why nothing here has to
    ///     hijack the data context to deliver it.
    /// </summary>
    internal static void ResolvePageBands(Report design)
    {
        IReportElement?[] candidates = [design.PageHeader, design.PageFooter];
        new BindingEngine().ResolveBindings(candidates.OfType<IReportElement>(), design.DataContext);
    }
}
