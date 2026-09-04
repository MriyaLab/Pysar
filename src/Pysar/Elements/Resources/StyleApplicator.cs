using System.Globalization;
using System.Reflection;
using Pysar.Binding;
using Pysar.Core;

namespace Pysar.Elements;

public static class StyleApplicator
{
    public static void Apply(object target, Style style)
        => Apply(target, style, ValuePrecedence.ExplicitStyle);

    /// <summary>
    ///     Applies <paramref name="style"/>'s setters at <paramref name="precedence"/>, skipping any member
    ///     already written by something of higher precedence - a local assignment, or a style that outranks
    ///     this one. A malformed style still throws even when every setter is skipped, so the member is
    ///     validated before precedence is consulted.
    /// </summary>
    public static void Apply(object target, Style style, ValuePrecedence precedence)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(style);

        if (style.TargetType is not null && !style.TargetType.IsInstanceOfType(target))
        {
            throw new InvalidOperationException(
                $"Style TargetType '{style.TargetType.Name}' is not compatible with target '{target.GetType().Name}'.");
        }

        var bindable = target as BindableObject;
        using var scope = bindable?.PushWritePrecedence(precedence);

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

            if (bindable is not null && !bindable.CanApplyValue(setter.Member, precedence))
                continue;

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
