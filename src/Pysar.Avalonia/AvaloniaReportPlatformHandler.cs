using Pysar.Core.Abstractions;
using Pysar.Skia;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;

namespace Pysar.Avalonia;

/// <summary>Resolves report assets from the application's Avalonia resources.</summary>
public sealed class AvaloniaReportPlatformHandler : IReportPlatformHandler
{
    public AvaloniaReportPlatformHandler(string assemblyName)
    {
        FileSystem = new AvaloniaAssetFileSystem(assemblyName);
        FontCollection = new SkiaFontCollection(FileSystem);
    }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}
