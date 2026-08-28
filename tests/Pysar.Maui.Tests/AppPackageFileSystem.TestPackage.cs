namespace Pysar.Maui;

/// <summary>
///     The package implementation the tests run against: the output directory laid out the way an
///     application package is, with assets under the relative paths a report asks for.
/// </summary>
/// <remarks>
///     This is the same seam the real platforms fill - <c>AppPackageFileSystem.Android.cs</c> reads
///     an <c>AssetManager</c>, the Apple one an app bundle - so what is exercised through it is the
///     shared half: the path a report writes turning into the path the package is asked for, and a
///     missing asset being reported rather than thrown.
/// </remarks>
public sealed partial class AppPackageFileSystem
{
    private static string ResolvePackagePath(string filePath)
        => Path.Combine(AppContext.BaseDirectory, filePath.Replace('/', Path.DirectorySeparatorChar));

    private static partial byte[]? ReadPackageFile(string filePath)
    {
        var path = ResolvePackagePath(filePath);

        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static partial bool PackageFileExists(string filePath) => File.Exists(ResolvePackagePath(filePath));
}
