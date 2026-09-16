using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using Pysar.Export;

namespace Pysar.Uno;

/// <summary>
///     Offers exported report bytes to the host: a file download on WebAssembly, and the default
///     application for the file on desktop, Android and iOS.
/// </summary>
/// <remarks>
///     A class library on a single <c>net10.0</c> target cannot reach Android's share intent or
///     iOS's activity controller, and Uno's <c>Launcher.LaunchFileAsync</c> is not implemented.
///     The browser path inlines a one-shot JS module the way <c>BrowserWheelZoom</c> does, because
///     <c>[JSImport]</c> needs a browser target framework this package cannot have.
/// </remarks>
public sealed class UnoReportSharer : IReportSharer
{
    public async Task ShareAsync(
        byte[] content, string fileName, string? title = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(fileName);

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(safeName))
            throw new ArgumentException("File name must include a file name.", nameof(fileName));

        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsBrowser())
        {
            await DownloadInBrowserAsync(safeName, content).ConfigureAwait(true);
            return;
        }

        var path = Path.Combine(Path.GetTempPath(), safeName);
        await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();

        OpenSharedFile(path);
    }

    private static async Task DownloadInBrowserAsync(string fileName, byte[] content)
    {
        var mime = MimeType(fileName);
        var script =
            "const bytes=Uint8Array.from(atob('" + Convert.ToBase64String(content) + "'),c=>c.charCodeAt(0));" +
            "const blob=new Blob([bytes],{type:" + JsonSerializer.Serialize(mime) + "});" +
            "const url=URL.createObjectURL(blob);" +
            "const a=document.createElement('a');" +
            "a.href=url;a.download=" + JsonSerializer.Serialize(fileName) + ";" +
            "a.click();URL.revokeObjectURL(url);export{};";

        var url = "data:text/javascript;base64,"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(script));

#pragma warning disable CA1416 // Guarded by OperatingSystem.IsBrowser in ShareAsync.
        await JSHost.ImportAsync("Pysar.Uno.share." + Guid.NewGuid().ToString("N"), url)
            .ConfigureAwait(true);
#pragma warning restore CA1416
    }

    private static void OpenSharedFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException(
                $"The host could not open '{Path.GetFileName(path)}'. Save the bytes from PysarUno.ExportService and share them from the application.",
                ex);
        }
    }

    private static string MimeType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };
}
