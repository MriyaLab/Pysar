namespace Pysar.Binding.Converters;

public class DateTimeFormatConverter : Pysar.Binding.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter)
    {
        if (value is DateTime dateTime && parameter is string format)
        {
            return dateTime.ToString(format);
        }
        return value?.ToString();
    }
}
