namespace Pysar.Binding;

public class BindingExpression
{
    public string Path { get; set; } = string.Empty;
    public IValueConverter? Converter { get; set; }
    public object? ConverterParameter { get; set; }
    public string? StringFormat { get; set; }
}