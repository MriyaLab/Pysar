namespace Pysar.Elements;

/// <summary>
/// Describes Pysar binding syntax to CLR-based XAML language services.
/// Runtime XAML loading continues to use the shared semantic parser.
/// </summary>
public sealed class BindingExtension : System.Windows.Markup.MarkupExtension
{
    public BindingExtension() { }

    public BindingExtension(string path) => Path = path;

    public string Path { get; set; } = string.Empty;
    public string? StringFormat { get; set; }

    /// <summary>An <c>IValueConverter</c> resource, typically supplied via <c>Converter={StaticResource ...}</c>.</summary>
    public object? Converter { get; set; }

    public object? ConverterParameter { get; set; }

    /// <summary>The binding's explicit source, typically <c>Source={x:Reference root}</c>.</summary>
    public object? Source { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => this;
}
