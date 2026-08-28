using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Windows;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;
using ISyncFileSystem = Pysar.Core.Abstractions.ISyncFileSystem;

namespace Pysar.Wpf;

/// <summary>
///     Reads report assets - fonts, images, resource dictionaries - out of the application's own
///     resources. Paths are the ones the report author wrote ("Images/logo.svg"), which are also the
///     path component of a pack URI or a plain embedded resource's <c>LogicalName</c>.
/// </summary>
/// <remarks>
///     Nothing is extracted to disk: both lookups below read straight out of the assembly, which is
///     also the only place the content exists on a published single-file build - so
///     <see cref="Exists"/> and <see cref="ReadFile"/> can both stay synchronous.
///
///     A path is tried as a manifest resource first (exact <c>LogicalName</c> match), then as a
///     pack URI when <see cref="Application.Current"/> is available.
/// </remarks>
public sealed class WpfAssetFileSystem(string assemblyName) : IFileSystem, ISyncFileSystem
{
    private readonly string _assemblyName = assemblyName;

    public byte[]? ReadFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var normalized = Normalize(filePath);

        return ReadManifestResource(normalized) ?? ReadPackResource(normalized);
    }

    public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

    public bool Exists([NotNullWhen(true)] string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var normalized = Normalize(filePath);

        return FindManifestResourceName(normalized) is not null || PackExists(normalized);
    }

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

    private byte[]? ReadPackResource(string normalizedPath)
    {
        if (Application.Current is null)
            return null;

        try
        {
            var info = Application.GetResourceStream(ResolvePackUri(normalizedPath));
            if (info?.Stream is null)
                return null;

            using var stream = info.Stream;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            return buffer.ToArray();
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private bool PackExists(string normalizedPath)
    {
        if (Application.Current is null)
            return false;

        try
        {
            var info = Application.GetResourceStream(ResolvePackUri(normalizedPath));
            if (info?.Stream is null)
                return false;

            info.Stream.Dispose();

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private Uri ResolvePackUri(string normalizedPath)
        => new($"pack://application:,,,/{_assemblyName};component/{normalizedPath}", UriKind.Absolute);

    private Assembly? ResolveAssembly()
        => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == _assemblyName)
            ?? TryLoad(_assemblyName);

    private static Assembly? TryLoad(string name)
    {
        try
        {
            return Assembly.Load(new AssemblyName(name));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Normalize(string filePath) => filePath.Replace('\\', '/').TrimStart('/');
}
