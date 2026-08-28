using Pysar.Skia;
using Xunit;

namespace Pysar.Skia.Tests;

public class ReportBootstrapTests
{
    private sealed class SpyBootstrap : IReportBootstrap
    {
        public static SkiaReportRenderer? Received;

        public static void Initialize(SkiaReportRenderer renderer) => Received = renderer;
    }

    [Fact]
    public void Initialize_IsInvokableThroughTheInterface()
    {
        var renderer = new SkiaReportRenderer();

        Configure<SpyBootstrap>(renderer);

        Assert.Same(renderer, SpyBootstrap.Received);
    }

    private static void Configure<T>(SkiaReportRenderer renderer) where T : IReportBootstrap
        => T.Initialize(renderer);
}
