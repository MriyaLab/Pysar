using Pysar.Binding;
using Pysar.Core;

namespace Pysar.Elements;

public class FileImageSource : ImageSource
{
    public static BindableProperty FilePathProperty { get; } =
        BindableProperty.Create(nameof(FilePath), typeof(string), typeof(FileImageSource), string.Empty);

    public FileImageSource()
    {
    }

    public FileImageSource(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        FilePath = filePath;
    }

    public string FilePath
    {
        get => (string)GetValue(FilePathProperty)!;
        set => SetValue(FilePathProperty, value);
    }

    public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(FilePath) || !ReportPlatformHandler.FileSystem.Exists(FilePath))
            return Task.FromResult<byte[]?>(null);

        return ReportPlatformHandler.FileSystem.ReadFileAsync(FilePath);
    }
}
