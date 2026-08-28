using Pysar.Viewer;
using Pysar.Viewer.Geometry;
using Pysar.Viewer.Tiles;

namespace Pysar.Viewer.Tests;

/// <summary>A host that records what it was asked to do, so the presenter can be driven headlessly.</summary>
public sealed class FakeHost : IReportViewHost
{
    public double ViewportWidth { get; set; } = 800;

    public double ViewportHeight { get; set; } = 1000;

    public double ScrollX { get; set; }

    public double ScrollY { get; set; }

    public double Density { get; set; } = 2;

    public List<ViewPoint> Scrolls { get; } = [];

    public Dictionary<int, ViewRect> Pages { get; } = [];

    public ViewPoint Extent { get; private set; }

    public void ScrollTo(double x, double y)
    {
        Scrolls.Add(new ViewPoint(x, y));

        if (DeferScroll)
        {
            DeferredScroll = new ViewPoint(x, y);
            return;
        }

        ScrollX = x;
        ScrollY = y;
    }

    public void SetExtent(double width, double height) => Extent = new ViewPoint(width, height);

    public void PlacePage(int index, ViewRect bounds) => Pages[index] = bounds;

    public Dictionary<TileKey, (ViewRect Bounds, Tile Tile)> Tiles { get; } = [];

    public List<TileKey> Removed { get; } = [];

    public void PlaceTile(ViewRect bounds, Tile tile) => Tiles[tile.Key] = (bounds, tile);

    public void RemoveTile(TileKey key)
    {
        Removed.Add(key);
        Tiles.Remove(key);
    }

    /// <summary>
    ///     Holds posted work instead of running it. Kept for hosts that still queue UI work; scroll
    ///     itself is no longer posted by the presenter.
    /// </summary>
    public bool DeferPosts { get; set; }

    /// <summary>
    ///     Records <see cref="ScrollTo"/> without moving, so a test can stand where MAUI does: told to
    ///     scroll, not yet scrolled, because <c>ScrollToAsync</c> completes on a later turn.
    /// </summary>
    public bool DeferScroll { get; set; }

    public ViewPoint? DeferredScroll { get; private set; }

    public List<Action> Posted { get; } = [];

    public void Post(Action action)
    {
        if (DeferPosts)
        {
            Posted.Add(action);
            return;
        }

        action();
    }

    /// <summary>Runs what was posted while <see cref="DeferPosts"/> was set, in the order it arrived.</summary>
    public void RunPosted()
    {
        var posted = Posted.ToList();
        Posted.Clear();

        foreach (var action in posted)
            action();
    }

    /// <summary>Applies a scroll recorded while <see cref="DeferScroll"/> was set.</summary>
    public void RunDeferredScroll()
    {
        if (DeferredScroll is not { } scroll)
            return;

        DeferredScroll = null;
        ScrollX = scroll.X;
        ScrollY = scroll.Y;
    }
}
