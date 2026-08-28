namespace Pysar.Viewer;

/// <summary>
///     The values a report viewer starts from, wherever it is hosted.
/// </summary>
/// <remarks>
///     One home, because a host that keeps its own copy is a host that can disagree with the core
///     without anything failing: the zoom limits used to be declared once in <c>ZoomModel</c>, where
///     the arithmetic clamps, and again in every control, where the bindable property coerces. The
///     limits stay in <see cref="Zoom.ZoomModel"/>, where that arithmetic lives, and this class
///     deliberately does not re-export them - a host is expected to coerce against
///     <see cref="Zoom.ZoomModel.MinimumZoom"/> / <see cref="Zoom.ZoomModel.MaximumZoom"/> directly
///     rather than against a copy of its own.
/// </remarks>
public static class ReportViewDefaults
{
    /// <summary>
    ///     Layout units per report point at 100%: 96 to the inch against the point's 72, which is
    ///     what a browser's PDF viewer calls 100%. A platform that rescales its whole interface
    ///     corrects for that on top, through <see cref="ReportViewPresenter.UnitsPerPoint"/>.
    /// </summary>
    public const double UnitsPerPoint = 96d / 72d;

    /// <summary>The gap between two pages at 100%; part of the document, so it scales with the zoom.</summary>
    public const double PageSpacing = 24;

    /// <summary>The width of the line around each page; 0 leaves the page unframed.</summary>
    public const double PageBorderThickness = 1;

    /// <summary>
    ///     The line around a page, as hex. A string rather than a colour: every host has a colour
    ///     type of its own and every one of them parses this form.
    /// </summary>
    /// <remarks>
    ///     Must stay six-digit <c>#RRGGBB</c>: an eight-digit form is not read the same way
    ///     everywhere. WPF's <c>ColorConverter</c> and MAUI's <c>Color.FromArgb</c> would read it as
    ///     <c>#AARRGGBB</c>, while Blazor hands the string to CSS, which reads it as
    ///     <c>#RRGGBBAA</c> - the same eight digits would name two different colours depending on the
    ///     host.
    /// </remarks>
    public const string PageBorderColorHex = "#8A8F98";

    /// <summary>
    ///     How far past the top and the bottom of the viewport a cell is drawn, as a fraction of the
    ///     viewport height.
    /// </summary>
    public const double VerticalOverdraw = 0.25;

    /// <summary>
    ///     How much memory, in megabytes, the cells requested for one view may occupy before the
    ///     ones furthest from the middle of the viewport are dropped.
    /// </summary>
    /// <remarks>
    ///     Counted against the pixels a cell occupies once decoded, which at a cell side of
    ///     <see cref="Tiles.TilePlanner.TileSidePx"/> is 4 MB each - so this is around fifty cells:
    ///     several screens' worth on a laptop display, and not enough to strain a phone. It is not
    ///     what decides whether a page is drawn whole; that is
    ///     <see cref="Tiles.TilePlanner.WholePageCellLimit"/>.
    /// </remarks>
    public const double RenderBudget = 192;

    /// <summary>
    ///     How far the zoom has to move, as a fraction of itself, before a gesture is worth applying.
    ///     Each application relays out every page and cell, so following a notch that changed almost
    ///     nothing would spend that cost on a change too small to see. Relative rather than absolute
    ///     because a step of 0.01 is a hundredth of the view at 100% and a four-hundredth at 400%,
    ///     where the relayouts are also at their most expensive.
    /// </summary>
    public const double ZoomStepThreshold = 0.01;

    /// <summary>How long the view has to hold still before the sharp cells are worth rendering.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(80);
}
