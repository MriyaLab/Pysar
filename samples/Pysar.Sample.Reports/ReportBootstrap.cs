using Pysar.Sample.Reports.QRCode;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Skia;

namespace Pysar.Sample.Reports;

/// <summary>Registrations shared by the application entry point and design-time tooling.</summary>
public sealed class ReportBootstrap : IReportBootstrap
{
    public static void Initialize(SkiaReportRenderer renderer)
    {
        ReportPlatformHandler.Create(new FileSystemPlatformHandler());

        RegisterFonts(ReportPlatformHandler.FontCollection);
        RegisterDrawers(renderer);
    }

    /// <summary>
    ///     Registers the fonts the sample reports use. Hosts that install their own platform handler -
    ///     the MAUI sample, which reads assets from the application package - call this directly.
    /// </summary>
    public static void RegisterFonts(IFontCollection fonts)
    {
        fonts.AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold);
        fonts.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");
        fonts.AddFont("Fonts/Ubuntu-Italic.ttf", "Ubuntu", FontStyle.Italic);
        fonts.AddFont("Fonts/Font Awesome 6 Free-Solid-900.otf", "FontAwesomeSolid");
        fonts.AddFont("Fonts/Font Awesome 6 Free-Regular-400.otf", "FontAwesomeRegular");
        fonts.AddFont("Fonts/LibreBarcode128-Regular.ttf", "LibreBarcode128");
    }

    /// <summary>Registers the drawers for the custom elements the sample reports use.</summary>
    public static void RegisterDrawers(SkiaReportRenderer renderer)
        => renderer.WithDrawer<QRCode.QRCode>(new QRCodeDrawer());
}
