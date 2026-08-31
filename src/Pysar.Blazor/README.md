# Pysar.Blazor

Blazor WebAssembly integration for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform
report engine for .NET: the `<ReportView>` component, printing through the browser, and a
`WasmPlatformHandler` for file and font access.

## Setup

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

## Showing a report

```razor
<ReportView Report="@_report" ZoomMode="@ReportZoomMode.FitWidth" PageSpacing="24" />
```

The view renders a built report as scrollable, zoomable pages, rasterised one visible tile at a
time, so memory follows the size of the viewport rather than the zoom level.

## Printing

`IReportPrinter` resolves to `BlazorReportPrinter`, which renders the report to PDF off the UI thread
and hands it to the browser's own print dialog. It is registered scoped, because it holds a JS module
reference belonging to one browser context. The report must already have `Build()` called.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
