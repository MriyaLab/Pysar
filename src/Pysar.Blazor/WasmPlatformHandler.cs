using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Skia;

namespace Pysar.Blazor;

/// <summary>Installs an in-memory file system as the one the report pipeline resolves assets through.</summary>
public static class WasmPlatformHandler
{
    public static DefaultReportPlatformHandler Install(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        // The preloaded assets first, then whatever referenced report libraries embedded.
        var handler = new DefaultReportPlatformHandler(
            new FallbackFileSystem(fileSystem, new EmbeddedAssetFileSystem()));

        ReportPlatformHandler.Create(handler);
        return handler;
    }
}
