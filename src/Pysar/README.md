# Pysar

Pysar is a report engine for .NET: you describe a paginated document once — in XAML markup
or with a fluent C# builder — hand it your data, and get back a vector PDF, a set of bitmap pages, or
a scrollable, zoomable view inside your application.

This package is the core: the report element tree, data binding, and the SkiaSharp measurement,
pagination, rendering and PDF engine. Add `Pysar.Xaml` for declarative `.rxaml` markup and one
`Pysar.<Platform>` package for an in-app report view.

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

- **Two authoring styles, one object model.** `.rxaml` markup (package `Pysar.Xaml`) for reports
  with a fixed shape, the fluent builder for reports whose structure is computed at runtime. They
  produce the same tree and can be mixed.
- **Data binding with `{Binding}`**, string formats, value converters and data triggers for
  conditional formatting. Bindings resolve once, at build time — a report is a document, not a UI.
- **Real pagination.** Detail rows are sliced across pages, detail headers can repeat on every page,
  page bands are re-resolved per page, and `PageNumber` / `PageCount` are available to bindings and
  to an `OnPageChangedAsync` hook.
- **Vector PDF output** — text stays selectable and sharp at any zoom — plus bitmap page rendering at
  an arbitrary scale.

Everything is measured in points (1/72"). A4 portrait is 595.5 × 842 pt.

## Exporting to PDF

`SkiaReportRenderer` writes the built report as a vector PDF. `Build()` must run first; a report can
only be built once, so build right before you export:

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

## Requirements

- .NET 10 SDK
- SkiaSharp native assets for the target platform (the platform packages bring the right ones)
- A platform implementation of `IReportPlatformHandler` for file and font access — or one of the
  platform packages, which install one for you

## Related packages

A consumer names at most three packages: `Pysar` always, `Pysar.Xaml` for reports written in
`.rxaml` markup, and one platform package for the target UI framework.

| Package | Responsibility |
| --- | --- |
| `Pysar.Xaml` | Declarative report markup: the runtime loader and the code-behind source generator |
| `Pysar.Maui` | .NET MAUI integration: app-package assets, font registration, PDF export and sharing |
| `Pysar.Avalonia` | Avalonia integration: `avares://` assets, font registration and the report view |
| `Pysar.Blazor` | Blazor integration: the report viewer component, printing through the browser |
| `Pysar.Wpf` | WPF integration (Windows only): pack/manifest assets, font registration and the report view |

`Pysar.Viewer` — the framework-neutral viewer logic — is not referenced directly; it arrives
transitively with a platform package.

## One rule worth knowing up front

`Report.Build()` mutates the report tree: it resolves bindings, expands repeaters and applies
triggers. A `Report` instance can therefore be built **once**. Create or load a new instance for each
document — building the same one twice throws.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)
- [API versioning policy](https://github.com/MriyaLab/Pysar/blob/main/docs/api-versioning.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
