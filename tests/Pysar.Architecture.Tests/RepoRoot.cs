namespace Pysar.Architecture.Tests;

/// <summary>
///     These tests assert on the repository's own files, not on compiled output, so they need the
///     source tree rather than the test binary's directory.
/// </summary>
internal static class RepoRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, "Pysar.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Pysar.sln not found above {AppContext.BaseDirectory}.");
    }
}
