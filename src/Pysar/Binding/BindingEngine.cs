using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;

namespace Pysar.Binding;

public class BindingEngine
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> _publicProperties = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _nestedStoreProperties = new();

    public object? GetValue(BindingInfo bindingInfo, object? dataContext)
        => GetValue(bindingInfo, dataContext, typeof(string));

    public object? GetValue(BindingInfo bindingInfo, object? dataContext, Type targetType)
    {
        if (dataContext == null || string.IsNullOrEmpty(bindingInfo.Path))
            return null;

        var value = ResolvePath(dataContext, bindingInfo.Path);

        if (value == null) return null;

        // Prefer an explicit ConverterParameter; otherwise fall back to StringFormat as the parameter.
        if (bindingInfo.Converter != null)
        {
            var parameter = bindingInfo.ConverterParameter
                            ?? (!string.IsNullOrEmpty(bindingInfo.StringFormat) ? bindingInfo.StringFormat : null);
            value = bindingInfo.Converter.Convert(value, targetType, parameter);
        }
        else if (!string.IsNullOrEmpty(bindingInfo.StringFormat))
        {
            value = string.Format(bindingInfo.StringFormat, value);
        }

        return value;
    }

    internal static object? ResolvePath(object? source, string? path)
    {
        if (source is null || string.IsNullOrEmpty(path))
            return null;

        var current = source;
        foreach (var part in path.Split('.'))
        {
            if (current is null)
                return null;

            if (TryGetDictionaryValue(current, part, out var dictValue))
            {
                current = dictValue;
                continue;
            }

            var property = PublicProperty(current.GetType(), part);
            current = property?.GetValue(current);
        }

        return current;
    }

    private static bool TryGetDictionaryValue(object obj, string key, out object? value)
    {
        // IDictionary<string, object?> — nullable values
        if (obj is IDictionary<string, object?> dictNullable)
        {
            var found = dictNullable.TryGetValue(key, out value);
            return found;
        }
        // IDictionary<string, object> — non-nullable values (e.g. Dictionary<string, object>)
        if (obj is IDictionary<string, object> dict)
        {
            var found = dict.TryGetValue(key, out var v);
            value = v;
            return found;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Walks the element tree and resolves bindings for each element using its own
    /// <see cref="IReportObject.DataContext"/>, falling back to <paramref name="fallbackContext"/>
    /// when the element has no context of its own.
    /// </summary>
    public void ResolveBindings(IEnumerable<IReportElement> elements, object? fallbackContext = null)
    {
        foreach (var element in elements)
        {
            var ctx = element.DataContext ?? fallbackContext;
            // Called unconditionally: a binding carrying an explicit Source resolves even when no
            // DataContext reaches this element.
            ResolveBindings(element, ctx);
            ResolveNestedStores(element, ctx);

            if (element is IReportContainer container && container.Children.Count > 0)
                ResolveBindings(container.Children, ctx);
        }
    }

    private void ResolveNestedStores(object element, object? dataContext)
    {
        foreach (var property in NestedStoresOn(element.GetType()))
        {
            var value = property.GetValue(element);
            if (value is IBindingStore)
                ResolveBindings(value, dataContext);
        }
    }

    public void ResolveBindings(object? element, object? dataContext)
    {
        if (element is not IBindingStore bindingStore) return;

        foreach (var kvp in bindingStore.EnumerateBindings())
        {
            var property = kvp.Key;
            var bindingInfo = kvp.Value;
            var source = bindingInfo.Source ?? dataContext;
            if (source == null) continue;

            var targetPropertyName = property.Name;
            var targetProperty = PublicProperty(element.GetType(), targetPropertyName);
            if (targetProperty == null) continue;

            var targetType = targetProperty.PropertyType;
            var value = GetValue(bindingInfo, source, targetType);

            // Preserve legacy behavior for string targets: null resolves to an empty string.
            if (targetType == typeof(string))
            {
                targetProperty.SetValue(element, value?.ToString() ?? string.Empty);
                continue;
            }

            // For non-string targets a null value would break value types, so keep the default.
            if (value == null) continue;

            targetProperty.SetValue(element, ConvertValue(value, targetType));
        }
    }

    /// <summary>
    /// Coerces a resolved binding value to the target property's type. Handles values that are
    /// already assignable (e.g. <see cref="Color"/>, <see cref="Size"/>), enums, primitives via
    /// <see cref="System.Convert.ChangeType(object, Type)"/>, and hex strings bound to <see cref="Color"/>.
    /// </summary>
    private static object ConvertValue(object value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var valueType = value.GetType();

        if (underlying.IsAssignableFrom(valueType))
            return value;

        if (underlying.IsEnum)
            return value is string enumText
                ? Enum.Parse(underlying, enumText, ignoreCase: true)
                : Enum.ToObject(underlying, value);

        if (underlying == typeof(Color) && value is string hex)
            return Color.FromHex(hex);

        if (value is IConvertible)
            return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);

        return value;
    }

    private static PropertyInfo? PublicProperty(Type type, string name)
        => _publicProperties.GetOrAdd(
            (type, name),
            static key => key.Type.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance));

    private static PropertyInfo[] NestedStoresOn(Type type)
        => _nestedStoreProperties.GetOrAdd(type, static t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property =>
                    property.CanRead
                    && property.GetIndexParameters().Length == 0
                    && !property.PropertyType.IsValueType
                    && property.PropertyType != typeof(string)
                    && !typeof(IReportObject).IsAssignableFrom(property.PropertyType))
                .ToArray());
}
