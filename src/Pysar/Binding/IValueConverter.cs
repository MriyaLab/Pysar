namespace Pysar.Binding;

public interface IValueConverter
{
    object? Convert(object? value, Type targetType, object? parameter);
}