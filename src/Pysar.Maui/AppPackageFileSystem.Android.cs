#if __ANDROID__ 

namespace Pysar.Maui;

public sealed partial class AppPackageFileSystem
{
    // Android.App.Application.Context is valid from the moment the application object is
    // constructed, which happens before CreateMauiApp runs - so registration at startup is safe.
    private static Android.Content.Res.AssetManager? Assets
        => Android.App.Application.Context.Assets;

    private static partial byte[]? ReadPackageFile(string filePath)
    {
        if (Assets is not { } assets)
            return null;

        try
        {
            using var source = assets.Open(filePath);
            using var buffer = new MemoryStream();

            source.CopyTo(buffer);

            return buffer.ToArray();
        }
        catch (Java.IO.FileNotFoundException)
        {
            return null;
        }
    }

    private static partial bool PackageFileExists(string filePath)
    {
        if (Assets is not { } assets)
            return false;

        try
        {
            using var _ = assets.Open(filePath);

            return true;
        }
        catch (Java.IO.FileNotFoundException)
        {
            return false;
        }
    }
}
#endif
