namespace Pysar.Binding;

public class BindableProperty
{
    public string Name { get; }
    public Type PropertyType { get; }
    public Type OwnerType { get; }
    public object? DefaultValue { get; }
    public Action<BindableObject, object?, object?>? PropertyChanged { get; }

    private BindableProperty(
        string name,
        Type propertyType,
        Type ownerType,
        object? defaultValue,
        Action<BindableObject, object?, object?>? propertyChanged)
    {
        Name = name;
        PropertyType = propertyType;
        OwnerType = ownerType;
        DefaultValue = defaultValue;
        PropertyChanged = propertyChanged;
    }

    public static BindableProperty Create(
        string name,
        Type propertyType,
        Type ownerType,
        object? defaultValue,
        Action<BindableObject, object?, object?>? propertyChanged = null)
    {
        return new BindableProperty(name, propertyType, ownerType, defaultValue, propertyChanged);
    }

    public void OnPropertyChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        PropertyChanged?.Invoke(bindable, oldValue, newValue);
    }
}
