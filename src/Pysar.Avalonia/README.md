# Pysar.Avalonia

Avalonia integration for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform report engine
for .NET: `avares://` application asset access, font registration and a scrollable, zoomable
`ReportView`. It installs an `AvaloniaReportPlatformHandler` for file and font access.

## Setup

Assets are `AvaloniaResource` items read through `avares://`:

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UsePysar(pysar => pysar
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
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

On the desktop, `Ctrl` or `Cmd` with the wheel zooms around the pointer, a plain wheel scrolls, and a
double click magnifies and returns. `PageBorderColor` and `PageBorderThickness` frame each page;
`PageSpacing` is the gap between pages and does not scale with the zoom. Bind `EffectiveZoom` to show
the zoom percentage — `Zoom` holds what was asked for, not what the view settled on.

Trackpad pinch on macOS is handled natively, in `MacPinchMonitor`. Avalonia delivers no pinch gesture
on that platform, so the zoom comes from an AppKit local event monitor for `NSEventTypeMagnify`,
reached through the Objective-C runtime. It is a no-op everywhere else.

## Printing

Printing uses the same vector PDF pipeline as export:

```csharp
var printer = new AvaloniaReportPrinter(PysarAvalonia.Renderer);
await printer.PrintAsync(builtReport);
```

The report must already have `Build()` called.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
