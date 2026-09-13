using Pysar.Core.Abstractions;

namespace Pysar.Skia;

/// <summary>
///     Resolves report assets from a directory on disk - the handler for hosts without a UI framework
///     of their own: console and worker applications, server-side rendering, and the design-time preview.
/// </summary>
/// <remarks>
///     Applications built on a UI framework install that framework's handler instead
///     (<c>AvaloniaReportPlatformHandler</c>, <c>WpfReportPlatformHandler</c>,
///     <c>MauiReportPlatformHandler</c>, <c>WasmPlatformHandler</c>), because their assets are served
///     from the application package rather than from the file system.
/// </remarks>
public sealed class DefaultReportPlatformHandler : IReportPlatformHandler
{
    /// <summary>Reads assets from the application's deployment directory.</summary>
    public DefaultReportPlatformHandler() : this(new LocalFileSystem()) { }

    /// <summary>Reads assets from <paramref name="rootDirectory"/>.</summary>
    public DefaultReportPlatformHandler(string rootDirectory) : this(new LocalFileSystem(rootDirectory)) { }

    private DefaultReportPlatformHandler(LocalFileSystem fileSystem)
    {
        FileSystem = fileSystem;
        FontCollection = new SkiaFontCollection(fileSystem);
    }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}
