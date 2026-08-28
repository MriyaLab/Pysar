// The zoom mode now lives in the framework-neutral core, alongside the arithmetic that resolves
// it. This keeps every existing `ReportZoomMode` usage in this project - and any consumer's XAML
// that names the type - compiling unchanged.
global using ReportZoomMode = Pysar.Viewer.Zoom.ReportZoomMode;
