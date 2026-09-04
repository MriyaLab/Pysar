using System.Reflection;
using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Elements;
using Pysar.Xaml.Model;

namespace Pysar.Xaml;

/// <summary>Assigns parsed XAML member values to runtime CLR objects.</summary>
internal sealed class XamlMemberAssigner(XamlLoadContext context)
{
    public void ApplyAttributes(object instance, XamlObjectNode node)
    {
        foreach (var member in node.Members)
        {
            if (member.Kind != XamlMemberKind.Attribute || member.Name.LocalName == "Style")
                continue;
            // Design-time markup (d:, mc:) is for IDE language services only.
            if (XamlNamespaces.IsIgnorable(member.Name.NamespaceName))
                continue;
            if (member.Value is not null)
                AssignValue(instance, member.Name.LocalName, member.Value);
        }
    }

    /// <summary>
    ///     Applies a style's setters at <paramref name="precedence"/>, skipping members already written by
    ///     something that outranks it. Mirrors <c>StyleApplicator</c>, which it cannot delegate to: setter
    ///     values here may still be unparsed markup extensions.
    /// </summary>
    public void ApplyStyle(object instance, Style style, ValuePrecedence precedence)
    {
        var bindable = instance as BindableObject;
        using var scope = bindable?.PushWritePrecedence(precedence);

        foreach (var setter in style.Setters)
        {
            if (bindable is not null && !bindable.CanApplyValue(setter.Member, precedence))
                continue;

            if (setter.Value is string text)
            {
                AssignValue(
                    instance,
                    setter.Member,
                    MarkupExtensionParser.Parse(text, new XamlSourceSpan(0, 0)));
                continue;
            }

            if (setter.Value is not null)
                AssignObject(instance, setter.Member, setter.Value);
        }
    }

    public void AssignObject(object instance, string name, object value)
    {
        var property = GetProperty(instance.GetType(), name);
        property.SetValue(instance, Coerce(value, property.PropertyType));
    }

    public void AssignValue(object instance, string name, XamlValueNode value)
    {
        var property = GetProperty(instance.GetType(), name);
        switch (value)
        {
            case XamlStaticResourceNode resource:
                property.SetValue(instance, Coerce(context.ResolveResource(resource.Key), property.PropertyType));
                return;
            case XamlBindingNode binding:
                AssignBinding(instance, property, binding);
                return;
            case XamlUnknownMarkupExtensionNode unknown:
                throw new XamlException($"Unsupported markup extension: {unknown.Text}");
            case XamlLiteralNode literal:
                property.SetValue(instance, XamlValueConverter.Convert(literal.Text, property.PropertyType));
                return;
            default:
                throw new XamlException($"Unsupported XAML value for '{name}'.");
        }
    }

    public void ApplyAttachedProperties(XamlObjectNode node, IReportElement child)
    {
        foreach (var member in node.Members.Where(member =>
                     member.Kind == XamlMemberKind.AttachedProperty
                     && member.Name.OwnerName == "Grid"))
        {
            if (member.Value is not XamlLiteralNode literal
                || !int.TryParse(literal.Text, out var value))
                throw new XamlException($"Grid.{member.Name.LocalName} requires an integer value.");

            switch (member.Name.LocalName)
            {
                case "Row": GridAttached.SetRow(child, value); break;
                case "Column": GridAttached.SetColumn(child, value); break;
                case "RowSpan": GridAttached.SetRowSpan(child, value); break;
                case "ColumnSpan": GridAttached.SetColumnSpan(child, value); break;
            }
        }
    }

    public PropertyInfo GetProperty(Type type, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new XamlException($"No property '{name}' on {type.Name}.");

    private static object? Coerce(object value, Type target)
        => target.IsInstanceOfType(value) ? value : System.Convert.ChangeType(value, target);

    private void AssignBinding(object instance, PropertyInfo property, XamlBindingNode binding)
    {
        var bindable = instance as IBindableObject
                       ?? throw new XamlException($"{instance.GetType().Name} is not bindable.");
        var bindableProperty = FindBindableProperty(instance.GetType(), property.Name)
                               ?? throw new XamlException(
                                   $"'{property.Name}' is not a BindableProperty; cannot bind.");

        if (binding.SourceName is not null)
        {
            context.DeferSourceBinding(bindable, bindableProperty, binding);
            return;
        }

        bindable.SetBinding(bindableProperty, BuildBindingInfo(binding, source: null));
    }

    /// <summary>Builds the runtime binding from a parsed node; <paramref name="source"/> is set only for source bindings.</summary>
    public BindingInfo BuildBindingInfo(XamlBindingNode binding, object? source)
    {
        if (binding.ConverterKey is null)
            return new BindingInfo(binding.Path, stringFormat: binding.StringFormat, source: source);

        var converter = context.ResolveResource(binding.ConverterKey) as IValueConverter
                        ?? throw new XamlException(
                            $"Resource '{binding.ConverterKey}' used as Converter is not an IValueConverter.");

        return new BindingInfo(
            binding.Path,
            converter,
            binding.StringFormat,
            converterParameter: binding.ConverterParameter,
            source: source);
    }

    private static BindableProperty? FindBindableProperty(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(
                $"{name}Property",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (property?.PropertyType == typeof(BindableProperty))
                return (BindableProperty?)property.GetValue(null);

            var field = current.GetField($"{name}Property", BindingFlags.Public | BindingFlags.Static);
            if (field?.FieldType == typeof(BindableProperty))
                return (BindableProperty?)field.GetValue(null);
        }

        return null;
    }
}
