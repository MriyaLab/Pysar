using Pysar.Core.Enums;
using Xunit;

namespace Pysar.Elements.Tests;

public class PageBreakTests
{
    [Fact]
    public void PageBreak_HasZeroHeight()
    {
        var pb = new PageBreak();
        Assert.True(pb.Size.Height.IsFixed);
        Assert.Equal(0f, pb.Size.Height.Value);
    }

    [Fact]
    public void PageBreak_FillsWidth()
    {
        var pb = new PageBreak();
        Assert.True(pb.Size.Width.IsFill);
    }

    [Fact]
    public void AddPageBreak_OnBand_SetsBandLevelAfter_WithoutAddingMarker()
    {
        var band = new ReportHeaderBand();
        var childrenBefore = band.Children.Count;

        band.AddPageBreak();

        Assert.Equal(PageBreakMode.After, band.PageBreak);
        Assert.Equal(childrenBefore, band.Children.Count);   // no PageBreak element added to the band
    }

    [Fact]
    public void AddPageBreak_OnBand_ThroughDegradedFrameType_StillSetsBandLevel()
    {
        // A fluent chain degrades to the Frame base type; the runtime check must still catch the band.
        Frame band = new ReportHeaderBand();
        band.AddPageBreak();
        Assert.Equal(PageBreakMode.After, ((ReportHeaderBand)band).PageBreak);
    }

    [Fact]
    public void AddPageBreak_OnVerticalContainer_AddsMarkerElement()
    {
        var stack = new StackPanel();
        stack.AddPageBreak();
        Assert.IsType<PageBreak>(Assert.Single(stack.Children));
    }
}
