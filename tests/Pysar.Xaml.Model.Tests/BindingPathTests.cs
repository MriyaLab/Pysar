using Pysar.Xaml.Model.Tooling;
using Xunit;

namespace Pysar.Xaml.Model.Tests;

public class BindingPathTests
{
    [Fact]
    public void Parse_Empty_HasNoCompletedSegmentsAndEmptyPartial()
    {
        var path = BindingPath.Parse("");

        Assert.Empty(path.CompletedSegments);
        Assert.Equal("", path.PartialSegment);
    }

    [Fact]
    public void Parse_SingleToken_IsPartialWithNoCompleted()
    {
        var path = BindingPath.Parse("Cus");

        Assert.Empty(path.CompletedSegments);
        Assert.Equal("Cus", path.PartialSegment);
    }

    [Fact]
    public void Parse_TrailingDot_CompletesLeadingSegmentAndEmptyPartial()
    {
        var path = BindingPath.Parse("Customer.");

        Assert.Equal(new[] { "Customer" }, path.CompletedSegments);
        Assert.Equal("", path.PartialSegment);
    }

    [Fact]
    public void Parse_NestedPartial_CompletesLeadingSegments()
    {
        var path = BindingPath.Parse("Customer.Address.Ci");

        Assert.Equal(new[] { "Customer", "Address" }, path.CompletedSegments);
        Assert.Equal("Ci", path.PartialSegment);
    }
}
