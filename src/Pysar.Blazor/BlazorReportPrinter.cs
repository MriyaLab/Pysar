using Microsoft.JSInterop;
using Pysar.Elements;
using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Blazor;

/// <summary>Renders a built report to PDF and opens the browser print dialog.</summary>
public sealed class BlazorReportPrinter : IReportPrinter, IAsyncDisposable
{
    private readonly SkiaReportRenderer _renderer;
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BlazorReportPrinter(SkiaReportRenderer renderer, IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(js);
        _renderer = renderer;
        _js = js;
    }

    public async Task PrintAsync(Report report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        // Off the Blazor circuit / UI thread — PDF render is CPU-bound and uses sync font I/O.
        var pdfBytes = await Task.Run(
            () => _renderer.RenderToPdfBytesAsync(report, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        _module ??= await _js.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            "./_content/Pysar.Blazor/reportPrint.js").ConfigureAwait(false);

        var paper = PrintPaper.From(report.PageFormat);
        await _module.InvokeVoidAsync(
                "printPdf",
                cancellationToken,
                pdfBytes,
                paper.WidthPt,
                paper.HeightPt,
                paper.IsLandscape)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync().ConfigureAwait(false);
            _module = null;
        }
    }
}
