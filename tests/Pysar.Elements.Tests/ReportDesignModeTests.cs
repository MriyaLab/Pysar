using Pysar.Core;
using Pysar.Core.Abstractions;
using Xunit;

namespace Pysar.Elements.Tests;

public class ReportDesignModeTests
{
    private sealed record Model(string Value) : IDesignTimeCreatable<Model>
    {
        public static Model CreateDesignInstance() => new("design");
    }

    [Fact]
    public void IsEnabled_IsFalseByDefault()
    {
        Assert.False(ReportDesignMode.IsEnabled);
    }

    [Fact]
    public void Enable_TurnsTheFlagOn()
    {
        try
        {
            ReportDesignMode.Enable();
            Assert.True(ReportDesignMode.IsEnabled);
        }
        finally
        {
            ReportDesignMode.Disable();
        }
    }

    [Fact]
    public void DesignTimeCreatable_ExposesTheFactoryThroughTheInterface()
    {
        Assert.Equal("design", CreateThrough<Model>().Value);
    }

    private static T CreateThrough<T>() where T : IDesignTimeCreatable<T> => T.CreateDesignInstance();
}
