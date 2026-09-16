using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;
using ISyncFileSystem = Pysar.Core.Abstractions.ISyncFileSystem;

namespace Pysar.Uno;

/// <summary>
///     Reads report assets - fonts, images, resource dictionaries - out of the application's own
///     assembly. Paths are the ones the report author wrote ("Images/logo.svg"), which are also the
///     <c>LogicalName</c> the asset is declared under.
/// </summary>
/// <remarks>
///     Synchronous on purpose, and not through <c>ms-appx:///</c>. <c>SkiaFontCollection.AddFont</c>,
///     a <c>ResourceDictionary</c> <c>Source</c> and the image renderer all read through
///     <see cref="ISyncFileSystem"/> without awaiting; Uno's <c>StorageFile</c> is asynchronous, and
///     on the WebAssembly host blocking on it is a deadlock on the single thread rather than a
///     stall - the same reasoning <c>Pysar.Blazor</c>'s <c>PreloadedFileSystem</c> carries. A
///     manifest resource is readable synchronously on every Uno host, so that is the path here.
///
///     An application that ships its assets as <c>Content</c> rather than <c>EmbeddedResource</c>
///     fetches them once through <see cref="PreloadAsync"/> and they are served from the same
///     dictionary afterwards.
/// </remarks>
public sealed class UnoAssetFileSystem : IFileSystem, ISyncFileSystem
{
    private readonly Assembly _assembly;

    private readonly Dictionary<string, byte[]> _preloaded =
        new(StringComparer.OrdinalIgnoreCase);

    /// <param name="assembly">
    ///     The assembly report assets are embedded in. Pass the application's own assembly, or the
    ///     shared project's when the assets live there.
    /// </param>
    public UnoAssetFileSystem(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        _assembly = assembly;
    }

    /// <summary>Holds assets already in hand, so later reads of them are a dictionary lookup.</summary>
    /// <remarks>
    ///     Takes precedence over an embedded resource at the same path: an application that has
    ///     replaced a packaged asset must see its replacement.
    /// </remarks>
    public void Preload(IEnumerable<KeyValuePair<string, byte[]>> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);

        foreach (var (path, content) in assets)
            _preloaded[Normalize(path)] = content;
    }

    /// <summary>
    ///     Fetches each path from the application package over <c>ms-appx:///Assets/...</c> and
    ///     holds it, for applications that ship assets as <c>Content</c> under <c>Assets/</c>
    ///     rather than <c>EmbeddedResource</c>.
    /// </summary>
    /// <remarks>
    ///     Awaited once during startup, before any report is built. Every read afterwards is
    ///     synchronous, which is the only thing font registration and the image renderer accept.
    ///     A path that is not in the package is skipped rather than throwing, so one missing image
    ///     does not stop an application from starting; <see cref="Exists"/> then reports it
    ///     truthfully and the report renders without it.
    /// </remarks>
    public async Task PreloadAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var fetched = new List<KeyValuePair<string, byte[]>>();

        foreach (var path in paths)
        {
            var normalized = Normalize(path);

            try
            {
                var file = await StorageFile
                    .GetFileFromApplicationUriAsync(new Uri(ToPackageUri(normalized)));

                var buffer = await FileIO.ReadBufferAsync(file);

                fetched.Add(new KeyValuePair<string, byte[]>(normalized, buffer.ToArray()));
            }
            catch (Exception)
            {
                // Every failure to fetch one path is the same thing to this method: that path is not
                // available, which Exists reports and the report renders without - see the remarks.
                //
                // Caught broadly on purpose. A missing ms-appx asset does not surface as one
                // exception type across Uno's hosts: Windows raises FileNotFoundException, the
                // WebAssembly host resolves through an HTTP fetch and raises whatever that failed
                // with, and a malformed path throws UriFormatException before any host is reached.
                // Narrowing this to FileNotFoundException would keep the promise above only on
                // Windows and let one missing image stop the application from starting everywhere
                // else.
            }
        }

        Preload(fetched);
    }

    public byte[]? ReadFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var normalized = Normalize(filePath);

        if (_preloaded.TryGetValue(normalized, out var preloaded))
            return preloaded;

        return ReadManifestResource(normalized);
    }

    public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

    public bool Exists([NotNullWhen(true)] string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var normalized = Normalize(filePath);

        return _preloaded.ContainsKey(normalized) || FindManifestResourceName(normalized) is not null;
    }

    private byte[]? ReadManifestResource(string normalizedPath)
    {
        var resourceName = FindManifestResourceName(normalizedPath);
        if (resourceName is null)
            return null;

        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    /// <summary>
    ///     A <c>LogicalName</c> becomes the manifest resource's name verbatim, so the exact
    ///     forward-slashed path the report asks for is what is looked up - no dotted-name
    ///     conversion, unlike a resource without an explicit logical name.
    /// </summary>
    private string? FindManifestResourceName(string normalizedPath)
        => _assembly.GetManifestResourceNames()
            .FirstOrDefault(name => string.Equals(name, normalizedPath, StringComparison.Ordinal));

    /// <summary>
    ///     The ms-appx URI for a report path. Uno packages content under <c>Assets/</c>, while the
    ///     report still asks for <c>Fonts/...</c> — the same path Maui, WPF and Avalonia use.
    /// </summary>
    internal static string ToPackageUri(string path)
    {
        var reportPath = ToReportPath(path);
        return "ms-appx:///Assets/" + reportPath;
    }

    /// <summary>Report paths are always forward-slashed and relative, whatever the report wrote.</summary>
    private static string ToReportPath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/').TrimStart('/');
        const string assetsPrefix = "Assets/";
        return normalized.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[assetsPrefix.Length..]
            : normalized;
    }

    private static string Normalize(string filePath) => ToReportPath(filePath);
}
