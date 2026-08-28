using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class PositionResolverTests
{
    [Fact]
    public void Resolve_ExplicitPosition_OffsetsFromAvailableRect()
    {
        var el = new Frame().At(30, 40);
        var (left, top) = PositionResolver.Resolve(el, 50, 50, new Rect(100, 200, 500, 600));
        Assert.Equal(130, left);
        Assert.Equal(240, top);
    }

    [Fact]
    public void Resolve_CenterEnd_AlignsWithinRect()
    {
        var el = new Frame { HorizontalAlignment = Alignment.Center, VerticalAlignment = Alignment.End };
        var (left, top) = PositionResolver.Resolve(el, 100, 100, new Rect(0, 0, 400, 400));
        Assert.Equal(150, left);
        Assert.Equal(300, top);
    }

    [Fact]
    public void Resolve_Default_IsStart()
    {
        var el = new Frame();
        var (left, top) = PositionResolver.Resolve(el, 10, 10, new Rect(50, 60, 500, 600));
        Assert.Equal(50, left);
        Assert.Equal(60, top);
    }
}
