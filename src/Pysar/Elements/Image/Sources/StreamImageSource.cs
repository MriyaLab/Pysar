using Pysar.Binding;

namespace Pysar.Elements;

public class StreamImageSource : ImageSource
{
    public static BindableProperty StreamProviderProperty { get; } =
        BindableProperty.Create(nameof(StreamProvider), typeof(Func<Stream>), typeof(StreamImageSource), null);

    public StreamImageSource()
    {
    }

    public StreamImageSource(Func<Stream> streamProvider)
    {
        ArgumentNullException.ThrowIfNull(streamProvider);
        StreamProvider = streamProvider;
    }

    public Func<Stream>? StreamProvider
    {
        get => (Func<Stream>?)GetValue(StreamProviderProperty);
        set => SetValue(StreamProviderProperty, value);
    }

    public override Task<byte[]?> LoadAsync(CancellationToken ct = default)
    {
        if (StreamProvider is null)
            return Task.FromResult<byte[]?>(null);

        try
        {
            using var stream = StreamProvider();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Task.FromResult<byte[]?>(ms.ToArray());
        }
        catch
        {
            return Task.FromResult<byte[]?>(null);
        }
    }
}
