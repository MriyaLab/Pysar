using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;

namespace Pysar.Viewer;

/// <summary>
///     Everything the presenter is allowed to know about a user interface.
/// </summary>
/// <remarks>
///     Deliberately small: a control implements this, and nothing else about the control reaches the
///     presenter. Pages and cells are placed as real views because the platform then moves them
///     itself while scrolling - no application repaint keeps pace with a finger.
/// </remarks>
public interface IReportViewHost
{
    double ViewportWidth { get; }

    double ViewportHeight { get; }

    double ScrollX { get; }

    double ScrollY { get; }

    /// <summary>Device pixels per layout unit.</summary>
    double Density { get; }

    /// <summary>
    ///     Scrolls the view. May complete asynchronously, but must not wait on a later
    ///     <see cref="Post"/>: the presenter issues the scroll in the same turn as
    ///     <see cref="SetExtent"/> and the page layout so those three share one frame.
    /// </summary>
    void ScrollTo(double x, double y);

    /// <summary>
    ///     Sets how much there is to scroll. Must take effect for a following <see cref="ScrollTo"/>
    ///     in the same call stack - force layout if the platform only sizes content on a later pass -
    ///     so the scroll is not clamped against the previous document size.
    /// </summary>
    void SetExtent(double width, double height);

    void PlacePage(int index, ViewRect bounds);

    /// <summary>
    ///     Puts a drawn cell on screen. The tile carries its own key, the region of the page it
    ///     covers, its bytes in whatever form this host asked for, and their size in pixels.
    /// </summary>
    void PlaceTile(ViewRect bounds, Tile tile);

    void RemoveTile(TileKey key);

    /// <summary>Runs an action on the thread the user interface belongs to.</summary>
    void Post(Action action);
}
