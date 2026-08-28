using Pysar.Core.Abstractions;

namespace Pysar.Skia.Layout;

/// <summary>
///     Maps element types to their custom <see cref="IElementMeasurer"/>. Populated via
///     <c>SkiaReportRenderer.WithMeasurer&lt;T&gt;</c>. Types without one keep the leaf-box rule,
///     where an <c>Auto</c> dimension resolves to zero.
/// </summary>
/// <remarks>
///     Deliberately empty by default, unlike <see cref="Rendering.DrawerRegistry"/>: the built-in
///     elements that have an intrinsic size are measured by the layout engine's own switch, which
///     knows how to recurse into their children. A measurer answers with a size, which is the right
///     shape for a leaf and not for a container.
/// </remarks>
public sealed class MeasurerRegistry
{
    private readonly Dictionary<Type, IElementMeasurer> _measurers = new();

    public int Count => _measurers.Count;

    public void Register<T>(IElementMeasurer measurer) where T : IReportElement
    {
        ArgumentNullException.ThrowIfNull(measurer);

        _measurers[typeof(T)] = measurer;
    }

    public bool TryGet(Type type, out IElementMeasurer measurer)
        => _measurers.TryGetValue(type, out measurer!);
}
