using Pysar.Export;

namespace Pysar.Maui;

/// <inheritdoc />
public sealed class MauiReportSharer : IReportSharer
{
    public async Task ShareAsync(byte[] content, string fileName, string? title = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        // The cache directory is the one location every platform lets the share sheet read from
        // without additional permissions.
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, content, ct).ConfigureAwait(false);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title ?? Path.GetFileNameWithoutExtension(fileName),
            File = new ShareFile(filePath)
        });
    }
}
