using Pysar.Console.Sample.Reports.CustomControls;
using Pysar.Core;
using Pysar.Core.Enums;
using Pysar.Skia;

namespace Pysar.Console.Sample;

/// <summary>
///     Shared by <c>Program</c> and the design-time .rxaml preview host (discovered by reflection).
///     Without this, the preview has no platform handler: custom fonts and images silently vanish.
/// </summary>
public sealed class ReportBootstrap : IReportBootstrap
{
    public static void Initialize(SkiaReportRenderer renderer)
    {
        ReportPlatformHandler.Create(new FileSystemPlatformHandler());
        var fonts = ReportPlatformHandler.FontCollection;
        fonts.AddFont("Fonts/Kanit-Bold.ttf", "Kanit", FontStyle.Bold);
        fonts.AddFont("Fonts/Kanit-Regular.ttf", "Kanit");
        fonts.AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold);
        fonts.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");
        renderer.WithDrawer<QRCode>(new QRCodeDrawer());
    }
}
