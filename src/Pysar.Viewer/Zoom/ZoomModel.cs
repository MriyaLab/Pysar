using Pysar.Viewer.Geometry;

namespace Pysar.Viewer.Zoom;

/// <summary>How the viewer decides the zoom.</summary>
public enum ReportZoomMode
{
    FitWidth,
    FitPage,
    Custom
}

/// <summary>
///     What the zoom is: the mode, the factor a custom mode uses, and what the current mode resolves
///     to against the viewport it is shown in.
/// </summary>
/// <remarks>
///     Where the scroll has to move for a zoom to keep a point of the document still is deliberately
///     not here: it belongs to <see cref="ReportViewPresenter"/>, which is the only thing that knows
///     where the pages are.
/// </remarks>
public sealed class ZoomModel
{
    public const double MinimumZoom = 0.25;
    public const double MaximumZoom = 5;

    public ReportZoomMode Mode { get; set; } = ReportZoomMode.FitWidth;

    /// <summary>The factor used when <see cref="Mode"/> is <see cref="ReportZoomMode.Custom"/>.</summary>
    public double Zoom { get; set; } = 1;

    public double PagePointWidth { get; set; } = 595.5;

    public double PagePointHeight { get; set; } = 842;

    /// <summary>Layout units per point at a zoom of 1.</summary>
    public double UnitsPerPoint { get; set; } = ReportViewDefaults.UnitsPerPoint;

    public double ViewportWidth { get; set; } = 1;

    public double ViewportHeight { get; set; } = 1;

    public PagePadding Padding { get; set; }

    /// <summary>The zoom the current mode resolves to, whatever the mode.</summary>
    public double EffectiveZoom
    {
        get
        {
            // A fit mode fits the page and the space around it together, since that space is part of
            // the document and scales with the zoom: it is the whole of it that has to fit the
            // viewport, or the page would be pushed out under its own padding and scroll sideways.
            var width = PagePointWidth * UnitsPerPoint + Padding.Left + Padding.Right;
            var height = PagePointHeight * UnitsPerPoint + Padding.Top + Padding.Bottom;

            var zoom = Mode switch
            {
                ReportZoomMode.FitWidth => PageViewport.FitWidthZoom(ViewportWidth, width),
                ReportZoomMode.FitPage =>
                    PageViewport.FitPageZoom(ViewportWidth, ViewportHeight, width, height),
                _ => Zoom
            };

            return Math.Clamp(zoom, MinimumZoom, MaximumZoom);
        }
    }

}
