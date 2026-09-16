using System.Diagnostics.CodeAnalysis;
using Pysar.Core.Abstractions;

namespace Pysar.Skia;

/// <summary>
///     Reads through an ordered chain of file systems, taking the first that has the path.
/// </summary>
/// <remarks>
///     The shape every host uses to put its own asset source ahead of
///     <see cref="EmbeddedAssetFileSystem"/>: a packaged asset wins over an embedded one, so an
///     application can replace an asset its report library shipped without rebuilding that library.
///
///     <see cref="ReadFile"/> steps over a member that is not an <see cref="ISyncFileSystem"/>
///     rather than blocking on its asynchronous read - blocking is a deadlock rather than a stall on
///     the browser's single thread, which is the reason the synchronous interface exists at all.
/// </remarks>
public sealed class FallbackFileSystem : IFileSystem, ISyncFileSystem
{
    private readonly IFileSystem[] _members;

    /// <summary>Reads through <paramref name="members"/> in order, first to last.</summary>
    /// <remarks>
    ///     The array is kept as given, not copied: every caller is one of Pysar's own registration
    ///     methods passing a fresh <c>params</c> array it does not hold on to or mutate afterward, so
    ///     a defensive copy would only spend an allocation guarding against a caller that does not
    ///     exist.
    /// </remarks>
    public FallbackFileSystem(params IFileSystem[] members)
    {
        ArgumentNullException.ThrowIfNull(members);

        if (members.Length == 0)
        {
            throw new ArgumentException(
                "A fallback chain needs at least one file system.", nameof(members));
        }

        foreach (var member in members)
            ArgumentNullException.ThrowIfNull(member, nameof(members));

        _members = members;
    }

    /// <remarks>
    ///     A member that is not an <see cref="ISyncFileSystem"/> is stepped over, not blocked on -
    ///     see the class remarks. In every chain Pysar builds today, every member is one, so this
    ///     never actually skips anything; it only matters if a future platform ever contributes an
    ///     async-only source.
    /// </remarks>
    public byte[]? ReadFile(string filePath)
    {
        foreach (var member in _members)
            if (member is ISyncFileSystem sync && sync.ReadFile(filePath) is { } content)
                return content;

        return null;
    }

    public async Task<byte[]?> ReadFileAsync(string filePath)
    {
        foreach (var member in _members)
            if (await member.ReadFileAsync(filePath).ConfigureAwait(false) is { } content)
                return content;

        return null;
    }

    /// <remarks>
    ///     Checked against every member's own <see cref="IFileSystem.Exists"/>, so it can say yes for
    ///     a path only <see cref="ReadFileAsync"/> - not the synchronous <see cref="ReadFile"/> -
    ///     would actually return, if an async-only member ever joins a chain. That mirrors
    ///     <see cref="EmbeddedAssetFileSystem"/> and every other member here, which is always both;
    ///     narrowing <see cref="Exists"/> to synchronous members would just invent a difference
    ///     between it and <see cref="ReadFileAsync"/> instead of removing one.
    /// </remarks>
    public bool Exists([NotNullWhen(true)] string? filePath)
    {
        if (filePath is null)
            return false;

        foreach (var member in _members)
            if (member.Exists(filePath))
                return true;

        return false;
    }
}
