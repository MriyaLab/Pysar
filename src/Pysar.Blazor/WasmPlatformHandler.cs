using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Skia;

namespace Pysar.Blazor;

/// <summary>Installs an in-memory file system as the one the report pipeline resolves assets through.</summary>
public static class WasmPlatformHandler
{
    public static DefaultReportPlatformHandler Install(IFileSystem fileSystem)
    {
        var handler = new DefaultReportPlatformHandler(fileSystem);
        ReportPlatformHandler.Create(handler);
        return handler;
    }
}
