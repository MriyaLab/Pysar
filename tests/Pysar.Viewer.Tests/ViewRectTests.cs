using Pysar.Viewer.Geometry;
using Xunit;

namespace Pysar.Viewer.Tests;

public class ViewRectTests
{
    [Fact]
    public void Inflate_GrowsOnEverySide()
    {
        var rect = new ViewRect(10, 20, 100, 200).Inflate(5);

        Assert.Equal(5, rect.X, 3);
        Assert.Equal(15, rect.Y, 3);
        Assert.Equal(110, rect.Width, 3);
        Assert.Equal(210, rect.Height, 3);
    }
}
