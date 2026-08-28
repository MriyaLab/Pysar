using Pysar.Binding;

namespace Pysar.Elements;

public class UriImageSource : ImageSource
{
    private static readonly HttpClient _httpClient = new();

    public static BindableProperty UriProperty { get; } =
        BindableProperty.Create(nameof(Uri), typeof(Uri), typeof(UriImageSource), null);

    public UriImageSource()
    {
    }

    public UriImageSource(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Uri = uri;
    }

    public Uri? Uri
    {
        get => (Uri?)GetValue(UriProperty);
        set => SetValue(UriProperty, value);
    }

    public override async Task<byte[]?> LoadAsync(CancellationToken ct = default)
    {
        if (Uri is null)
            return null;

        var response = await _httpClient.GetAsync(Uri, ct);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadAsByteArrayAsync(ct);
        return null;
    }
}
