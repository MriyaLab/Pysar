using System.Reflection;
using Pysar.Core.Abstractions;
using Pysar.Skia;
using IFileSystem = Pysar.Core.Abstractions.IFileSystem;

namespace Pysar.Uno;

/// <summary>Resolves report assets from the application's own packaged resources.</summary>
public sealed class UnoReportPlatformHandler : IReportPlatformHandler
{
    public UnoReportPlatformHandler(Assembly assetAssembly)
    {
        ArgumentNullException.ThrowIfNull(assetAssembly);

        Assets = new UnoAssetFileSystem(assetAssembly);
        FontCollection = new SkiaFontCollection(Assets);
    }

    /// <summary>The same object as <see cref="FileSystem"/>, typed so preloading is reachable.</summary>
    public UnoAssetFileSystem Assets { get; }

    public IFileSystem FileSystem => Assets;

    public IFontCollection FontCollection { get; }
}
