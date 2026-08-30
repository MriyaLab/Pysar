using System.Globalization;

namespace Pysar.Maui.Sample.Converters;

/// <summary>Maps a string to <c>true</c> when it carries text, for visibility bindings.</summary>
public sealed class IsNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
