using Pysar.Core.Abstractions;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
///     Build-time application of explicit and implicit styles. Walks bands and their children
///     (including <see cref="Repeater.Header"/>/<see cref="Repeater.Footer"/>) and applies
///     <see cref="StyleApplicator"/> before the binding pipeline runs.
/// </summary>
public static class StyleEngine
{
    public static void Apply(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);

        foreach (var band in report.Bands)
            Walk(band, report.Resources);
    }

    private static void Walk(IReportElement element, ResourceDictionary resources)
    {
        if (element is ReportObject reportObject)
            ApplyTo(reportObject, resources);

        if (element is Repeater repeater)
        {
            if (repeater.Header is not null)
                Walk(repeater.Header, resources);
            if (repeater.Footer is not null)
                Walk(repeater.Footer, resources);
        }

        if (element is IReportContainer { Children.Count: > 0 } container)
        {
            foreach (var child in container.Children)
                Walk(child, resources);
        }
    }

    private static void ApplyTo(ReportObject reportObject, ResourceDictionary resources)
    {
        if (TryGetImplicitStyle(resources, reportObject.GetType(), out var implicitStyle))
            StyleApplicator.Apply(reportObject, implicitStyle);
        if (reportObject.Style is not null)
            StyleApplicator.Apply(reportObject, reportObject.Style);
    }

    private static bool TryGetImplicitStyle(ResourceDictionary resources, Type type, out Style style)
    {
        if (resources.ContainsKey(type) && resources[type] is Style found)
        {
            style = found;
            return true;
        }

        style = null!;
        return false;
    }
}
