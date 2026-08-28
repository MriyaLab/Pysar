using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
/// Root of a reusable XAML component. A component is an ordinary container in the element tree —
/// layout treats it structurally (see LayoutEngine's IReportContainer branch) — but it owns its own
/// resource scope and is the only root besides &lt;Report&gt; that may carry x:Class.
/// </summary>
public class ReportView : ReportContainer<ReportView>, IResourceHost
{
    [System.Windows.Markup.Ambient]
    public ResourceDictionary Resources { get; } = new();

    public override IReportElement Clone()
    {
        var clone = (ReportView)base.Clone();
        RemapSources(clone, from: this, to: clone);
        return clone;
    }

    /// <summary>
    /// Repoints every descendant binding that referenced the original component at the clone.
    /// CopyStateTo carries the binding store verbatim, so without this pass each expanded row would
    /// read the original instance and every row would render identical values.
    /// </summary>
    private static void RemapSources(IReportElement element, object from, object to)
    {
        if (element is BindableObject bindable)
            bindable.RemapBindingSource(from, to);

        if (element is IReportContainer container)
            foreach (var child in container.Children)
                RemapSources(child, from, to);
    }
}
