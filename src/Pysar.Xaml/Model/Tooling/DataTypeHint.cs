using System;

namespace Pysar.Xaml.Model.Tooling;

/// <summary>Reads the data-type hint off a XAML object node. The canonical form is the MAUI-style
/// directive <c>x:DataType="reports:Invoice"</c>; the XAML designer idiom
/// <c>d:DataContext="{d:DesignInstance Type=reports:Invoice}"</c> remains supported as an
/// alternative spelling for reports that already use it. Whichever form supplies the type is the
/// source of truth for build-time binding validation, and the IDE plugins mirror this precedence
/// when they type binding paths.</summary>
internal static class DataTypeHint
{
    private const string DataContextName = "DataContext";
    private const string DesignInstanceName = "DesignInstance";
    private const string TypeArgument = "Type";
    private const string DataTypeDirectiveName = "DataType";

    /// <summary>Returns the declared type name (e.g. <c>"reports:Invoice"</c>): null when absent,
    /// empty string when explicitly cleared, otherwise the declared type name.</summary>
    public static string? Read(XamlObjectNode node)
        => FindSource(node) switch
        {
            null => null,
            { Kind: XamlMemberKind.Directive, Value: XamlLiteralNode literal } => literal.Text,
            var designContext => ReadDesignInstanceType(designContext),
        };

    /// <summary>The member that supplies the data type, so a caller reporting on the hint (a
    /// diagnostic span, say) points at the same attribute <see cref="Read"/> took the value from.
    /// The <c>x:DataType</c> directive wins, but only when it carries a literal type name: any other
    /// value — <c>{x:Type …}</c>, which this dialect does not support — is treated as absent and
    /// falls through to the designer idiom.</summary>
    public static XamlMemberNode? FindSource(XamlObjectNode node)
        => FindDataTypeDirective(node) is { Value: XamlLiteralNode } directive
            ? directive
            : FindDesignDataContext(node);

    /// <summary>The <c>d:DataContext</c> member: an attribute in the designer namespace, used when
    /// no <c>x:DataType</c> directive is present.</summary>
    public static XamlMemberNode? FindDesignDataContext(XamlObjectNode node)
    {
        foreach (var member in node.Members)
            if (member.Name.LocalName == DataContextName
                && member.Name.NamespaceName == XamlNamespaces.Designer)
                return member;
        return null;
    }

    /// <summary>The MAUI-style <c>x:DataType</c> directive — the canonical data-type hint, accepted
    /// from either XAML language namespace.</summary>
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
