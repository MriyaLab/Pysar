using System.Reflection;
using Pysar.Core;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Uno.Tests;

/// <summary>
///     Registration installs the ambient state a report and a report view read at render time.
/// </summary>
/// <remarks>
///     One collection, not parallel facts: <c>ReportPlatformHandler</c> and the renderer are process
///     wide, so two of these running at once would each see the other's installation.
/// </remarks>
[Collection(nameof(PysarUnoTests))]
public class PysarUnoTests
{
    private static Assembly Assets => Assembly.GetExecutingAssembly();

    /// <summary>A custom element outside the built-in set, to register a drawer for.</summary>
    private sealed class Badge : ReportContainer<Badge> { }

    private sealed class RecordingDrawer : IElementDrawer
    {
        public bool Drew { get; private set; }

        public void Draw(LayoutNode node, RenderContext ctx) => Drew = true;
    }

    [Fact]
    public void Use_InstallsTheAmbientPlatformHandler()
    {
        var handler = PysarUno.Use(Assets);

        Assert.Same(handler.FileSystem, ReportPlatformHandler.FileSystem);
    }

    [Fact]
    public void Use_RunsTheConfigurationCallbackAgainstThePackagedAssets()
    {
        var configured = false;

        PysarUno.Use(Assets, pysar =>
        {
            pysar.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");
            configured = true;
        });

        Assert.True(configured);
    }

    /// <summary>
    ///     The contract the property exists for: what an application configures through the builder is
    ///     what the report view and an outside caller render with. Asserted by rendering rather than by
    ///     comparing references, because the renderer keeps its drawers private - and rendering is what
    ///     would break if the two ever drifted apart.
    /// </summary>
    [Fact]
    public async Task Renderer_CarriesTheDrawersTheConfigurationCallbackRegistered()
    {
        var drawer = new RecordingDrawer();

        PysarUno.Use(Assets, pysar => pysar.AddDrawer<Badge>(drawer));

        var design = ReportBuilder.Create("drawer-reaches-the-shared-renderer")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(band => band.AddElement(
                new Badge { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)) }))
            .Build();

        await PysarUno.Renderer.RenderPageAsync(design);

        Assert.True(drawer.Drew);
    }

    /// <summary>
    ///     The property builds its service the first time it is read and holds it afterwards, so an
    ///     application that keeps a reference to it holds the same object every other reader gets.
    /// </summary>
    [Fact]
    public void ExportService_IsCreatedOnceAndHeld()
    {
        PysarUno.Use(Assets);

        Assert.NotNull(PysarUno.ExportService);
        Assert.Same(PysarUno.ExportService, PysarUno.ExportService);
    }

    [Fact]
    public void ExportService_IsRebuiltWhenUseInstallsANewRenderer()
    {
        PysarUno.Use(Assets);

        // Touched before the second registration, so the stale one would be cached by now: an
        // export service pins the renderer it was built over, and keeping it would go on exporting
        // through a renderer that never saw the second call's drawers and fonts.
        var beforeReregistration = PysarUno.ExportService;

        PysarUno.Use(Assets);

        Assert.NotSame(beforeReregistration, PysarUno.ExportService);
    }

    [Fact]
    public void Use_RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => PysarUno.Use(null!));
}
