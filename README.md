# Pysar

Pysar is a report engine for .NET: you describe a paginated document once — in XAML markup
or with a fluent C# builder — hand it your data, and get back a vector PDF, a set of bitmap pages, or
a scrollable, zoomable view inside your application.

It exists because the usual options for this in .NET are either tied to Windows, tied to a designer,
or priced per developer. QReport is none of those. The layout, pagination and rendering engine is
plain .NET on top of SkiaSharp, so the same report produces the same document on Windows, macOS,
Linux, Android, iOS and in the browser. The host application only supplies the two things that are
genuinely platform-specific: where files come from, and which fonts are available.

```csharp
var report = ReportBuilder.Create("Hello report")
    .WithPageFormat(new PageFormat { Margin = new Thickness(30) })
    .WithDetail(detail => detail.AddElement(new Text { Content = "Hello from QReport" }))
    .Build();

await new SkiaReportRenderer().SavePdfAsync(report, "hello.pdf");
```

> **Project status:** active development, pre-1.0. The public API and the XAML language are usable
> and covered by tests, but they may still receive documented breaking changes before the first
> stable release.

## What you get

A report is a vertical stack of bands — report header, page header, detail, page footer, report
footer — and the detail band is the one that repeats over your data. Inside a band you compose the
usual layout primitives: `Grid`, `StackPanel`, `Frame`, `Text`, `Image`, plus `Repeater` for nested
master-detail groups.

- **Two authoring styles, one object model.** `.rxaml` markup for reports with a fixed shape
  (invoices, statements, certificates), the fluent builder for reports whose structure is computed at
  runtime. They produce the same tree and can be mixed.
- **Data binding with `{Binding}`**, string formats, value converters and data triggers for
  conditional formatting. Bindings resolve once, at build time — a report is a document, not a UI.
- **Real pagination.** Detail rows are sliced across pages, detail headers can repeat on every page,
  page bands are re-resolved per page, and `PageNumber` / `PageCount` are available to bindings and
  to an `OnPageChangedAsync` hook.
- **Vector PDF output** — text stays selectable and sharp at any zoom — plus bitmap page rendering at
  an arbitrary scale.
- **A tiled viewer control** shared by every UI framework: only the visible region is rasterised, so
  memory follows the size of the viewport rather than the zoom level.
- **Compile-time binding validation** for `.rxaml`, and a design-time preview in Rider and VS Code
  through the IDE plugins.

