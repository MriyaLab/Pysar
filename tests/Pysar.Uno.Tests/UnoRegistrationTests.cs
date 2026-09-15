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
///     Exercised through the internal seam rather than through <c>Application.UsePysar</c> - see
///     <see cref="UnoRegistration"/>'s remarks for why the public surface cannot be called with a
///     real <c>Application</c> here. What is reachable without one - the null guard and the
///     <c>suppressBrowserZoom</c> default recorded in the extension's metadata - is covered directly
///     against <c>ApplicationExtensions</c> below.
///
///     One collection, not parallel facts: <c>ReportPlatformHandler</c> and the renderer are process
///     wide, so two of these running at once would each see the other's installation.
///
///     Not covered: <c>PysarUno.PlatformHandler</c> throwing before any registration. <c>Current</c>
///     is never cleared, and every fact in this collection installs a handler first, so nothing here
///     observes the unregistered state - the same reason <c>ReportViewRenderer.Instance</c>'s throw
///     is untested.
/// </remarks>
[Collection(nameof(UnoRegistrationTests))]
public class UnoRegistrationTests
{
    private static Assembly Assets => Assembly.GetExecutingAssembly();

    /// <summary>A custom element outside the built-in set, to register a drawer for.</summary>
    private sealed class Badge : ReportContainer<Badge> { }

    private sealed class RecordingDrawer : IElementDrawer
    {
        public bool Drew { get; private set; }

        public void Draw(LayoutNode node, RenderContext ctx) => Drew = true;
    }

