using Microsoft.JSInterop;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using Xunit;

namespace Pysar.Blazor.Tests;

/// <summary>
///     Printing from the browser, which the Blazor samples used to be the only proof of: the report
///     becomes a PDF and is handed to the print module, once per printer.
/// </summary>
public class BlazorReportPrinterTests
{
    [Fact]
    public async Task PrintAsync_HandsThePdfToThePrintModule()
    {
        var js = new RecordingJsRuntime();
        var printer = new BlazorReportPrinter(new SkiaReportRenderer(), js);

        await printer.PrintAsync(BuildReport());

        Assert.Equal("./_content/Pysar.Blazor/reportPrint.js", js.ImportedModule);
        Assert.Equal("printPdf", js.Module.Identifier);

        var pdf = Assert.IsType<byte[]>(js.Module.Arguments![0]);

        // A PDF, not just any bytes: the browser is handed a document, not a bitmap.
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public async Task PrintAsync_HandsPageSizeAndOrientationToThePrintModule()
    {
        var js = new RecordingJsRuntime();
        var printer = new BlazorReportPrinter(new SkiaReportRenderer(), js);

        await printer.PrintAsync(BuildReport(Orientation.Landscape));

        Assert.Equal("printPdf", js.Module.Identifier);

        var args = js.Module.Arguments!;
        Assert.Equal(4, args.Length);
        Assert.IsType<byte[]>(args[0]);
        Assert.Equal(842f, args[1]);
        Assert.Equal(595.5f, args[2]);
        Assert.Equal(true, args[3]);
    }

    [Fact]
    public async Task TheModuleIsImportedOnce_AndReleasedOnDispose()
    {
        var js = new RecordingJsRuntime();
        var printer = new BlazorReportPrinter(new SkiaReportRenderer(), js);

        await printer.PrintAsync(BuildReport());
        await printer.PrintAsync(BuildReport());

        Assert.Equal(1, js.Imports);

        await printer.DisposeAsync();

        Assert.True(js.Module.Disposed);

        // Disposing twice is what a scope teardown after an explicit dispose looks like.
        await printer.DisposeAsync();
    }

    [Fact]
    public async Task PrintAsync_RejectsAMissingReport()
    {
        var printer = new BlazorReportPrinter(new SkiaReportRenderer(), new RecordingJsRuntime());

        await Assert.ThrowsAsync<ArgumentNullException>(() => printer.PrintAsync(null!));
    }

    [Fact]
    public void TheConstructor_RejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new BlazorReportPrinter(null!, new RecordingJsRuntime()));
        Assert.Throws<ArgumentNullException>(() => new BlazorReportPrinter(new SkiaReportRenderer(), null!));
    }

    private static Report BuildReport(Orientation orientation = Orientation.Portrait)
        => ReportBuilder.Create("Print")
            .WithPageFormat(new PageFormat
            {
                Margin = new Thickness(30),
                Size = PageSize.A4,
                Orientation = orientation
            })
            .WithDetail(detail => detail.AddElement(new Text { Content = "Hello" }))
            .Build();

    /// <summary>Stands in for the browser, recording what the printer asked it to do.</summary>
    private sealed class RecordingJsRuntime : IJSRuntime
    {
        public int Imports { get; private set; }

        public string? ImportedModule { get; private set; }

        public RecordingModule Module { get; } = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "import")
            {
                Imports++;
                ImportedModule = args?[0] as string;

                return ValueTask.FromResult((TValue)(object)Module);
            }

            return ValueTask.FromResult<TValue>(default!);
        }
    }

    private sealed class RecordingModule : IJSObjectReference
    {
        public string? Identifier { get; private set; }

        public object?[]? Arguments { get; private set; }

        public bool Disposed { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Identifier = identifier;
            Arguments = args;

            return ValueTask.FromResult<TValue>(default!);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
