using Pysar.Core.Abstractions;

namespace Pysar.Skia;

/// <summary>
///     Resolves report assets from a directory on disk - the handler for hosts without a UI framework
///     of their own: console and worker applications, server-side rendering, and the design-time preview.
/// </summary>
/// <remarks>
///     Applications built on a UI framework pass that framework's <see cref="IFileSystem"/>
///     (package assets, avares, pack URIs) instead of reading from disk. The disk-backed
///     constructors fall back to the report assets embedded in referenced libraries - see
///     <see cref="EmbeddedAssetFileSystem"/> - so an asset a library shipped is reachable even
///     when the host has not deployed a copy of its own next to the binary.
/// </remarks>
public sealed class DefaultReportPlatformHandler : IReportPlatformHandler
{
    /// <summary>
    ///     Reads assets from the application's deployment directory, then from the report assets
    ///     embedded in referenced libraries.
    /// </summary>
    public DefaultReportPlatformHandler() : this(Chain(new LocalFileSystem())) { }

    /// <summary>
    ///     Reads assets from <paramref name="rootDirectory"/>, then from the report assets embedded
    ///     in referenced libraries.
    /// </summary>
    public DefaultReportPlatformHandler(string rootDirectory) : this(Chain(new LocalFileSystem(rootDirectory))) { }

    /// <summary>Reads assets through <paramref name="fileSystem"/>.</summary>
    public DefaultReportPlatformHandler(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        FileSystem = fileSystem;
        FontCollection = new SkiaFontCollection(fileSystem);
    }

    /// <summary>
    ///     Puts a disk directory ahead of the embedded assets, so a file next to the binary replaces
    ///     the copy a referenced library shipped. Applied to the disk-backed constructors only: an
    ///     explicitly supplied <see cref="IFileSystem"/> is the caller's whole answer, and the hosts
    ///     that pass one chain it themselves.
    /// </summary>
    private static IFileSystem Chain(LocalFileSystem local)
        => new FallbackFileSystem(local, new EmbeddedAssetFileSystem());

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}
