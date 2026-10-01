# Pysar.Avalonia

Avalonia integration for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform report engine
for .NET: `avares://` application asset access, font registration and a scrollable, zoomable
`ReportView`. It installs a `DefaultReportPlatformHandler` over `AvaloniaAssetFileSystem` for file and font access.

## Setup

Assets are declared with `ReportAsset`; Pysar packages them as `AvaloniaResource` items read through
`avares://`. Register from the application builder:

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UsePysar();
```

The family string in the report has to be the Avalonia `FontFamily` string — `fonts:Inter#Inter`
after `WithInterFont` or `AddFontCollection` with the key `fonts:Inter`, or
`avares://MyApp/Fonts#Ubuntu` for an Avalonia resource folder — not a file path. A bare name
(`Ubuntu`, `Inter`) is a system-font lookup; if `TryGetGlyphTypeface` substitutes the default
face (Helvetica), Pysar treats that as a miss. `AddFont` still wins, and a library `ReportAsset`
still needs one `AddFont`: the file is not copied into the head, and `FontManager` does not see
it. A XAML `FontFamily` resource is not a registration. Styles stay `Normal`, `Bold`, `Italic`
and `BoldItalic`. SemiBold, Light, stretch and per-character fallbacks are not shared, and Bold
against a Regular-only file is not a real bold — the bytes that come back may be the regular
face, because Avalonia's simulations are not kept on the `SKTypeface`. This bridge is Avalonia
only. Console, the design-time preview, MAUI, WPF and Uno do not have `FontManager` and keep
`AddFont`.

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
var printer = new AvaloniaReportPrinter(ReportViewRenderer.Instance);
await printer.PrintAsync(builtReport);
```

The report must already have `Build()` called.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
