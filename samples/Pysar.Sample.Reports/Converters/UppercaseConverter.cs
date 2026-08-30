using Pysar.Binding;

namespace Pysar.Sample.Reports.Converters;

/// <summary>Renders the bound value in upper case. Demonstrates XAML <c>Converter={StaticResource ...}</c>.</summary>
public sealed class UppercaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter)
        => value?.ToString()?.ToUpperInvariant();
}
