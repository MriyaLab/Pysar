using Pysar.Core.Structs;

namespace Pysar.Skia.Layout;

/// <summary>Measurement constraint. Replaces the legacy child.Size/Position mutations.</summary>
public readonly record struct MeasureConstraint(
    Rect AvailableRect,
    SizeLength? WidthOverride = null,
    SizeLength? HeightOverride = null,
    bool IgnorePosition = false);   // true for bands: Position/At has no effect on a band
