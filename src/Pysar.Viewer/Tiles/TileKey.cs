using Pysar.Viewer.Geometry;

namespace Pysar.Viewer.Tiles;

/// <summary>Identifies one cell of the grid a page is cut into at a given scale.</summary>
/// <remarks>
///     The grid is anchored to the page, not to the viewport, so a cell keeps its identity while the
///     view scrolls - which is what lets an already drawn cell be reused instead of redrawn.
/// </remarks>
public readonly record struct TileKey(int PageIndex, int Column, int Row, float Scale);

/// <summary>One rasterised cell: the part of the page it covers, its pixels, and their size.</summary>
/// <remarks>
///     Bytes rather than a live bitmap: these become platform image sources, which the platform
///     decodes and then scrolls by itself, and nothing downstream has to manage a native lifetime.
///     <para>
///     The payload is whatever the host asked <see cref="ReportViewTiles"/> to produce - PNG bytes
///     for a platform that wants an image source, raw pixels for one that writes into a canvas. The
///     pixel size travels with it because only the renderer knows how its rounding came out, and a
///     host that recomputed it would drift the moment that rounding changed.
///     </para>
/// </remarks>
public sealed record Tile(TileKey Key, RectPt RegionPt, byte[] Bytes, int PixelWidth, int PixelHeight);

/// <summary>A cell the view wants drawn, and the part of the page it covers.</summary>
public readonly record struct TileRequest(TileKey Key, RectPt RegionPt);
