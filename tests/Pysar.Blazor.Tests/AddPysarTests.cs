using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Elements;
using Pysar.Export;
using Pysar.Skia;
using Xunit;

namespace Pysar.Blazor.Tests;

/// <summary>
///     The registration a Blazor host performs once at startup, which the browser samples used to be
///     the only proof of.
/// </summary>
public class AddPysarTests
{
    [Fact]
    public void TheViewerAndTheExportersShareOneRenderer()
    {
        // Not an implementation detail: a drawer registered here has to reach the on-screen viewer
        // as well as an export, which only holds while both resolve the same renderer.
        using var provider = BuildProvider();

        var renderer = provider.GetRequiredService<SkiaReportRenderer>();

        Assert.Same(renderer, provider.GetRequiredService<SkiaReportRenderer>());
        Assert.NotNull(provider.GetRequiredService<IReportExportService>());
    }

    [Fact]
    public void TheConfigureCallbackRunsAgainstTheRendererThatIsRegistered()
    {
        SkiaReportRenderer? configured = null;

        using var provider = BuildProvider(renderer => configured = renderer);

        Assert.Same(provider.GetRequiredService<SkiaReportRenderer>(), configured);
    }

    [Fact]
    public async Task ThePrinterIsScoped_BecauseItHoldsOneBrowserContextsJsModule()
    {
        using var provider = BuildProvider();

        // AsyncScope, and awaited: the printer is IAsyncDisposable, and a scope holding one refuses
        // to be torn down synchronously - which is the container agreeing about its lifetime.
        await using var scope = provider.CreateAsyncScope();
        await using var other = provider.CreateAsyncScope();

        var printer = scope.ServiceProvider.GetRequiredService<IReportPrinter>();

        Assert.Same(printer, scope.ServiceProvider.GetRequiredService<IReportPrinter>());
        Assert.NotSame(printer, other.ServiceProvider.GetRequiredService<IReportPrinter>());
    }

    [Fact]
    public void AddPysar_RejectsAMissingServiceCollection()
        => Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddPysar(null!));

    [Fact]
    public void TheWasmHandler_BecomesTheOneAssetsResolveThrough()
    {
        var files = PreloadedFileSystem.From(new Dictionary<string, byte[]>
        {
            ["Images/logo.svg"] = [1, 2, 3]
        });

        var handler = WasmPlatformHandler.Install(files);

        // Install now wraps the preloaded file system in a fallback chain (Images/logo.svg falls
        // back to EmbeddedAssetFileSystem), so the preloaded set is no longer the whole of
        // handler.FileSystem and Assert.Same against it no longer holds. What the test's name
        // claims - that a preloaded asset is actually reachable through the ambient file system,
        // and that the returned handler is the one installed - still has to hold, so assert that
        // directly instead of the object identity that used to stand in for it.
        Assert.True(ReportPlatformHandler.FileSystem.Exists("Images/logo.svg"));
        Assert.Equal([1, 2, 3], ((ISyncFileSystem)ReportPlatformHandler.FileSystem).ReadFile("Images/logo.svg"));
        Assert.Same(handler.FileSystem, ReportPlatformHandler.FileSystem);
        Assert.Same(handler.FontCollection, ReportPlatformHandler.FontCollection);
    }

    [Fact]
    public void TheWasmHandler_RejectsAMissingFileSystem()
        => Assert.Throws<ArgumentNullException>(() => WasmPlatformHandler.Install(null!));

    private static ServiceProvider BuildProvider(Action<SkiaReportRenderer>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IJSRuntime>(new FakeJsRuntime());
        services.AddPysar(configure);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>Stands in for the browser: nothing is invoked in these tests, only resolved.</summary>
    private sealed class FakeJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult<TValue>(default!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult<TValue>(default!);
    }
}
