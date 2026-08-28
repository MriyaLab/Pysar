namespace Pysar.Binding;

public class BindingInfo
{
    public string Path { get; }
    public IValueConverter? Converter { get; }
    public object? ConverterParameter { get; }
    public string? StringFormat { get; }
    public bool IsLambda { get; }
    public string? LambdaPath { get; }

    /// <summary>
    /// Explicit binding source. When set, the path resolves against this object instead of the
    /// inherited DataContext — this is what makes a component's markup read the component's own
    /// properties (<c>{Binding Title, Source={x:Reference root}}</c>).
    /// </summary>
    public object? Source { get; }

    public BindingInfo(
        string path,
        IValueConverter? converter = null,
        string? stringFormat = null,
        bool isLambda = false,
        string? lambdaPath = null,
        object? converterParameter = null,
        object? source = null)
    {
        Path = path;
        Converter = converter;
        ConverterParameter = converterParameter;
        StringFormat = stringFormat;
        IsLambda = isLambda;
        LambdaPath = lambdaPath;
        Source = source;
    }

    public BindingInfo WithSource(object? source)
        => new(Path, Converter, StringFormat, IsLambda, LambdaPath, ConverterParameter, source);
}
