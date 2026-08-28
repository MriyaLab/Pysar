using Pysar.Core.Abstractions;
using Pysar.Skia;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;
// Microsoft.Maui.Hosting declares its own IFontCollection - the one behind ConfigureFonts.
using IFontCollection = Pysar.Core.Abstractions.IFontCollection;

namespace Pysar.Maui;

/// <summary>Resolves report assets from the application package.</summary>
public sealed class MauiReportPlatformHandler : IReportPlatformHandler
{
    public MauiReportPlatformHandler()
    {
        FileSystem = new AppPackageFileSystem();
        FontCollection = new SkiaFontCollection(FileSystem);
    }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}
