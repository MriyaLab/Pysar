using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class EdgeBleedTests
{
    private static LayoutNode Node(float left, float right) =>
        new(new Frame(), new Rect(left, 0, right, 100), LayoutNode.NoChildren, LayoutNode.NoCuts);

    [Fact]
    public void ApplyEdgeBleed_BandReachingBothPageEdges_ExtendsOutward()
    {
        // contentLeft = 30, pageWidth = 595.5 → the band spans page x[0 .. 595.5] (both edges).
        var node = Node(-30, 565.5f);
        var bled = PageRenderer.ApplyEdgeBleed(node, contentLeft: 30f, pageWidth: 595.5f, bleed: 1f);

        Assert.Equal(-31f, bled.Bounds.Left);
        Assert.Equal(566.5f, bled.Bounds.Right);
    }

    [Fact]
    public void ApplyEdgeBleed_BandWithinContentZone_Unchanged()
    {
        // Page x[30 .. 565.5] — inside the content zone, does not touch either page edge.
        var node = Node(0, 535.5f);
        var bled = PageRenderer.ApplyEdgeBleed(node, contentLeft: 30f, pageWidth: 595.5f, bleed: 1f);

        Assert.Same(node, bled);
    }

    [Fact]
    public void ApplyEdgeBleed_BandReachingOnlyRightEdge_ExtendsRightOnly()
    {
        // Page x[30 .. 595.5]: touches the right edge only.
        var node = Node(0, 565.5f);
        var bled = PageRenderer.ApplyEdgeBleed(node, contentLeft: 30f, pageWidth: 595.5f, bleed: 1f);

        Assert.Equal(0f, bled.Bounds.Left);       // left untouched
        Assert.Equal(566.5f, bled.Bounds.Right);  // right extended
    }

    [Fact]
    public void ApplyEdgeBleed_NestedChildReachingPageEdges_ExtendsChildNotOnlyRoot()
    {
        // Band stays inside the content zone; a nested full-bleed child reaches the page edges.
        var child = new LayoutNode(
            new Frame(), new Rect(-50, 0, 545.5f, 100), LayoutNode.NoChildren, LayoutNode.NoCuts);
        var band = new LayoutNode(
            new Frame(), new Rect(0, 0, 495.5f, 100), [child], LayoutNode.NoCuts);

        var bled = PageRenderer.ApplyEdgeBleed(band, contentLeft: 50f, pageWidth: 595.5f, bleed: 1f);

        Assert.Equal(0f, bled.Bounds.Left);           // band itself does not touch edges
        Assert.Equal(495.5f, bled.Bounds.Right);
        Assert.Equal(-51f, bled.Children[0].Bounds.Left);
        Assert.Equal(546.5f, bled.Children[0].Bounds.Right);
    }
}
