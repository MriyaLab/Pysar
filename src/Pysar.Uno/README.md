# Pysar.Uno

Uno Platform integration for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform report
engine for .NET: packaged asset access, font registration, a scrollable, zoomable `ReportView` and
PDF printing. It installs a `UnoReportPlatformHandler` for file and font access.

One `net10.0` target covers every Uno Skia host — Desktop, WebAssembly, Android and iOS. The Windows
App SDK head is not supported yet.

## Preview package

`Pysar.Uno` ships as a prerelease, and will until Uno Platform 7.0 is released.

The reason is a version conflict, not an unfinished package. Pysar renders through SkiaSharp
4.151.2, and one managed SkiaSharp is resolved for a whole application. Uno 6.7's Skia hosts depend
on SkiaSharp 3.119, so pairing them would unify Uno's own renderer onto a major version it was not
compiled against. Uno 7.0 moved to the 4.151.x line, which matches — so this package targets Uno 7.0
and cannot be published as stable before it is.

## Packaging report assets

Assets are `EmbeddedResource` items whose `LogicalName` is the exact path the report asks for:

```xml
<ItemGroup>
  <EmbeddedResource Include="Fonts\**" LogicalName="Fonts/%(Filename)%(Extension)" />
  <EmbeddedResource Include="Images\**" LogicalName="Images/%(Filename)%(Extension)" />
  <EmbeddedResource Include="Styles\**" LogicalName="Styles/%(Filename)%(Extension)" />
</ItemGroup>
```

Embedded resources rather than the `ms-appx:///` URIs an Uno application usually reaches for, because
asset reads here have to be synchronous. `SkiaFontCollection.AddFont`, a `ResourceDictionary`
`Source` in `.rxaml` and the image renderer all read without awaiting; `StorageFile` is asynchronous,
and on the WebAssembly host blocking on it is a deadlock on the single thread rather than a stall.
A manifest resource is readable synchronously on every host.

An application that already ships its assets as `Content` can fetch them once at startup instead:

```csharp
await PysarUno.UseAsync(
    typeof(App).Assembly,
    ["Fonts/Ubuntu-Regular.ttf", "Images/logo.svg"],
    pysar => pysar.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu"));
```

Every read after that is a dictionary lookup, so the synchronous callers are satisfied.

## Setup

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    PysarUno.Use(typeof(App).Assembly, pysar => pysar
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));

    // ... the rest of your launch
}
```

A static entry point rather than a service registration: an Uno application is a
`Microsoft.UI.Xaml.Application` and has no service collection of its own unless it also uses
Uno.Extensions. `Use` returns the handler, for an application that needs it beyond what `ReportView`
wires up.

## Showing a report

```xml
xmlns:pysar="using:Pysar.Uno"

<pysar:ReportView Report="{Binding Report}"
                  ZoomMode="FitWidth"
                  Zoom="{Binding Zoom}"
                  EffectiveZoom="{Binding EffectiveZoom}"
                  CurrentPage="{Binding CurrentPage}"
                  PageSpacing="24" />
```

The report must already have `Build()` called.

| Property | |
| --- | --- |
| `Report` | The built report to show |
| `ZoomMode` | `FitWidth`, `FitPage` or `Custom` |
| `Zoom` | The factor used when `ZoomMode` is `Custom`; 1 is 100% |
| `EffectiveZoom` | What the current mode actually resolved to — read this to display a percentage, since `Zoom` holds what was asked for |
| `CurrentPage` | The page at the top of the viewport, one-based; two-way |
| `PageCount` | Pages in the built report |
| `PageSpacing` | The gap between two pages |
| `PageBorderColor`, `PageBorderThickness` | The line framing each page; thickness 0 leaves it unframed |
| `DocumentPadding` | The space between the viewport edges and the document |
| `VerticalOverdraw` | How far past the viewport to keep tiles sharp, as a fraction of its height |
| `RenderBudget` | Megabytes the visible pages may occupy before pages stop being drawn whole |
| `RenderFailed` | Raised when a report could not be prepared or a page could not be drawn |

`Ctrl` or the platform command key with the wheel zooms around the pointer; a plain wheel scrolls.
A trackpad or touch pinch zooms, and a double tap magnifies and returns.

Only the visible region is rasterised, so memory follows the size of the viewport rather than the
zoom level. The arithmetic behind that — page geometry, zoom, tile planning — lives in
`Pysar.Viewer` and is shared with every other platform package.

## Printing

```csharp
var printer = new UnoReportPrinter(PysarUno.Renderer);
await printer.PrintAsync(builtReport);
```

Desktop only. macOS shows the system Print panel through PDFKit, Windows uses the shell print verb,
and Linux opens the PDF in the default viewer to print from there.

Android, iOS and WebAssembly throw `PlatformNotSupportedException`: each of their print paths belongs
to an activity, a view controller or the browser, and none is reachable from a class library without
that platform's own target framework — which this package deliberately does not have. Produce the
bytes yourself on those hosts and share or download them:

```csharp
var pdf = await PysarUno.ExportService.ExportAsync(builtReport, ExportFormat.Pdf);
```

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
