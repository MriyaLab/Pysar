using Pysar.Core.Abstractions;
using Pysar.Core.Structs;

namespace Pysar.Skia.Layout;

/// <summary>Immutable measurement result. Coordinates are absolute, on the ribbon.</summary>
public sealed record LayoutNode(
    IReportElement Element,
    Rect Bounds,
    IReadOnlyList<LayoutNode> Children,
    IReadOnlyList<float> CutHints)
{
    public static readonly IReadOnlyList<LayoutNode> NoChildren = [];
    public static readonly IReadOnlyList<float> NoCuts = [];
}
