namespace Pysar.Viewer.Geometry;

/// <summary>A point of a report: the page it is on, and where it is on that page, in page points.</summary>
/// <remarks>
///     Points rather than layout units, and a page rather than the document, so the same point keeps
///     its identity through a change of zoom - which is what lets a zoom put back whatever the reader
///     was holding. A position in the document would have to be recomputed for every zoom, and
///     anything that does not scale with it - the line around a page, the centring of a document
///     narrower than the viewport - would move that position without moving the point it named.
/// </remarks>
public readonly record struct DocumentPoint(int PageIndex, double XPt, double YPt);

/// <summary>
///     A zoom shown by scaling what is already drawn: the zoom it amounts to, the scale to draw at,
///     and the translation to draw it with.
/// </summary>
/// <remarks>
///     The translation is for a transform applied at the drawing's own origin, and it already accounts
///     for where the view will be scrolled once the zoom is really applied - see
///     <see cref="ReportViewPresenter.PreviewZoom"/>.
/// </remarks>
public readonly record struct ZoomPreview(double Zoom, double Scale, double OffsetX, double OffsetY);
