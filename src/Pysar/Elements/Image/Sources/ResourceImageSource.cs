using Pysar.Binding;
using Pysar.Core;

namespace Pysar.Elements;

public class ResourceImageSource : ImageSource
{
    public static BindableProperty ResourceNameProperty { get; } =
        BindableProperty.Create(nameof(ResourceName), typeof(string), typeof(ResourceImageSource), string.Empty);

    public ResourceImageSource()
    {
    }

    public ResourceImageSource(string resourceName)
    {
        ArgumentNullException.ThrowIfNull(resourceName);
        ResourceName = resourceName;
    }

    public string ResourceName
    {
        get => (string)GetValue(ResourceNameProperty)!;
        set => SetValue(ResourceNameProperty, value);
    }

    public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ResourceName) || !ReportPlatformHandler.FileSystem.Exists(ResourceName))
            return Task.FromResult<byte[]?>(null);

        return ReportPlatformHandler.FileSystem.ReadFileAsync(ResourceName);
    }
}
