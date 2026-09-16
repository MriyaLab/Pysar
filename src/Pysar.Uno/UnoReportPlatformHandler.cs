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

        // The application's own assets first, then those embedded by referenced report libraries.
        FileSystem = new FallbackFileSystem(Assets, new EmbeddedAssetFileSystem());
        FontCollection = new DefaultReportPlatformHandler(FileSystem).FontCollection;
    }

    /// <summary>The application's own assets, typed so preloading is reachable.</summary>
    public UnoAssetFileSystem Assets { get; }

    public IFileSystem FileSystem { get; }

    public IFontCollection FontCollection { get; }
}
