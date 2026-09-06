using Pysar.Core.Enums;
using Pysar.Elements;

namespace Pysar.Export;

/// <summary>
///     Paper size and orientation the host print UI should request, taken from a report's
///     <see cref="PageFormat"/>. Width and height are PDF points (1/72 inch).
/// </summary>
public readonly record struct PrintPaper(
    float WidthPt,
    float HeightPt,
    bool IsLandscape,
    string? PaperName)
{
    /// <summary>
    ///     Builds print-dialog paper from <paramref name="pageFormat"/>, including ISO paper names
    ///     and portrait mils Android's <c>MediaSize</c> constructor requires.
    /// </summary>
    public static PrintPaper From(PageFormat pageFormat)
    {
        ArgumentNullException.ThrowIfNull(pageFormat);

        var (width, height) = pageFormat.GetPageSizePt();
        return new PrintPaper(
            width,
            height,
            pageFormat.Orientation == Orientation.Landscape,
            pageFormat.Size == PageSize.A4 ? "iso-a4" : null);
    }

    /// <summary>Portrait-side width in mils (1/1000 inch) for Android <c>PrintAttributes.MediaSize</c>.</summary>
    public int PortraitWidthMils => Math.Min(PointsToMils(WidthPt), PointsToMils(HeightPt));

    /// <summary>Portrait-side height in mils (1/1000 inch) for Android <c>PrintAttributes.MediaSize</c>.</summary>
    public int PortraitHeightMils => Math.Max(PointsToMils(WidthPt), PointsToMils(HeightPt));

    private static int PointsToMils(float points)
        => (int)Math.Round(points * 1000d / 72d);
}
