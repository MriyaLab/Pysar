using Pysar.Skia;

namespace Pysar.Uno;

/// <summary>
///     The renderer the control measures reports with. A single instance carries the drawers the
///     application registered through <see cref="ApplicationExtensions.UsePysar"/>.
/// </summary>
internal static class ReportViewRenderer
{
    private static SkiaReportRenderer? _instance;

    /// <summary>
    ///     The renderer installed by <see cref="ApplicationExtensions.UsePysar"/>. Throws rather
    ///     than falling back to an unconfigured renderer: without the fonts and asset access that
    ///     installs, a report renders with substitute fonts and blank images instead of reporting
    ///     the mistake.
    /// </summary>
    public static SkiaReportRenderer Instance
    {
        get => _instance ?? throw new InvalidOperationException(
            "Call Application.UsePysar during application startup before using a report view or the renderer.");
        set => _instance = value;
    }
}
