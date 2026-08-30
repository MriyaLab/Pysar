# Pysar

Pysar is a report engine for .NET: you describe a paginated document once — in XAML markup
or with a fluent C# builder — hand it your data, and get back a vector PDF, a set of bitmap pages, or
a scrollable, zoomable view inside your application.

It exists because the usual options for this in .NET are either tied to Windows, tied to a designer,
or priced per developer. Pysar is none of those. The layout, pagination and rendering engine is
plain .NET on top of SkiaSharp, so the same report produces the same document on Windows, macOS,
Linux, Android, iOS and in the browser. The host application only supplies the two things that are
genuinely platform-specific: where files come from, and which fonts are available.

```csharp
var report = ReportBuilder.Create("Hello report")
    .WithPageFormat(new PageFormat { Margin = new Thickness(30) })
    .WithDetail(detail => detail.AddElement(new Text { Content = "Hello from Pysar" }))
    .Build();

await new SkiaReportRenderer().SavePdfAsync(report, "hello.pdf");
```

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

| Package | Version | Responsibility |
| --- | --- | --- |
| `Pysar` | [![NuGet](https://img.shields.io/nuget/v/Pysar)](https://www.nuget.org/packages/Pysar) | Reports, bands, layouts, repeaters, styles, triggers, data binding, and the SkiaSharp measurement, pagination, rendering and PDF engine |
| `Pysar.Xaml` | [![NuGet](https://img.shields.io/nuget/v/Pysar.Xaml)](https://www.nuget.org/packages/Pysar.Xaml) | Declarative report markup: the runtime loader and the code-behind source generator |
| `Pysar.Viewer` | [![NuGet](https://img.shields.io/nuget/v/Pysar.Viewer)](https://www.nuget.org/packages/Pysar.Viewer) | Framework-neutral viewer logic: page geometry, zoom, tile planning and the tile cache |
| `Pysar.Maui` | [![NuGet](https://img.shields.io/nuget/v/Pysar.Maui)](https://www.nuget.org/packages/Pysar.Maui) | .NET MAUI integration: app-package assets, font registration, PDF export and sharing |
| `Pysar.Avalonia` | [![NuGet](https://img.shields.io/nuget/v/Pysar.Avalonia)](https://www.nuget.org/packages/Pysar.Avalonia) | Avalonia integration: `avares://` assets, font registration and the report view |
| `Pysar.Blazor` | [![NuGet](https://img.shields.io/nuget/v/Pysar.Blazor)](https://www.nuget.org/packages/Pysar.Blazor) | Blazor integration: the report viewer component, printing through the browser |
| `Pysar.Wpf` | [![NuGet](https://img.shields.io/nuget/v/Pysar.Wpf)](https://www.nuget.org/packages/Pysar.Wpf) | WPF integration (Windows only): pack/manifest assets, font registration and the report view |

`Pysar.Viewer` is not referenced directly — it arrives transitively with a platform package.

```xml
<!-- before -->
xmlns:e="clr-namespace:Pysar.Elements;assembly=Pysar.Elements"
<!-- after: the namespace is unchanged, the assembly is not -->
xmlns:e="clr-namespace:Pysar.Elements;assembly=Pysar"
```

Most reports never hit this — Pysar elements are reached through the default
`https://mriyalab.com/pysar` namespace, which needs no assembly qualifier.

## Reports in XAML

`.rxaml` is Pysar's markup dialect: an XML document that describes a report the same way the fluent
builder does, using the same elements (`Grid`, `StackPanel`, `Frame`, `Text`, `Image`, `Repeater`)
and the same bands. It is the better fit for a report with a fixed shape — an invoice, a statement, a
certificate — because the layout stays readable as a tree instead of a chain of method calls, and it
is what makes a report editable by someone who is not writing C#: a template stored in a database, or
edited through the design-time preview in Rider and VS Code.

An `.rxaml` report supports the same binding system as the object model: `{Binding Path}` against the
element's data context, string formats, value converters, and `DataTrigger` for conditional
formatting. A `DataSource` on `DetailBand` (or any `Repeater`) puts its children in a per-item scope
over the bound collection, with an optional `DetailHeader` and `DetailFooter` that stay in the outer
scope:

```xml
<Report xmlns="https://mriyalab.com/pysar" x:DataType="local:Invoice">
  <PageFormat Size="A4" Margin="30" />

  <ReportHeaderBand>
    <Text Content="{Binding Company.Name}" FontSize="18" FontStyle="Bold" />
  </ReportHeaderBand>

  <DetailBand DataSource="{Binding Items}">
    <DetailBand.DetailHeader>
      <Text Content="Product" FontStyle="Bold" />
    </DetailBand.DetailHeader>

    <Grid ColumnDefinitions="*, 80, 80">
      <Text Grid.Column="0" Content="{Binding Product}" />
      <Text Grid.Column="1" Content="{Binding Quantity}" />
      <Text Grid.Column="2" Content="{Binding Total, StringFormat='{0:C}'}">
        <Text.Triggers>
          <DataTrigger Binding="{Binding Total}" CompareType="GreaterThan" Value="1000">
            <Setter Member="FontColor" Value="Chocolate" />
          </DataTrigger>
        </Text.Triggers>
      </Text>
    </Grid>
  </DetailBand>
</Report>
```

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

Any element accepts the MAUI-style directive `x:DataType="local:Invoice"` to declare the
data-context type of a scope. The hint is design-time only — it is ignored when the report is
loaded — and is inherited by child elements until another element declares its own;
`x:DataType=""` clears it for that subtree.
An element with a `DataSource` (or the legacy `DataSourcePath`) puts its content children in a
per-item scope (the collection's element type), while its property elements
(`DetailBand.DetailHeader` and the like) stay in the outer scope. The source generator validates
`{Binding ...}` paths — and `DataTrigger.Binding` — against the hint at build time (`PQX010` error
for an unknown member, `PQX011` warning for a type it cannot resolve), including for reports loaded
at runtime. Where the scope cannot be known, nothing is reported rather than guessed: styles and
resource dictionaries are reused across scopes, so their bindings are never validated. The XAML
designer idiom `d:DataContext="{d:DesignInstance Type=local:Invoice}"` is an accepted alternative
spelling of the same hint, used when no `x:DataType` is present on the element.

## Exporting to PDF

`SkiaReportRenderer` writes the built report as a vector PDF — text stays selectable and sharp at any
zoom, since nothing is rasterised. `Build()` must run first; a report can only be built once, so build
right before you export:

```csharp
using Pysar.Skia;

report.Build();

// straight to a file
await new SkiaReportRenderer().SavePdfAsync(report, "invoice.pdf");

// or onto any stream, e.g. an HTTP response body
await new SkiaReportRenderer().RenderToPdfAsync(report, httpContext.Response.Body);

// or as an in-memory byte array, e.g. to attach to an email
byte[] pdfBytes = await new SkiaReportRenderer().RenderToPdfBytesAsync(report);
```

On a platform package, the same output is reachable through `IReportExportService` instead, which is
what makes it injectable and lets `ExportFormat` stand in for a future non-PDF format without changing
call sites:

```csharp
byte[] pdfBytes = await exportService.ExportAsync(report, ExportFormat.Pdf);
```

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
    .UsePysar(pysar => pysar
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
```

`SkiaReportRenderer`, `IReportExportService`, `IReportSharer` and `IReportPrinter` are then
injectable: the export service writes a report to a stream or a byte array in a requested
`ExportFormat`, the sharer offers those bytes to the platform share sheet, and the printer opens the
platform print dialog.

```xml
<pysar:ReportView Report="{Binding Report}"
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
    .UsePysar(pysar => pysar
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
resources. Call `UsePysar` from `Application.OnStartup`:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    this.UsePysar(pysar => pysar
        .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
        .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
}
```

```xml
<pysar:ReportView Report="{Binding Report}"
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
builder.Services.AddPysar(renderer => renderer.WithDrawer<QRCode>(new QRCodeDrawer()));
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

## Documentation

- [Quick start](docs/quick-start.md)
- [API versioning policy](docs/api-versioning.md)
- [Design specifications](docs/superpowers/specs)

### IDE plugins

The Rider and VS Code plugins are not yet published to their marketplaces — install them manually
from the direct download links below:

- [Rider plugin (0.0.32)](https://mriyalab.github.io/pysar/plugins/Rider/pysar-rider-0.0.32.zip)
- [VS Code plugin (0.0.3)](https://mriyalab.github.io/pysar/plugins/VSCode/pysar-0.0.3.vsix)

A Visual Studio plugin is not available yet — it is coming soon.

## One rule worth knowing up front

`Report.Build()` mutates the report tree: it resolves bindings, expands repeaters and applies
triggers. A `Report` instance can therefore be built **once**. Create or load a new instance for each
document — building the same one twice throws.

## License

MIT — see [LICENSE](LICENSE).
