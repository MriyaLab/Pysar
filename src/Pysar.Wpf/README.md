# Pysar.Wpf

WPF integration for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform report engine for
.NET: pack URI and assembly manifest asset access, font registration, a scrollable, zoomable
`ReportView`, and printing. It installs a `WpfReportPlatformHandler` for file and font access.

**Windows only.** On macOS and Linux the package is not produced; the real WPF sources compile only
under `net10.0-windows`.

## Setup

Call `UsePysar` from `Application.OnStartup`:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    this.UsePysar(pysar => pysar
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
}
```

## Showing a report

```xml
<pysar:ReportView Report="{Binding Report}"
                    ZoomMode="FitWidth"
                    Zoom="{Binding Zoom}"
                    EffectiveZoom="{Binding EffectiveZoom, Mode=OneWayToSource}"
                    CurrentPage="{Binding CurrentPage}"
                    PageSpacing="24" />
```

Ctrl + wheel zooms around the pointer; a plain wheel scrolls; double-click toggles fit width and
close-up. `PageBorderColor` and `PageBorderThickness` frame each page; `PageSpacing` is the gap
between pages and does not scale with the zoom. Bind `EffectiveZoom` to show the zoom percentage —
`Zoom` holds what was asked for, not what the view settled on.

## Printing

Printing uses the same vector PDF pipeline as export:

```csharp
var printer = new WpfReportPrinter(PysarWpf.Renderer);
await printer.PrintAsync(builtReport);
```

The report must already have `Build()` called.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
