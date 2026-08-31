# Pysar.Maui

.NET MAUI integration for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform report engine
for .NET: application-package asset access, font registration, PDF export, sharing and a scrollable,
zoomable `ReportView`. It installs a `MauiReportPlatformHandler` for file and font access, and brings
the right SkiaSharp native assets for each target platform.

Android, iOS and Mac Catalyst are the platforms the package is built for; the Windows target only
appears when packing on a Windows host, and ships untested.

## Setup

Fonts and images are declared as `MauiAsset` items and read straight from the application package —
nothing is extracted to disk:

```xml
<MauiAsset Include="Fonts\**" LogicalName="Fonts/%(Filename)%(Extension)" />
<MauiAsset Include="Images\**" LogicalName="Images/%(Filename)%(Extension)" />
```

```csharp
builder
    .UseMauiApp<App>()
    .UsePysar(pysar => pysar
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
```

`SkiaReportRenderer`, `IReportExportService`, `IReportSharer` and `IReportPrinter` are then
injectable: the export service writes a report to a stream or a byte array in a requested
`ExportFormat`, the sharer offers those bytes to the platform share sheet, and the printer opens the
platform print dialog.

```csharp
byte[] pdfBytes = await exportService.ExportAsync(report, ExportFormat.Pdf);
```

## Showing a report

```xml
<pysar:ReportView Report="{Binding Report}"
                    ZoomMode="FitWidth"
                    Zoom="{Binding Zoom}"
                    EffectiveZoom="{Binding EffectiveZoom}"
                    CurrentPage="{Binding CurrentPage}"
                    PageSpacing="24" />
```

Pinch zooms around the fingers and a double tap switches between fitting the width and reading close
up, both anchored on the point they were made at. `PageBorderColor` and `PageBorderThickness` frame
each page; `PageSpacing` is the gap between pages and does not scale with the zoom. Bind
`EffectiveZoom` to show the zoom percentage — `Zoom` holds what was asked for, not what the view
settled on.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