Everything is measured in points (1/72"). A4 portrait is 595.5 × 842 pt.

## Requirements

- .NET 10 SDK
- SkiaSharp native assets for the target platform (the platform packages bring the right ones)
- A platform implementation of `IReportPlatformHandler` for file and font access — or one of the
  platform packages below, which install one for you

## Packages

A consumer names at most three packages: `Pysar` always, `Pysar.Xaml` for reports
written in `.rxaml` markup, and one platform package for the target UI framework.

| Package | Responsibility |
| --- | --- |
| `Pysar` | Reports, bands, layouts, repeaters, styles, triggers, data binding, and the SkiaSharp measurement, pagination, rendering and PDF engine |
| `Pysar.Xaml` | Declarative report markup: the runtime loader and the code-behind source generator |
| `Pysar.Viewer` | Framework-neutral viewer logic: page geometry, zoom, tile planning and the tile cache |
| `Pysar.Maui` | .NET MAUI integration: app-package assets, font registration, PDF export and sharing |
| `Pysar.Avalonia` | Avalonia integration: `avares://` assets, font registration and the report view |
| `Pysar.Blazor` | Blazor integration: the report viewer component, printing through the browser |
| `Pysar.Wpf` | WPF integration (Windows only): pack/manifest assets, font registration and the report view |

`Pysar.Viewer` is not referenced directly — it arrives transitively with a platform package.

### Upgrading from 0.1.0.x

`Pysar.Core`, `.Binding`, `.Elements`, `.Export`, `.Skia`, `.Xaml.Model` and `.Xaml.SourceGen`
were merged away. Replace all of them with `Pysar`, and `Pysar.Xaml` if you write
`.rxaml` reports — the source generator now ships inside that package.

Namespaces did not change, so no `using` directive needs editing. The one thing that can break is a
`clr-namespace` mapping in `.rxaml` that names a QReport **assembly**, because five assemblies became
one:

```xml
<!-- before -->
xmlns:e="clr-namespace:Pysar.Elements;assembly=Pysar.Elements"
<!-- after: the namespace is unchanged, the assembly is not -->
xmlns:e="clr-namespace:Pysar.Elements;assembly=Pysar"
```

Most reports never hit this — QReport elements are reached through the default
`https://mriyalab.com/pysar` namespace, which needs no assembly qualifier.

## Reports in XAML

Reports can be loaded at runtime, which is what a template stored in a database or edited by a user
needs:

```csharp
using Pysar.Xaml;

var report = ReportXaml.Load("""
    <Report xmlns="https://mriyalab.com/pysar">
      <PageFormat Size="A4" Margin="30" />
      <DetailBand>
        <Text Content="Hello from XAML" />
      </DetailBand>
    </Report>
    """);

report.Build();
```

For application projects, the source generator in `Pysar.Xaml` uses the standard `x:Class`
directive to provide generated `InitializeComponent()`, strongly typed `x:Name` fields, and compiled
object construction for supported XAML. The legacy `Report.CodeBehind` attribute remains available
for compatibility. Resources, styles, and triggers currently use the runtime-loader fallback.

Any element accepts the standard designer hint `d:DataContext="{d:DesignInstance Type=local:Invoice}"`
to declare the data-context type of a scope. The hint is design-time only — it is ignored when the
report is loaded — and is inherited by child elements until another element declares its own.
An element with a `DataSource` (or the legacy `DataSourcePath`) puts its content children in a
per-item scope (the collection's element type), while its property elements
(`DetailBand.DetailHeader` and the like) stay in the outer scope. The source generator validates
`{Binding ...}` paths — and `DataTrigger.Binding` — against the hint at build time (`PQX010` error
for an unknown member, `PQX011` warning for a type it cannot resolve), including for reports loaded
at runtime. Where the scope cannot be known, nothing is reported rather than guessed: styles and
resource dictionaries are reused across scopes, so their bindings are never validated. MAUI-style
`x:DataType` is honoured as a fallback.

## Showing a report

Every platform package offers the same `ReportView`: a built report as scrollable, zoomable pages,
rasterised one visible tile at a time. The properties are the same everywhere.

`PageBorderColor` and `PageBorderThickness` frame each page, which is what tells a page from the
surface behind it when both are light; the line is drawn outside the paper, so the report itself is
never covered by it. `PageSpacing` is the gap between two pages, in device independent units — it
does not scale with the zoom, so the gap looks the same however far in the reader is.

A fit mode resolves to a factor only the control knows, so bind `EffectiveZoom` to show the
percentage: `Zoom` holds what was asked for, not what the view settled on.

### .NET MAUI

Fonts and images are declared as `MauiAsset` items and read straight from the application package —
nothing is extracted to disk:

```xml
<MauiAsset Include="Fonts\**" LogicalName="Fonts/%(Filename)%(Extension)" />
<MauiAsset Include="Images\**" LogicalName="Images/%(Filename)%(Extension)" />
```

```csharp
builder
    .UseMauiApp<App>()
    .UseQReport(qreport => qreport
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
```

`SkiaReportRenderer`, `IReportExportService`, `IReportSharer` and `IReportPrinter` are then
injectable: the export service writes a report to a stream or a byte array in a requested
`ExportFormat`, the sharer offers those bytes to the platform share sheet, and the printer opens the
platform print dialog.

```xml
<qreport:ReportView Report="{Binding Report}"
                    ZoomMode="FitWidth"
                    Zoom="{Binding Zoom}"
                    EffectiveZoom="{Binding EffectiveZoom}"
                    CurrentPage="{Binding CurrentPage}"
                    PageSpacing="24" />
```

Pinch zooms around the fingers and a double tap switches between fitting the width and reading close
up, both anchored on the point they were made at.

Android, iOS and Mac Catalyst are the platforms the package is built for; the Windows target only
appears when packing on a Windows host, and ships untested.

### Avalonia

`Pysar.Avalonia` builds against Avalonia 12.1.1 and offers the same `ReportView` over the
same `Pysar.Viewer` core. Assets are `AvaloniaResource` items read through `avares://`:

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseQReport(qreport => qreport
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
```

On the desktop, `Ctrl` or `Cmd` with the wheel zooms around the pointer, a plain wheel scrolls, and a
double click magnifies and returns.

Trackpad pinch on macOS is handled natively, in `MacPinchMonitor`. Avalonia delivers no pinch gesture
on that platform — measured over two branches, 11.3.12 and 12.1.1, where a trackpad pinch produced
only wheel events and not one `PinchEvent` — so the zoom comes from an AppKit local event monitor for
`NSEventTypeMagnify`, reached through the Objective-C runtime. It is a no-op everywhere else.

That file is the one part of the viewer no test can reach, and an exception crossing the native
boundary ends the process rather than raising: **verify any change to it by hand, on a trackpad.**

### WPF

`Pysar.Wpf` is **Windows only**, with assets resolved from pack URIs and assembly manifest
resources. Call `UseQReport` from `Application.OnStartup`:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    this.UseQReport(qreport => qreport
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
}
```

```xml
<qreport:ReportView Report="{Binding Report}"
                    ZoomMode="FitWidth"
                    Zoom="{Binding Zoom}"
                    EffectiveZoom="{Binding EffectiveZoom, Mode=OneWayToSource}"
                    CurrentPage="{Binding CurrentPage}"
                    PageSpacing="24" />
```

Ctrl + wheel zooms around the pointer; a plain wheel scrolls; double-click toggles fit width and
close-up. On macOS and Linux the project still restores and builds as a `net10.0` stub so the
solution stays green; the real WPF sources compile only under `net10.0-windows`.

### Blazor

Registration is a service-collection call, and the same renderer serves both the viewer and the
exporters — so a custom drawer registered here reaches the screen as well as the PDF:

```csharp
builder.Services.AddQReport(renderer => renderer.WithDrawer<QRCode>(new QRCodeDrawer()));
```

The browser has no file system, so assets are fetched up front and held in memory. This is not a
preference: fonts are loaded synchronously, and a blocking read on the browser's single thread is a
deadlock rather than a stall.

```csharp
var files = await PreloadedFileSystem.FetchAsync(http, [
    "Fonts/Ubuntu-Regular.ttf",
    "Images/logo.svg"
]);

WasmPlatformHandler.Install(files);
```

```razor
<ReportView Report="@_report" ZoomMode="@ReportZoomMode.FitWidth" PageSpacing="24" />
```

`IReportPrinter` resolves to `BlazorReportPrinter`, which renders the report to PDF off the UI thread
and hands it to the browser's own print dialog. It is registered scoped, because it holds a JS module
reference belonging to one browser context.

## Repository layout

```
src/    the packages listed above
tests/  one test project per area, plus tests/Assets for shared fixtures
docs/   quick start, versioning policy, design specs
```

Sample applications are not part of this repository. What they used to demonstrate — application
startup, asset resolution, and the report view responding to real input — is covered by tests
instead, so it is verified on every push rather than by hand:

| Test project | What it covers |
| --- | --- |
| `Pysar.Elements.Tests`, `.Binding.Tests` | the element tree, bindings, converters, triggers |
| `Pysar.Skia.Tests` | measurement, layout, pagination, drawing, PDF export |
| `Pysar.Xaml*.Tests` | the XAML model, runtime loader, source generator and code-behind |
| `Pysar.Viewer.Tests` | page geometry, zoom, gestures, tile planning — framework-neutral |
| `Pysar.Avalonia.Tests` | a real Avalonia app on the headless platform: `UseQReport`, `avares://` assets, a report shown in a window and zoomed with injected input |
| `Pysar.Wpf.Tests` | the same, on a WPF STA session — Windows only, exactly like the package itself |
| `Pysar.Blazor.Tests` | service registration, the preloaded file system, printing through a fake JS runtime |
| `Pysar.Maui.Tests` | package asset access and the platform handler over it |
| `Pysar.Architecture.Tests` | the dependency rules between the projects above |

Two gaps are deliberate, because CI cannot close them: the MAUI views need a device or an emulator,
and `MacPinchMonitor` needs a trackpad.

## Build and test

```bash
dotnet restore Pysar.sln
dotnet build Pysar.sln --configuration Release
dotnet test Pysar.sln --configuration Release --no-build
```

CI uses `Pysar.CI.slnf` instead — the same solution minus `Pysar.Maui`, which needs
the `maui` workload and is built and packed in a job of its own. The test matrix runs on
`ubuntu-latest` and `windows-latest`; the WPF projects are non-test `net10.0` placeholders on the
Linux leg and the real thing on the Windows one, so a single `dotnet test` covers both.

## Documentation

- [Quick start](docs/quick-start.md)
- [API versioning policy](docs/api-versioning.md)
- [Design specifications](docs/superpowers/specs)
- IDE plugins — see the `Pysar.Plugins` repository (sibling project, not part of this repo)

## One rule worth knowing up front

`Report.Build()` mutates the report tree: it resolves bindings, expands repeaters and applies
triggers. A `Report` instance can therefore be built **once**. Create or load a new instance for each
document — building the same one twice throws.

## License

MIT — see [LICENSE](LICENSE).
