using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class MetadataTests
{
    [Fact]
    public void Build_PreservesTitle()
    {
        var design = ReportBuilder.Create("Sales Report").Build();
        Assert.Equal("Sales Report", design.Metadata.Title);
    }

    [Fact]
    public void Build_PreservesAuthor()
    {
        var design = ReportBuilder.Create("t")
            .WithAuthor("Jane Doe")
            .Build();
        Assert.Equal("Jane Doe", design.Metadata.Author);
    }

    [Fact]
    public void Build_WithoutAuthor_AuthorIsEmpty()
    {
        var design = ReportBuilder.Create("t").Build();
        Assert.Equal(string.Empty, design.Metadata.Author);
    }

    [Fact]
    public void Report_HasMetadataByDefault()
    {
        var design = new Report();
        Assert.NotNull(design.Metadata);
        Assert.Equal(string.Empty, design.Metadata.Title);
    }

    [Fact]
    public void Report_BackgroundColor_DefaultsToWhite()
    {
        Assert.Equal(Colors.White, new Report().BackgroundColor);
    }
}
