using Pysar.Core.Abstractions;
using Pysar.Skia;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;

namespace Pysar.Wpf;

/// <summary>Resolves report assets from the application's WPF resources.</summary>
public sealed class WpfReportPlatformHandler : IReportPlatformHandler
{
    public WpfReportPlatformHandler(string assemblyName)
    {
        FileSystem = new WpfAssetFileSystem(assemblyName);
        FontCollection = new SkiaFontCollection(FileSystem);
    }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}
