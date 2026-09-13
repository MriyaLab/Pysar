using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;

namespace Pysar.Skia;

/// <summary>
///     An <see cref="IFileSystem"/> that reads report assets from a directory on disk - by default the
///     one the application was deployed into, which is where relative asset paths are authored against.
/// </summary>
/// <remarks>
///     A rooted path is read as given; a relative one is resolved against the root directory. Anything
///     that is not an existing file reads as <c>null</c> rather than throwing, so a missing asset
///     degrades the same way it does on the platforms that serve assets from an application package.
/// </remarks>
public sealed class LocalFileSystem : IFileSystem, ISyncFileSystem
{
    private readonly string _rootDirectory;

    /// <summary>Resolves relative paths against the application's deployment directory.</summary>
    public LocalFileSystem() : this(AppContext.BaseDirectory) { }

    /// <summary>Resolves relative paths against <paramref name="rootDirectory"/>.</summary>
    /// <remarks>
    ///     Hosts whose assets do not sit next to the binaries - a web application reading from its
    ///     content root, for instance - pass that directory here.
    /// </remarks>
    public LocalFileSystem(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);

        // AppContext.BaseDirectory is the directory of the managed application, which is what
        // relative asset paths are authored against. Process.MainModule points at the executing
        // binary instead, so it resolves to the shared runtime when the assembly is loaded by a
        // host such as `dotnet app.dll` or the design-time preview - and every image goes missing.
        _rootDirectory = rootDirectory;
    }

    public async Task<byte[]?> ReadFileAsync(string filePath)
    {
        var fullPath = ResolveExistingPath(filePath);
        if (fullPath == null)
            return null;

        return await File.ReadAllBytesAsync(fullPath);
    }

    public byte[]? ReadFile(string filePath)
    {
        var fullPath = ResolveExistingPath(filePath);
        if (fullPath == null)
            return null;

        return File.ReadAllBytes(fullPath);
    }

    public bool Exists([NotNullWhen(true)] string? filePath) => ResolveExistingPath(filePath) != null;

    /// <summary>The absolute path <paramref name="filePath"/> points at, or <c>null</c> when there is no such file.</summary>
    private string? ResolveExistingPath([NotNullWhen(true)] string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        // Path.Combine returns the second argument unchanged when it is rooted, which is how an
        // absolute asset path keeps working.
        var fullPath = Path.Combine(_rootDirectory, filePath);

        return File.Exists(fullPath) ? fullPath : null;
    }
}
