#if __IOS__ || __MACCATALYST__
namespace Pysar.Maui;

public sealed partial class AppPackageFileSystem
{
    // ResourcePath is the bundle root on iOS and Contents/Resources on Mac Catalyst, which is
    // exactly where MauiAsset content lands on each - so no per-platform branch is needed.
    private static string ResourceRoot => Foundation.NSBundle.MainBundle.ResourcePath ?? AppContext.BaseDirectory;

    private static partial byte[]? ReadPackageFile(string filePath)
    {
        var fullPath = Path.Combine(ResourceRoot, filePath);

        return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
    }

    private static partial bool PackageFileExists(string filePath)
        => File.Exists(Path.Combine(ResourceRoot, filePath));
}
#endif
