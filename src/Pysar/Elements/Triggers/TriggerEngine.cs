using System.Globalization;
using System.Reflection;
using Pysar.Binding;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
///     Build-time evaluation of <see cref="DataTrigger"/>s. Walks the element tree the same way the
///     binding engine does — an element's own <see cref="IReportObject.DataContext"/> wins, otherwise the
///     inherited context flows down — so a row template cloned per data item evaluates against that item.
///     Each satisfied trigger applies its setters to the owning element; there is no revert (one-shot render).
/// </summary>
internal static class TriggerEngine
{
    public static void Apply(IEnumerable<IReportElement> elements, object? fallbackContext)
    {
        foreach (var element in elements)
        {
            var ctx = element.DataContext ?? fallbackContext;

            if (element is ReportObject owner && owner.Triggers.Count > 0)
                ApplyOwnerTriggers(owner, ctx);

            if (element is Image { Source: { } source } && source.Triggers.Count > 0)
                ApplyOwnerTriggers(source, ctx);

            if (element is IReportContainer container && container.Children.Count > 0)
                Apply(container.Children, ctx);
        }
    }

    private static void ApplyOwnerTriggers(object owner, object? ctx)
    {
        IList<DataTrigger> triggers = owner switch
        {
            ReportObject reportObject => reportObject.Triggers,
            ImageSource imageSource => imageSource.Triggers,
            _ => Array.Empty<DataTrigger>()
        };

        foreach (var trigger in triggers)
            if (Satisfied(PropertyPathResolver.Resolve(ctx, trigger.Binding), trigger.Value, trigger.CompareType))
                ApplySetters(owner, trigger.Setters);
    }

    /// <summary>Applies each setter to the element by CLR property (works for both BindableProperty-backed
    /// properties and struct facades such as <c>FontColor</c>), coercing the literal to the property type.
    /// Written at <see cref="ValuePrecedence.Trigger"/>, the top of the precedence order: a satisfied
    /// trigger is conditional formatting and outranks both styles and the author's own value.</summary>
    private static void ApplySetters(object element, IEnumerable<Setter> setters)
    {
        using var scope = (element as BindableObject)?.PushWritePrecedence(ValuePrecedence.Trigger);

        foreach (var setter in setters)
        {
            var prop = element.GetType().GetProperty(setter.Member, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null || !prop.CanWrite || setter.Value is null) continue;
            var value = prop.PropertyType.IsInstanceOfType(setter.Value)
                ? setter.Value
                : setter.Value is string text
                    ? ValueConverter.Convert(text, prop.PropertyType)
                    : System.Convert.ChangeType(setter.Value, prop.PropertyType, CultureInfo.InvariantCulture);
            prop.SetValue(element, value);
        }
    }

    private static bool Satisfied(object? actual, string? expected, CompareType compare)
    {
        if (actual is null) return false;

        var typed = Coerce(expected, actual.GetType());
        int? order = actual is IComparable cmp && typed is not null && typed.GetType() == actual.GetType()
            ? cmp.CompareTo(typed)
            : null;

        return compare switch
        {
            CompareType.Equal => typed is not null ? Equals(actual, typed) : StringEquals(actual, expected),
            CompareType.NotEqual => typed is not null ? !Equals(actual, typed) : !StringEquals(actual, expected),
            CompareType.GreaterThan => order is > 0,
            CompareType.GreaterThanOrEqual => order is >= 0,
            CompareType.LessThan => order is < 0,
            CompareType.LessThanOrEqual => order is <= 0,
            _ => false
        };
    }

    private static bool StringEquals(object actual, string? expected)
        => string.Equals(actual.ToString(), expected, StringComparison.Ordinal);

    private static object? Coerce(string? text, Type target)
    {
        if (text is null) return null;
        try { return System.Convert.ChangeType(text, target, CultureInfo.InvariantCulture); }
        catch { return null; }
    }
}
