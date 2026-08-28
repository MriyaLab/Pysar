using System.Diagnostics.CodeAnalysis;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;
using ISyncFileSystem = Pysar.Core.Abstractions.ISyncFileSystem;

namespace Pysar.Maui;

/// <summary>
///     Reads report assets - fonts, images - from the application package, in place. Paths are the
///     ones the report author wrote ("Images/logo.svg"), which are also the <c>LogicalName</c> of the
///     corresponding <c>MauiAsset</c> items.
/// </summary>
/// <remarks>
///     Nothing is extracted to disk: every supported platform can reach its package content
///     synchronously, which is what <see cref="IFileSystem.Exists"/> needs.
/// </remarks>
public sealed partial class AppPackageFileSystem : IFileSystem, ISyncFileSystem
{
    public byte[]? ReadFile(string filePath)
        => string.IsNullOrEmpty(filePath) ? null : ReadPackageFile(Normalize(filePath));

    public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

    public bool Exists([NotNullWhen(true)] string? filePath)
        => !string.IsNullOrEmpty(filePath) && PackageFileExists(Normalize(filePath));

    /// <summary>
    ///     Package paths are always forward-slashed and relative, whatever separator the report used.
    /// </summary>
    private static string Normalize(string filePath) => filePath.Replace('\\', '/').TrimStart('/');

    private static partial byte[]? ReadPackageFile(string filePath);

    private static partial bool PackageFileExists(string filePath);
}
