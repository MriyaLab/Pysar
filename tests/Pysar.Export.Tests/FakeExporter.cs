using Pysar.Elements;

namespace Pysar.Export.Tests;

internal sealed class FakeExporter : IReportExporter
{
    private readonly byte[] _output;

    public FakeExporter(ExportFormat format, byte[] output)
    {
        Format = format;
        _output = output;
    }

    public ExportFormat Format { get; }

    public Task ExportAsync(Report report, Stream destination, CancellationToken ct = default)
    {
        destination.Write(_output, 0, _output.Length);
        return Task.CompletedTask;
    }
}
