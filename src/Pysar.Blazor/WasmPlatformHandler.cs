using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Skia;

namespace Pysar.Blazor;

/// <summary>Resolves report assets from memory, which is all a browser offers.</summary>
public sealed class WasmPlatformHandler : IReportPlatformHandler
{
    public WasmPlatformHandler(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        FileSystem = fileSystem;
        FontCollection = new SkiaFontCollection(fileSystem);
    }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }

    /// <summary>Installs this handler as the one the report pipeline resolves assets through.</summary>
    public static WasmPlatformHandler Install(IFileSystem fileSystem)
    {
        var handler = new WasmPlatformHandler(fileSystem);

        ReportPlatformHandler.Create(handler);

        return handler;
    }
}
