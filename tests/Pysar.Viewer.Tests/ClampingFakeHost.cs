using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;

namespace Pysar.Viewer.Tests;

/// <summary>
///     A host that clamps scroll to its extent the way a real scroll viewer does, and can fire a
///     scroll notification when a shrink of the extent pulls the offset back in range - which is
///     what Avalonia and MAUI do when a zoom-out shortens the document under the last page.
/// </summary>
public sealed class ClampingFakeHost : IReportViewHost
{
    private double _scrollX;
    private double _scrollY;

    public double ViewportWidth { get; set; } = 800;

    public double ViewportHeight { get; set; } = 1000;

    public double ScrollX => _scrollX;

    public double ScrollY => _scrollY;

    public double Density { get; set; } = 2;

    public ViewPoint Extent { get; private set; }

    public Dictionary<int, ViewRect> Pages { get; } = [];

    public List<ViewPoint> Scrolls { get; } = [];

    /// <summary>Raised when clamping the scroll changes it, as a platform ScrollChanged would.</summary>
    public Action? ScrollClamped { get; set; }

    public void ScrollTo(double x, double y)
    {
        Scrolls.Add(new ViewPoint(x, y));
        SetScroll(x, y, notify: false);
    }

    public void SetExtent(double width, double height)
    {
        Extent = new ViewPoint(width, height);
        SetScroll(_scrollX, _scrollY, notify: true);
    }

    public void PlacePage(int index, ViewRect bounds) => Pages[index] = bounds;

    public void PlaceTile(ViewRect bounds, Tile tile)
    {
    }

    public void RemoveTile(TileKey key)
    {
    }

    public void Post(Action action) => action();

    /// <summary>Sets the reported scroll without going through <see cref="ScrollTo"/>.</summary>
    public void ReportScroll(double x, double y) => SetScroll(x, y, notify: false);

    private void SetScroll(double x, double y, bool notify)
    {
        var maxX = Math.Max(0, Extent.X - ViewportWidth);
        var maxY = Math.Max(0, Extent.Y - ViewportHeight);

        var nextX = Math.Clamp(x, 0, maxX);
        var nextY = Math.Clamp(y, 0, maxY);

        if (Math.Abs(nextX - _scrollX) < 0.001 && Math.Abs(nextY - _scrollY) < 0.001)
            return;

        _scrollX = nextX;
        _scrollY = nextY;

        if (notify)
            ScrollClamped?.Invoke();
    }
}
