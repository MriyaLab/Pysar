using System.Globalization;
using System.Reflection;
using Pysar.Core;

namespace Pysar.Elements;

public static class StyleApplicator
{
    public static void Apply(object target, Style style)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(style);

        if (style.TargetType is not null && !style.TargetType.IsInstanceOfType(target))
        {
            throw new InvalidOperationException(
                $"Style TargetType '{style.TargetType.Name}' is not compatible with target '{target.GetType().Name}'.");
        }

        foreach (var setter in style.Setters)
        {
            if (string.IsNullOrWhiteSpace(setter.Member) || setter.Value is null)
                continue;

            var property = target.GetType().GetProperty(setter.Member, BindingFlags.Public | BindingFlags.Instance)
                           ?? throw new InvalidOperationException(
                               $"No property '{setter.Member}' on {target.GetType().Name}.");

            if (!property.CanWrite)
            {
                throw new InvalidOperationException(
                    $"Property '{setter.Member}' on {target.GetType().Name} is not writable.");
            }

            property.SetValue(target, ResolveValue(setter.Value, property.PropertyType));
        }
    }

    private static object? ResolveValue(object raw, Type propertyType)
    {
        if (propertyType.IsInstanceOfType(raw))
            return raw;

        if (raw is not string text)
            return Convert.ChangeType(raw, Nullable.GetUnderlyingType(propertyType) ?? propertyType, CultureInfo.InvariantCulture);

        return ValueConverter.Convert(text, propertyType);
    }
}
