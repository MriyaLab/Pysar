using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Platform;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;
using ISyncFileSystem = Pysar.Core.Abstractions.ISyncFileSystem;

namespace Pysar.Avalonia;

/// <summary>
///     Reads report assets - fonts, images, resource dictionaries - out of the application's own
///     resources. Paths are the ones the report author wrote ("Images/logo.svg"), which are also the
///     path component of the corresponding <c>AvaloniaResource</c> item's <c>avares://</c> URI, or -
///     for assets that cannot be an <c>AvaloniaResource</c>, see below - a plain embedded resource's
///     <c>LogicalName</c>.
/// </summary>
/// <remarks>
///     Nothing is extracted to disk: both lookups below read straight out of the assembly, which is
///     also the only place the content exists on a published single-file build - so
///     <see cref="Exists"/> and <see cref="ReadFile"/> can both stay synchronous.
///
///     A path is tried as an <c>avares://</c> asset first, and as a plain embedded manifest
///     resource second. The samples in this repository need only the first: report documents end
///     in ".rxaml", which Avalonia's build task leaves alone, so they travel as ordinary
///     <c>AvaloniaResource</c> items. The fallback stays for hosts that embed their assets some
///     other way, declaring them as <c>EmbeddedResource</c> with a <c>LogicalName</c> equal to the
///     path the report asks for.
/// </remarks>
public sealed class AvaloniaAssetFileSystem(string assemblyName) : IFileSystem, ISyncFileSystem
{
    private readonly string _assemblyName = assemblyName;

    public byte[]? ReadFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var normalized = Normalize(filePath);

        var uri = ResolveAvares(normalized);
        if (AssetLoader.Exists(uri))
        {
            using var stream = AssetLoader.Open(uri);
            using var buffer = new MemoryStream();

            stream.CopyTo(buffer);

            return buffer.ToArray();
        }

        return ReadManifestResource(normalized);
    }

    public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

    public bool Exists([NotNullWhen(true)] string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var normalized = Normalize(filePath);

        return AssetLoader.Exists(ResolveAvares(normalized)) || FindManifestResourceName(normalized) is not null;
    }

    /// <summary>
    ///     Report paths are always forward-slashed and relative, whatever separator the report used;
    ///     the <c>avares://</c> scheme needs them rooted under the assembly name.
    /// </summary>
    private Uri ResolveAvares(string normalizedPath) => new($"avares://{_assemblyName}/{normalizedPath}");

    private byte[]? ReadManifestResource(string normalizedPath)
    {
        var resourceName = FindManifestResourceName(normalizedPath);
        if (resourceName is null)
            return null;

        var assembly = ResolveAssembly();
        using var stream = assembly?.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    /// <summary>
    ///     A <c>LogicalName</c> becomes the manifest resource's name verbatim, so the exact
    ///     forward-slashed path the report asks for is what is looked up here - no dotted-name
    ///     conversion, unlike a resource without an explicit logical name.
    /// </summary>
    private string? FindManifestResourceName(string normalizedPath)
        => ResolveAssembly()?.GetManifestResourceNames()
            .FirstOrDefault(name => string.Equals(name, normalizedPath, StringComparison.Ordinal));

    private Assembly? ResolveAssembly()
        => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == _assemblyName)
            ?? TryLoad(_assemblyName);

    private static Assembly? TryLoad(string assemblyName)
    {
        try
        {
            return Assembly.Load(new AssemblyName(assemblyName));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Normalize(string filePath) => filePath.Replace('\\', '/').TrimStart('/');
}
