using Pysar.Core.Enums;
using Pysar.Elements;
using Xunit;

namespace Pysar.Export.Tests;

public class PrintPaperTests
{
    [Fact]
    public void From_A4Portrait_UsesPortraitPageSize()
    {
        var paper = PrintPaper.From(new PageFormat
        {
            Size = PageSize.A4,
            Orientation = Orientation.Portrait
        });

        Assert.Equal(595.5f, paper.WidthPt);
        Assert.Equal(842f, paper.HeightPt);
        Assert.False(paper.IsLandscape);
        Assert.Equal("iso-a4", paper.PaperName);
    }

    [Fact]
    public void From_A4Landscape_UsesLandscapePageSize()
    {
        var paper = PrintPaper.From(new PageFormat
        {
            Size = PageSize.A4,
            Orientation = Orientation.Landscape
        });

        Assert.Equal(842f, paper.WidthPt);
        Assert.Equal(595.5f, paper.HeightPt);
        Assert.True(paper.IsLandscape);
        Assert.Equal("iso-a4", paper.PaperName);
    }

    [Fact]
    public void From_A4Portrait_ExposesPortraitMilsForAndroidMediaSize()
    {
        var paper = PrintPaper.From(new PageFormat
        {
            Size = PageSize.A4,
            Orientation = Orientation.Portrait
        });

        Assert.Equal(8271, paper.PortraitWidthMils);
        Assert.Equal(11694, paper.PortraitHeightMils);
        Assert.True(paper.PortraitWidthMils < paper.PortraitHeightMils);
    }

    [Fact]
    public void From_A4Landscape_KeepsPortraitMilsAndMarksLandscape()
    {
        var paper = PrintPaper.From(new PageFormat
        {
            Size = PageSize.A4,
            Orientation = Orientation.Landscape
        });

        Assert.Equal(8271, paper.PortraitWidthMils);
        Assert.Equal(11694, paper.PortraitHeightMils);
        Assert.True(paper.IsLandscape);
    }

    [Fact]
    public void From_RejectsAMissingPageFormat()
    {
        Assert.Throws<ArgumentNullException>(() => PrintPaper.From(null!));
    }
}
