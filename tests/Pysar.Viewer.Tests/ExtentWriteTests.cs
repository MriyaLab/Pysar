using Xunit;

namespace Pysar.Viewer.Tests;

public class ExtentWriteTests
{
    /// <summary>
    ///     The one that matters. An unset size is NaN in WPF and Avalonia, and every comparison
    ///     against NaN is false - so a guard written as a difference alone never fires, the canvas
    ///     is never given a size, and the scroll viewer concludes there is nothing to scroll.
    /// </summary>
    [Fact]
    public void AnUnsetSize_HasAlwaysChanged()
    {
        Assert.True(ExtentWrite.Needed(double.NaN, 800));
        Assert.True(ExtentWrite.Needed(double.NaN, 0));
    }

    /// <summary>
    ///     Writing an unchanged value invalidates layout, which raises a size change, which lands
    ///     back here: the window stays blank forever, with no exception to show for it.
    /// </summary>
    [Fact]
    public void TheSameSize_HasNotChanged()
    {
        Assert.False(ExtentWrite.Needed(800, 800));
    }

    /// <summary>Under half a unit is below anything a layout pass can act on.</summary>
    [Theory]
    [InlineData(800, 800.4)]
    [InlineData(800, 799.6)]
    public void AChangeSmallerThanHalfAUnit_IsNotAChange(double current, double wanted)
    {
        Assert.False(ExtentWrite.Needed(current, wanted));
    }

    [Theory]
    [InlineData(800, 800.5)]
    [InlineData(800, 799.5)]
    [InlineData(800, 1200)]
    public void AChangeOfHalfAUnitOrMore_IsAChange(double current, double wanted)
    {
        Assert.True(ExtentWrite.Needed(current, wanted));
    }
}
