using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Pysar.Xaml;

/// <summary>
///     Ambient unsaved-file map for preview. Lookup is by <see cref="Path.GetFullPath"/> with
///     <see cref="StringComparer.OrdinalIgnoreCase"/>.
/// </summary>
public static class XamlFileOverlay
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, string>?> Current = new();

    /// <summary>
    ///     Replaces the ambient overlay for this async context so unsaved buffers can be read
    ///     instead of disk until the returned handle is disposed.
    /// </summary>
    public static IDisposable Push(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in files)
            normalized[Path.GetFullPath(pair.Key)] = pair.Value;

        var previous = Current.Value;
        Current.Value = normalized;
        return new Popper(previous);
    }

    internal static bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        stream = null;
        var map = Current.Value;
        if (map is null)
            return false;

        if (!map.TryGetValue(Path.GetFullPath(path), out var text))
            return false;

        stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return true;
    }

    private sealed class Popper(IReadOnlyDictionary<string, string>? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
