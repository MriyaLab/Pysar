using Xunit;

namespace Pysar.Elements.Tests;

public class ReportContainerTests
{
    [Fact]
    public void RemoveElement_RemovesTheChild()
    {
        var frame = new Frame();
        var child = new Text { Content = "x" };
        frame.AddElement(child);

        frame.RemoveElement(child);

        Assert.Empty(frame.Children);
    }

    [Fact]
    public void RemoveElement_UnknownChild_DoesNotAddIt()
    {
        var frame = new Frame();
        frame.RemoveElement(new Text { Content = "not a child" });

        Assert.Empty(frame.Children);
    }
}
