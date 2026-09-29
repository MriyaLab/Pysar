using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Skia;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests;

[CollectionDefinition(nameof(PysarInstallationTests))]
public sealed class PysarInstallationCollection;

[Collection(nameof(PysarInstallationTests))]
public sealed class PysarInstallationTests
{
    private sealed class Badge : ReportContainer<Badge>;

    private sealed class RecordingDrawer : IElementDrawer
    {
        public bool Drew { get; private set; }

        public void Draw(LayoutNode node, RenderContext ctx) => Drew = true;
    }

    private sealed class NamedFileSystem(string marker) : IFileSystem
    {
        public string Marker { get; } = marker;

        public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult<byte[]?>(null);

        public bool Exists(string? filePath) => false;
    }

    [Fact]
    public void Create_RejectsANullHandler()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => PysarInstallation.Create(null!));

        Assert.Equal("platformHandler", exception.ParamName);
    }

    [Fact]
    public void Create_InstallsTheHandlerBeforeReturning()
    {
        var fileSystem = new NamedFileSystem("installed");
        var handler = new DefaultReportPlatformHandler(fileSystem);

        var installation = PysarInstallation.Create(handler);

        Assert.Same(handler, installation.PlatformHandler);
        Assert.Same(fileSystem, ReportPlatformHandler.FileSystem);
        Assert.Same(handler.FontCollection, ReportPlatformHandler.FontCollection);
        Assert.NotNull(installation.Renderer);
    }

    [Fact]
    public void Create_ReplacesThePreviousAmbientHandler()
    {
        PysarInstallation.Create(new DefaultReportPlatformHandler(new NamedFileSystem("first")));

        var second = new NamedFileSystem("second");
        PysarInstallation.Create(new DefaultReportPlatformHandler(second));

        var installed = Assert.IsType<NamedFileSystem>(ReportPlatformHandler.FileSystem);
        Assert.Equal("second", installed.Marker);
    }

    [Fact]
    public void Configure_NullCallback_DoesNotThrow()
    {
        var installation = PysarInstallation.Create(
            new DefaultReportPlatformHandler(new NamedFileSystem("idle")));

        var exception = Record.Exception(() => installation.Configure(null));

        Assert.Null(exception);
    }

    [Fact]
    public void Configure_PassesTheHandlerFontCollection()
    {
        var handler = new DefaultReportPlatformHandler(new NamedFileSystem("fonts"));
        var installation = PysarInstallation.Create(handler);
        IFontCollection? seen = null;

        installation.Configure(pysar => seen = pysar.Fonts);

        Assert.Same(handler.FontCollection, seen);
    }

    [Fact]
    public async Task Configure_RegistersDrawersOnTheReturnedRenderer()
    {
        var drawer = new RecordingDrawer();
        var installation = PysarInstallation.Create(
            new DefaultReportPlatformHandler(new NamedFileSystem("drawers")));

        installation.Configure(pysar => pysar.AddDrawer<Badge>(drawer));

        var design = ReportBuilder.Create("installation-drawer")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(band => band.AddElement(
                new Badge { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)) }))
            .Build();

        await installation.Renderer.RenderPageAsync(design);

        Assert.True(drawer.Drew);
    }
}
