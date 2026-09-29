using Pysar.Core;
using Pysar.Core.Abstractions;

namespace Pysar.Skia;

/// <summary>
///     The installation sequence every UI host repeats: publish the platform handler reports
///     resolve assets through, create the renderer the report view draws with, and apply the
///     application's fonts and custom drawers.
/// </summary>
/// <remarks>
///     <see cref="Configure"/> is separate from <see cref="Create"/> because Avalonia cannot read
///     assets until <c>AfterPlatformServicesSetup</c>. Hosts that can configure immediately call
///     both in one method. This type does not own a host's <c>ReportViewRenderer</c> static or its
///     service collection — those stay in the host package.
/// </remarks>
public sealed class PysarInstallation
{
    private PysarInstallation(IReportPlatformHandler platformHandler, SkiaReportRenderer renderer)
    {
        PlatformHandler = platformHandler;
        Renderer = renderer;
    }

    /// <summary>The handler <see cref="Create"/> installed into <see cref="ReportPlatformHandler"/>.</summary>
    public IReportPlatformHandler PlatformHandler { get; }

    /// <summary>The renderer created for this installation. Custom drawers land on this instance.</summary>
    public SkiaReportRenderer Renderer { get; }

    /// <summary>
    ///     Installs <paramref name="platformHandler"/> as the ambient handler and returns a new
    ///     renderer. A later call replaces the ambient handler; it does not update a renderer a
    ///     previous installation already handed out.
    /// </summary>
    public static PysarInstallation Create(IReportPlatformHandler platformHandler)
    {
        ArgumentNullException.ThrowIfNull(platformHandler);

        ReportPlatformHandler.Create(platformHandler);
        return new PysarInstallation(platformHandler, new SkiaReportRenderer());
    }

    /// <summary>
    ///     Applies fonts and custom drawers. A null callback is a host that registered Pysar
    ///     without configuration.
    /// </summary>
    public void Configure(Action<PysarBuilder>? configure)
        => configure?.Invoke(new PysarBuilder(Renderer, PlatformHandler.FontCollection));
}
