using Xunit;

namespace Pysar.Uno.Tests;

public class RecycledViewPoolTests
{
    [Fact]
    public void Take_WhenEmpty_Creates()
    {
        var pool = new RecycledViewPool<object>();
        var created = new object();

        Assert.Same(created, pool.Take(() => created));
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Take_AfterRelease_ReturnsTheSameInstance()
    {
        var pool = new RecycledViewPool<object>();
        var first = new object();

        pool.Release(first);

        Assert.Same(first, pool.Take(() => new object()));
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void Clear_DropsSparesSoTheNextTakeCreates()
    {
        var pool = new RecycledViewPool<object>();
        pool.Release(new object());
        pool.Clear();

        var created = new object();

        Assert.Same(created, pool.Take(() => created));
    }

    [Fact]
    public void Release_RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => new RecycledViewPool<object>().Release(null!));
}
