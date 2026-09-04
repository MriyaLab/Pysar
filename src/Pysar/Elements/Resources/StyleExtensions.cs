using Pysar.Binding;
using Pysar.Elements.Base;

namespace Pysar.Elements;

public static class StyleExtensions
{
    public static T WithStyle<T>(this T element, ResourceDictionary resources, string key)
        where T : ReportObject
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (resources[key] is not Style style)
            throw new InvalidOperationException($"Resource '{key}' is not a Style.");

        element.Style = style;
        if (TryGetImplicitStyle(resources, element.GetType(), out var implicitStyle))
            StyleApplicator.Apply(element, implicitStyle, ValuePrecedence.ImplicitStyle);
        StyleApplicator.Apply(element, style, ValuePrecedence.ExplicitStyle);
        return element;
    }

    private static bool TryGetImplicitStyle(ResourceDictionary resources, Type type, out Style style)
    {
        if (resources.ContainsKey(type) && resources[type] is Style found)
        {
            style = found;
            return true;
        }

        style = null!;
        return false;
    }
}