    /// <summary>
    ///     Covers <c>suppressBrowserZoom</c> both ways because the flag is otherwise irrelevant to
    ///     registration: under the reference assembly <c>OperatingSystem.IsBrowser()</c> is false, so
    ///     <c>BrowserWheelZoom.EnsureInstalledAsync()</c> returns <see cref="Task.CompletedTask"/> and
    ///     the branch it guards is inert. This proves asking for suppression off the browser installs
    ///     the handler and runs the configuration callback exactly as asking without it does - not
    ///     that the script actually imports, which only a browser head can show.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Install_InstallsAndConfigures_RegardlessOfSuppressBrowserZoom(bool suppressBrowserZoom)
    {
        var configured = false;

        var handler = UnoRegistration.Install(
            Assets,
            pysar =>
            {
                pysar.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");
                configured = true;
            },
            suppressBrowserZoom);

        Assert.Same(handler.FileSystem, ReportPlatformHandler.FileSystem);
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

        UnoRegistration.Install(
            Assets, pysar => pysar.AddDrawer<Badge>(drawer), suppressBrowserZoom: false);

        var design = ReportBuilder.Create("drawer-reaches-the-shared-renderer")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(band => band.AddElement(
                new Badge { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)) }))
            .Build();

        await PysarUno.Renderer.RenderPageAsync(design);

        Assert.True(drawer.Drew);
    }

    /// <summary>
    ///     What <c>Install</c> used to return to the application, now reachable the way
    ///     <c>Renderer</c> and <c>ExportService</c> are.
    /// </summary>
    [Fact]
    public void PlatformHandler_IsTheHandlerRegistrationInstalled()
    {
        var handler = UnoRegistration.Install(Assets, configure: null, suppressBrowserZoom: false);

        Assert.Same(handler, PysarUno.PlatformHandler);
    }

    /// <summary>
    ///     The property builds its service the first time it is read and holds it afterwards, so an
    ///     application that keeps a reference to it holds the same object every other reader gets.
    /// </summary>
    [Fact]
    public void ExportService_IsCreatedOnceAndHeld()
    {
        UnoRegistration.Install(Assets, configure: null, suppressBrowserZoom: false);

        Assert.NotNull(PysarUno.ExportService);
        Assert.Same(PysarUno.ExportService, PysarUno.ExportService);
    }

    [Fact]
    public void ExportService_IsRebuiltWhenRegistrationInstallsANewRenderer()
    {
        UnoRegistration.Install(Assets, configure: null, suppressBrowserZoom: false);

        // Touched before the second registration, so the stale one would be cached by now: an
        // export service pins the renderer it was built over, and keeping it would go on exporting
        // through a renderer that never saw the second call's drawers and fonts.
        var beforeReregistration = PysarUno.ExportService;

        UnoRegistration.Install(Assets, configure: null, suppressBrowserZoom: false);

        Assert.NotSame(beforeReregistration, PysarUno.ExportService);
    }

    /// <summary>
    ///     The preloaded path is deliberately not one of the packaged fonts: every ms-appx fetch
    ///     fails under the reference assembly, and unlike <c>Fonts/Ubuntu-Regular.ttf</c> this path
    ///     has no embedded resource to fall back to either, so <c>Exists</c> is false whether or not
    ///     the preload attempt ran - the value itself proves nothing about that ordering. What it does
    ///     pin: <c>Assert.False</c> fails on a null <c>bool?</c>, so a recorded value at all means the
    ///     callback ran; reading the ambient file system from inside it does not throw; and, the part
    ///     that matters, a preload that cannot resolve its path lets registration complete rather than
    ///     aborting it - the real behaviour this fact guards.
    /// </summary>
    [Fact]
    public async Task InstallAsync_InstallsTheHandlerAndRunsTheCallback()
    {
        const string unpackagedPath = "Fonts/Not-A-Packaged-Font.ttf";
        var recordedExists = (bool?)null;

        var handler = await UnoRegistration.InstallAsync(
            Assets,
            [unpackagedPath],
            _ => recordedExists = ReportPlatformHandler.FileSystem.Exists(unpackagedPath),
            suppressBrowserZoom: false);

        Assert.Same(handler.FileSystem, ReportPlatformHandler.FileSystem);
        Assert.False(recordedExists);
    }

    /// <summary>
    ///     Covers <c>suppressBrowserZoom</c> both ways for <see cref="UnoRegistration.InstallAsync"/>,
    ///     the async counterpart to <see cref="Install_InstallsAndConfigures_RegardlessOfSuppressBrowserZoom"/>.
    ///     Under the reference assembly <c>OperatingSystem.IsBrowser()</c> is false, so
    ///     <c>BrowserWheelZoom.EnsureInstalledAsync()</c> returns <see cref="Task.CompletedTask"/> and
    ///     the branch it guards is inert - this proves asking for suppression off the browser installs
    ///     the handler and runs the configuration callback exactly as asking without it does, not that
    ///     the script actually imports, which only a browser head can show.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InstallAsync_InstallsAndConfigures_RegardlessOfSuppressBrowserZoom(bool suppressBrowserZoom)
    {
        var configured = false;

        var handler = await UnoRegistration.InstallAsync(
            Assets,
            [],
            pysar =>
            {
                pysar.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");
                configured = true;
            },
            suppressBrowserZoom);

        Assert.Same(handler.FileSystem, ReportPlatformHandler.FileSystem);
        Assert.True(configured);
    }

    [Fact]
    public void Install_RejectsANullAssembly()
        => Assert.Throws<ArgumentNullException>(
            () => UnoRegistration.Install(null!, configure: null, suppressBrowserZoom: false));

    [Fact]
    public async Task InstallAsync_RejectsNullPreloadPaths()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            () => UnoRegistration.InstallAsync(
                Assets, null!, configure: null, suppressBrowserZoom: false));

    /// <summary>
    ///     Off the browser <see cref="BrowserWheelZoom.EnsureInstalledAsync"/> never touches the
    ///     cached import task - it returns <see cref="Task.CompletedTask"/> before reaching it - so
    ///     every call sees the same already-completed task. That is the contract
    ///     <see cref="UnoRegistration.Install"/>'s discarded call and <see cref="UnoRegistration.InstallAsync"/>'s
    ///     unguarded <c>await</c> both lean on without a null check or a try/catch of their own;
    ///     verified here directly against the type that owns it rather than only through those two
    ///     registration-level proxies. Says nothing about the browser path, where the cached task is
    ///     the real import instead.
    /// </summary>
    [Fact]
    public void EnsureInstalledAsync_OffTheBrowser_IsAlreadyCompletedAndRepeatable()
    {
        var first = BrowserWheelZoom.EnsureInstalledAsync();
        var second = BrowserWheelZoom.EnsureInstalledAsync();

        Assert.True(first.IsCompletedSuccessfully);
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Flipping <c>suppressBrowserZoom</c>'s default back to <c>false</c> is a deliberate decision
    ///     for this task, and it lives only here: <see cref="UnoRegistration.Install"/> and
    ///     <see cref="UnoRegistration.InstallAsync"/> take no default at all, so a silent flip on the
    ///     public extensions would compile, run and pass every other fact in this file without
    ///     anything catching it. Read through reflection because there is no other way to observe a
    ///     parameter default without constructing an <c>Application</c> to call the method.
    /// </summary>
    [Fact]
    public void UsePysarMethods_DefaultSuppressBrowserZoomToTrue()
    {
        var useSync = typeof(ApplicationExtensions).GetMethod(nameof(ApplicationExtensions.UsePysar));
        var useAsync = typeof(ApplicationExtensions).GetMethod(nameof(ApplicationExtensions.UsePysarAsync));

        Assert.Equal(true, useSync!.GetParameters()
            .Single(p => p.Name == "suppressBrowserZoom").DefaultValue);
        Assert.Equal(true, useAsync!.GetParameters()
            .Single(p => p.Name == "suppressBrowserZoom").DefaultValue);
    }

    /// <summary>
    ///     The one piece of <see cref="ApplicationExtensions.UsePysar"/> reachable without a real
    ///     <c>Application</c>: <c>ArgumentNullException.ThrowIfNull</c> runs before anything that
    ///     needs a live host, so calling it with a null application exercises the guard on its own.
    /// </summary>
    [Fact]
    public void UsePysar_RejectsANullApplication()
        => Assert.Throws<ArgumentNullException>(() => ApplicationExtensions.UsePysar(null!, Assets));

    /// <summary>
    ///     The compensating check for the one failure mode <see cref="BrowserWheelZoom"/>'s import
    ///     deliberately swallows and traces Debug-only: a mismatch between this name and the embedded
    ///     resource's path would leave a Release package that never suppresses browser zoom, with
    ///     nothing surfacing the mistake, because the failed import is cached exactly as permanently
    ///     as a successful one. Runs off the browser - a manifest resource lookup needs no interop.
    /// </summary>
    [Fact]
    public void ZoomScript_IsEmbeddedUnderTheNameBrowserWheelZoomImports()
    {
        using var stream = typeof(UnoReportPlatformHandler).Assembly
            .GetManifestResourceStream("Pysar.Uno.Scripts.reportViewZoom.js");

        Assert.NotNull(stream);
    }
}
