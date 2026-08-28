namespace Pysar.Binding.Converters;

public class StringFormatConverter : Pysar.Binding.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter)
    {
        if (value == null) return null;
        if (parameter is string format)
        {
            return string.Format(format, value);
        }
        return value?.ToString();
    }
}
