using Pysar.Core.Abstractions;

namespace Pysar.Skia;

/// <summary>
///     Resolves report assets from a directory on disk - the handler for hosts without a UI framework
///     of their own: console and worker applications, server-side rendering, and the design-time preview.
/// </summary>
/// <remarks>
    ///     Applications built on a UI framework pass that framework's <see cref="IFileSystem"/>
    ///     (package assets, avares, pack URIs) instead of reading from disk.
/// </remarks>
public sealed class DefaultReportPlatformHandler : IReportPlatformHandler
{
    /// <summary>Reads assets from the application's deployment directory.</summary>
    public DefaultReportPlatformHandler() : this(new LocalFileSystem()) { }

    /// <summary>Reads assets from <paramref name="rootDirectory"/>.</summary>
    public DefaultReportPlatformHandler(string rootDirectory) : this(new LocalFileSystem(rootDirectory)) { }

    /// <summary>Reads assets through <paramref name="fileSystem"/>.</summary>
    public DefaultReportPlatformHandler(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        FileSystem = fileSystem;
        FontCollection = new SkiaFontCollection(fileSystem);
    }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}
