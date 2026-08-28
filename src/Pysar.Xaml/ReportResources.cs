using Pysar.Elements;

namespace Pysar.Xaml;

public static class ReportResources
{
    public static ResourceDictionary LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new XamlException($"ResourceDictionary file not found: {fullPath}");

        var directory = Path.GetDirectoryName(fullPath)
                        ?? throw new XamlException($"Cannot resolve directory for '{fullPath}'.");

        using var stream = File.OpenRead(fullPath);
        return new XamlLoader().LoadDictionary(stream, directory);
    }
}
