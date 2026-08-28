using Pysar.Binding;

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
        if (string.IsNullOrEmpty(ResourceName))
            return Task.FromResult<byte[]?>(null);

        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        using var stream = assembly?.GetManifestResourceStream(ResourceName);
        if (stream == null)
            return Task.FromResult<byte[]?>(null);

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Task.FromResult<byte[]?>(ms.ToArray());
    }
}
