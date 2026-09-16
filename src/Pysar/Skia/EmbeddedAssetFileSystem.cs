using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Pysar.Core.Abstractions;

namespace Pysar.Skia;

/// <summary>
///     Reads report assets out of the manifest resources of every loaded assembly marked with
///     <see cref="PysarAssetSourceAttribute"/>. Paths are the ones the report author wrote
///     ("Images/logo.svg"), which are also the resource's <c>LogicalName</c>.
/// </summary>
/// <remarks>
///     This is what makes an asset declared in a platform-neutral library reachable from every host
///     that references it: the bytes travel inside the library's own assembly, through a project
///     reference and through a NuGet package alike, and need no per-host re-declaration.
///
///     Chained behind a host's own file system rather than replacing it, so a packaged asset always
///     wins over an embedded one - see <c>FallbackFileSystem</c>.
///
///     When two marked assemblies both embed the same logical name, the one indexed first wins -
///     the order <see cref="AppDomain.GetAssemblies"/> returns them in, which is the order they were
///     loaded. Later duplicates are silently ignored rather than throwing, for the same reason a bad
///     assembly is skipped rather than failing every report.
/// </remarks>
public sealed class EmbeddedAssetFileSystem : IFileSystem, ISyncFileSystem
{
    private readonly Func<Assembly[]> _assemblies;
    private readonly Lock _gate = new();

    private Dictionary<string, (Assembly Assembly, string ResourceName)>? _index;

    /// <summary>
    ///     Cheap staleness proxy for <see cref="_index"/>: a rebuild triggers when this no longer
    ///     matches the current assembly count. It is not foolproof - unloading a collectible
    ///     <see cref="System.Runtime.Loader.AssemblyLoadContext"/> and loading another leaves the
    ///     count unchanged and the index stale - but a live report host never does that.
    /// </summary>
    private int _indexedCount;

    /// <summary>Indexes the assemblies loaded into the current application.</summary>
    public EmbeddedAssetFileSystem() => _assemblies = AppDomain.CurrentDomain.GetAssemblies;

    /// <summary>Indexes exactly <paramref name="assemblies"/>.</summary>
    public EmbeddedAssetFileSystem(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var snapshot = assemblies.ToArray();
        _assemblies = () => snapshot;
    }

    public byte[]? ReadFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        if (!Resolve(Normalize(filePath), out var entry))
            return null;

        using var stream = entry.Assembly.GetManifestResourceStream(entry.ResourceName);
        if (stream is null)
            return null;

        var buffer = new byte[stream.Length];
        stream.ReadExactly(buffer);

        return buffer;
    }

    public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

    public bool Exists([NotNullWhen(true)] string? filePath)
        => !string.IsNullOrEmpty(filePath) && Resolve(Normalize(filePath), out _);

    /// <summary>
    ///     Looks the path up, rebuilding the index first when assemblies have loaded since it was
    ///     built.
    /// </summary>
    /// <remarks>
    ///     A miss is the only thing that triggers a rebuild, and only when the assembly count has
    ///     moved: hosts that load assemblies lazily - Uno, Blazor, and the design-time preview,
    ///     which loads the user's assembly after Pysar itself - would otherwise be stuck with
    ///     whatever was loaded at the first read. A hit never pays for this.
    /// </remarks>
    private bool Resolve(
        string normalizedPath, out (Assembly Assembly, string ResourceName) entry)
    {
        lock (_gate)
        {
            if (_index is not null && _index.TryGetValue(normalizedPath, out entry))
                return true;

            var assemblies = _assemblies();
            if (_index is not null && assemblies.Length == _indexedCount)
            {
                entry = default;
                return false;
            }

            _index = BuildIndex(assemblies);
            _indexedCount = assemblies.Length;

            return _index.TryGetValue(normalizedPath, out entry);
        }
    }

    private static Dictionary<string, (Assembly, string)> BuildIndex(Assembly[] assemblies)
    {
        var index = new Dictionary<string, (Assembly, string)>(StringComparer.OrdinalIgnoreCase);

        foreach (var assembly in assemblies)
        {
            if (assembly.GetCustomAttribute<PysarAssetSourceAttribute>() is null)
                continue;

            foreach (var resourceName in SafeResourceNames(assembly))
                index.TryAdd(Normalize(resourceName), (assembly, resourceName));
        }

        return index;
    }

    /// <summary>
    ///     A reflection-only or otherwise unreadable assembly is one without assets, not a startup
    ///     failure: one bad assembly in the process must not stop every report from rendering.
    /// </summary>
    private static string[] SafeResourceNames(Assembly assembly)
    {
        try
        {
            return assembly.GetManifestResourceNames();
        }
        catch (Exception exception) when (exception is NotSupportedException
            or FileLoadException or BadImageFormatException)
        {
            return [];
        }
    }

    /// <summary>
    ///     Asset paths are forward-slashed and relative, whatever separator the report used - the
    ///     same normalisation the platform file systems apply.
    /// </summary>
    private static string Normalize(string filePath) => filePath.Replace('\\', '/').TrimStart('/');
}
