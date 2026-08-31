# Pysar.Viewer

Framework-neutral viewer logic for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform
report engine for .NET: page geometry, zoom, tile planning and the tile cache. It depends on the
core `Pysar` package and is the shared core behind every platform-specific `ReportView` control.

A built report renders as scrollable, zoomable pages, rasterised one visible tile at a time. Only the
visible region is rasterised, so memory follows the size of the viewport rather than the zoom level.

This package is not referenced directly — it arrives transitively with a platform package, which
supplies the actual UI control:

| Platform package | Control |
| --- | --- |
| `Pysar.Maui` | .NET MAUI `ReportView` |
| `Pysar.Avalonia` | Avalonia `ReportView` |
| `Pysar.Wpf` | WPF `ReportView` (Windows only) |
| `Pysar.Blazor` | Blazor `<ReportView>` component |

The properties are the same on every platform. `PageBorderColor` and `PageBorderThickness` frame each
page, which is what tells a page from the surface behind it when both are light; the line is drawn
outside the paper, so the report itself is never covered by it. `PageSpacing` is the gap between two
pages, in device independent units — it does not scale with the zoom, so the gap looks the same
however far in the reader is.

A fit mode resolves to a factor only the control knows, so bind `EffectiveZoom` to show the
percentage: `Zoom` holds what was asked for, not what the view settled on.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
