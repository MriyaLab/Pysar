using System;

namespace Pysar.Xaml.Model.Tooling;

/// <summary>Reads the design-time data-type hint off a XAML object node:
/// <c>d:DataContext="{d:DesignInstance Type=reports:Invoice}"</c>. This is the standard XAML
/// designer idiom, which IDE XAML language services resolve natively (binding-path typing,
/// navigation and completion), so it doubles as the source of truth for build-time binding
/// validation. MAUI's <c>x:DataType</c> directive is honoured as an interop fallback.</summary>
internal static class DataTypeHint
{
    private const string DataContextName = "DataContext";
    private const string DesignInstanceName = "DesignInstance";
    private const string TypeArgument = "Type";
    private const string DataTypeDirectiveName = "DataType";

    /// <summary>Returns the declared type name (e.g. <c>"reports:Invoice"</c>): null when absent,
    /// empty string when explicitly cleared, otherwise the declared type name.</summary>
    public static string? Read(XamlObjectNode node)
    {
        var designContext = FindDesignDataContext(node);
        if (designContext is not null)
            return ReadDesignInstanceType(designContext);

        return (FindDataTypeDirective(node)?.Value as XamlLiteralNode)?.Text;
    }

    /// <summary>The <c>d:DataContext</c> member: an attribute in the designer namespace.</summary>
    public static XamlMemberNode? FindDesignDataContext(XamlObjectNode node)
    {
        foreach (var member in node.Members)
            if (member.Name.LocalName == DataContextName
                && member.Name.NamespaceName == XamlNamespaces.Designer)
                return member;
        return null;
    }

    /// <summary>The MAUI <c>x:DataType</c> directive, kept as an interop fallback.</summary>
    public static XamlMemberNode? FindDataTypeDirective(XamlObjectNode node)
    {
        foreach (var member in node.Members)
            if (member.Kind == XamlMemberKind.Directive
                && member.Name.LocalName == DataTypeDirectiveName
                && XamlNamespaces.IsXaml(member.Name.NamespaceName))
                return member;
        return null;
    }

    /// <summary>Extracts the <c>Type=</c> argument of a <c>{d:DesignInstance …}</c> value (the
    /// positional form is also accepted). Returns an empty string for an empty value — an explicit
    /// "no data type" — or null when the value is not a DesignInstance.</summary>
    private static string? ReadDesignInstanceType(XamlMemberNode member)
    {
        var text = member.Value switch
        {
            XamlUnknownMarkupExtensionNode unknown => unknown.Text,
            XamlLiteralNode literal => literal.Text,
            _ => null,
        };

        if (text is null)
            return null;
        if (text.Length == 0)
            return string.Empty;

        var start = text.IndexOf(DesignInstanceName, StringComparison.Ordinal);
        if (start < 0)
            return null;

        var body = text.Substring(start + DesignInstanceName.Length).TrimEnd('}').Trim();
        foreach (var argument in body.Split(','))
        {
            var trimmed = argument.Trim();
            if (trimmed.Length == 0)
                continue;

            var separator = trimmed.IndexOf('=');
            if (separator < 0)
                return trimmed; // Positional form: {d:DesignInstance reports:Invoice}
            if (trimmed.Substring(0, separator).Trim() == TypeArgument)
                return trimmed.Substring(separator + 1).Trim();
        }

        return null;
    }
}
