// WINDOWS is the symbol the .NET SDK defines for a -windows target framework; there is no
// __WINDOWS__ counterpart to the legacy __ANDROID__ / __IOS__ symbols the platform SDKs define.
#if WINDOWS
namespace Pysar.Maui;

public sealed partial class AppPackageFileSystem
{
    private static readonly Lazy<string> InstallRoot = new(ResolveInstallRoot);

    private static partial byte[]? ReadPackageFile(string filePath)
    {
        var fullPath = Path.Combine(InstallRoot.Value, filePath);

        return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
    }

    private static partial bool PackageFileExists(string filePath)
        => File.Exists(Path.Combine(InstallRoot.Value, filePath));

    private static string ResolveInstallRoot()
    {
        try
        {
            return Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
        }
        catch (InvalidOperationException)
        {
            // Unpackaged (WindowsPackageType=None): assets sit next to the executable.
            return AppContext.BaseDirectory;
        }
    }
}
#endif