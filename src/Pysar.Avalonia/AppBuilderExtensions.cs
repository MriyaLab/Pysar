using System.Reflection;
using Avalonia;
using Pysar.Skia;

namespace Pysar.Avalonia;

/// <summary>
///     Registers Pysar with the application: reports resolve their assets from the application's
///     Avalonia resources, and <see cref="ReportView"/> renders through a shared
///     <see cref="SkiaReportRenderer"/>.
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    ///     Registers Pysar with the application.
    /// </summary>
    /// <example>
    ///     <code>
    ///     AppBuilder.Configure&lt;App&gt;()
    ///         .UsePlatformDetect()
    ///         .UsePysar();
    ///     </code>
    /// </example>
    /// <remarks>
    ///     The family string in the report has to be the Avalonia <c>FontFamily</c> string
    ///     (<c>fonts:Inter#Inter</c> after <c>WithInterFont</c> or <c>AddFontCollection</c> with the
    ///     key <c>fonts:Inter</c>, or <c>avares://MyApp/Fonts#Ubuntu</c> for an Avalonia resource
    ///     folder), not a file path. A bare name (<c>Ubuntu</c>, <c>Inter</c>) is a system-font
    ///     lookup; if <c>TryGetGlyphTypeface</c> substitutes the default face (Helvetica), Pysar
    ///     treats that as a miss.
    ///
    ///     <c>AddFont</c> still wins, and a library <c>ReportAsset</c> still needs one
    ///     <c>AddFont</c>: the file is not copied into the head, and <c>FontManager</c> does not see
    ///     it. A XAML <c>FontFamily</c> resource is not a registration.
    ///
    ///     Styles stay <c>Normal</c>, <c>Bold</c>, <c>Italic</c> and <c>BoldItalic</c>. SemiBold,
    ///     Light, stretch and per-character fallbacks are not shared, and Bold against a
    ///     Regular-only file is not a real bold - the bytes that come back may be the regular face,
    ///     because Avalonia's simulations are not kept on the <c>SKTypeface</c>.
    ///
    ///     This bridge is Avalonia only. Console, the design-time preview, MAUI, WPF and Uno do not
    ///     have <c>FontManager</c> and keep <c>AddFont</c>.
    /// </remarks>
    /// <param name="assemblyName">
    ///     The assembly report assets are packaged under, used to resolve <c>avares://</c> URIs.
    ///     Defaults to the entry assembly's name, which is correct for the common case of a single
    ///     application project holding its own assets; pass an explicit name when assets live in a
    ///     different assembly (a shared resources project, for instance).
    /// </param>
    public static AppBuilder UsePysar(
        this AppBuilder builder, Action<PysarBuilder>? configure = null, string? assemblyName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        assemblyName ??= Assembly.GetEntryAssembly()?.GetName().Name
            ?? throw new InvalidOperationException(
                "Could not determine the entry assembly's name; pass assemblyName explicitly.");

        var platformHandler = new DefaultReportPlatformHandler(
            new FallbackFileSystem(new AvaloniaAssetFileSystem(assemblyName), new EmbeddedAssetFileSystem()));

        var installation = PysarInstallation.Create(platformHandler);

        // An export service pins the renderer it was created with, so one cached across a later
        // registration would keep exporting through the previous renderer and silently miss the
        // drawers and fonts that registration added.
        PysarAvalonia.ResetExportService();

        // The control measures reports with the same renderer, so custom drawers reach the viewer.
        ReportViewRenderer.Instance = installation.Renderer;

        // configure typically reads font bytes through AvaloniaAssetFileSystem, which needs
        // Avalonia.Platform.IAssetLoader - a platform service that UsePlatformDetect() has only
        // scheduled, not yet registered, while this method is still running as part of the
        // AppBuilder's fluent chain. Deferring to AfterPlatformServicesSetup runs it once that
        // service (and the rest of the platform) is actually in the locator.
        builder.AfterPlatformServicesSetup(_ =>
        {
            // Installed before configure so a later AddFont still wins: FontCache drops a host
            // face when the collection grows. Lookup itself waits until the first glyph.
            if (platformHandler.FontCollection is SkiaFontCollection fonts)
                fonts.SetTypefaceFallback(AvaloniaTypefaceFallback.TryResolve);

            installation.Configure(configure);
        });

        return builder;
    }
}
