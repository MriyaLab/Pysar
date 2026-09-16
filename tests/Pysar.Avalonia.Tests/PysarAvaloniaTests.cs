using Pysar.Avalonia;
using Xunit;

namespace Pysar.Avalonia.Tests;

/// <summary>
///     The public renderer/export accessor a host with no service collection reaches Pysar through;
///     the headless app's <c>UsePysar</c> runs before any test, so the renderer is installed.
/// </summary>
[Collection(HeadlessCollection.Name)]
public class PysarAvaloniaTests
{
    [Fact]
    public void Renderer_IsInstalledByUsePysar()
    {
        var renderer = PysarAvalonia.Renderer;

        Assert.NotNull(renderer);
    }

    [Fact]
    public void ExportService_IsCachedAcrossReads()
    {
        var first = PysarAvalonia.ExportService;
        var second = PysarAvalonia.ExportService;

        Assert.Same(first, second);
    }
}