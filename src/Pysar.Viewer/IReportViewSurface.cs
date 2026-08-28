namespace Pysar.Viewer;

/// <summary>
///     The part of a report view only its own framework can do: building the views a tile is shown
///     in, and carrying presenter state back out to the host's own properties.
/// </summary>
/// <remarks>
///     Distinct from <see cref="IReportViewHost"/>, which is the viewport a presenter measures
///     against. This is what <see cref="ReportViewController"/> calls at the points its sequence
///     reaches something platform-bound - deliberately narrow, because everything that can be
///     answered without a framework belongs in the controller instead.
/// </remarks>
public interface IReportViewSurface
{
    /// <summary>Puts the tiles the presenter settled on into the host's own views.</summary>
    void RefreshVisuals();

    /// <summary>Drops every view built for the report that has just gone.</summary>
    void ClearVisuals();

    /// <summary>
    ///     Asks the drawing surface to repaint. Hosts whose surface repaints by itself when its
    ///     children change leave this empty.
    /// </summary>
    void InvalidateSurface();

    /// <summary>
    ///     True while an input gesture owns the viewport - a running pinch, a scroll the host is
    ///     driving itself. The controller then leaves the presenter alone, because the gesture has
    ///     already told it where it put things.
    /// </summary>
    bool SuppressesViewportReaction { get; }

    /// <summary>How far beyond the viewport to draw, and how many cells one pass may draw.</summary>
    (double VerticalOverdraw, double RenderBudget) TilePolicy { get; }

    /// <summary>
    ///     The presenter's page and zoom have moved; write them to whatever the host exposes them as.
    ///     The host is responsible for not treating its own write as a fresh request from a binding.
    /// </summary>
    void ReportState(int currentPage, double effectiveZoom);
}
