using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;

namespace Pysar.Blazor;

/// <summary>
///     Every asset a report can ask for, fetched once and then held in memory.
/// </summary>
/// <remarks>
///     Not an <see cref="HttpClient"/>-backed file system, which is what a browser would suggest. A
///     font is loaded through <c>SkiaFontCollection</c>, which reads it through
///     <see cref="ISyncFileSystem"/> when the file system offers one and otherwise blocks on
///     <c>ReadFileAsync</c>. On the browser's single thread that block is a deadlock rather than a
///     stall, so there is no asynchronous option here at all: everything is fetched up front, and
///     every read afterwards is a dictionary lookup.
/// </remarks>
public sealed class PreloadedFileSystem : IFileSystem, ISyncFileSystem
{
    private readonly Dictionary<string, byte[]> _files;

    private PreloadedFileSystem(Dictionary<string, byte[]> files) => _files = files;

    /// <summary>Wraps assets that have already been loaded.</summary>
    public static PreloadedFileSystem From(IEnumerable<KeyValuePair<string, byte[]>> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var byPath = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, content) in files)
            byPath[Normalise(path)] = content;

        return new PreloadedFileSystem(byPath);
    }

    /// <summary>
    ///     Fetches every path relative to the application's base address, in order.
    /// </summary>
    /// <remarks>
    ///     A report's XAML resource dictionaries belong in <paramref name="paths"/> alongside its
    ///     fonts and images. The generated <c>InitializeComponent</c> carries the build machine's
    ///     absolute directory, and the package-path fallback that avoids using it is only taken when
    ///     this file system already holds the path.
    /// </remarks>
    public static async Task<PreloadedFileSystem> FetchAsync(HttpClient http, IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(paths);

        var files = new List<KeyValuePair<string, byte[]>>();

        foreach (var path in paths)
            files.Add(new KeyValuePair<string, byte[]>(path, await http.GetByteArrayAsync(path)));

        return From(files);
    }

    public byte[]? ReadFile(string filePath)
        => _files.TryGetValue(Normalise(filePath), out var content) ? content : null;

    public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

    public bool Exists([NotNullWhen(true)] string? filePath)
        => !string.IsNullOrEmpty(filePath) && _files.ContainsKey(Normalise(filePath));

    /// <summary>
    ///     Reports author asset paths with forward slashes, but a path can reach here after a
    ///     <c>Path.Combine</c>, which on some hosts inserts the other kind. Both spellings, and a
    ///     leading slash, must find the same file.
    /// </summary>
    private static string Normalise(string path) => path.Replace('\\', '/').TrimStart('/');
}
