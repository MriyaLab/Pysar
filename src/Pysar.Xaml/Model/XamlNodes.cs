namespace Pysar.Xaml.Model;

internal sealed class XamlDocumentNode
{
    public XamlDocumentNode(
        XamlObjectNode root,
        IReadOnlyList<XamlNamespaceDeclaration> namespaces)
    {
        Root = root;
        Namespaces = namespaces;
    }

    public XamlObjectNode Root { get; }
    public IReadOnlyList<XamlNamespaceDeclaration> Namespaces { get; }
}

internal sealed class XamlNamespaceDeclaration
{
    public XamlNamespaceDeclaration(string? prefix, string namespaceName, XamlSourceSpan span)
    {
        Prefix = prefix;
        NamespaceName = namespaceName;
        Span = span;
    }

    public string? Prefix { get; }
    public string NamespaceName { get; }
    public XamlSourceSpan Span { get; }
}

internal sealed class XamlObjectNode
{
    public XamlObjectNode(
        XamlTypeName type,
        IReadOnlyList<XamlMemberNode> members,
        IReadOnlyList<XamlObjectNode> children,
        string? textContent,
        XamlSourceSpan span)
    {
        Type = type;
        Members = members;
        Children = children;
        TextContent = textContent;
        Span = span;
    }

    public XamlTypeName Type { get; }
    public IReadOnlyList<XamlMemberNode> Members { get; }
    public IReadOnlyList<XamlObjectNode> Children { get; }
    public string? TextContent { get; }
    public XamlSourceSpan Span { get; }
}

internal enum XamlMemberKind
{
    Attribute,
    AttachedProperty,
    Directive,
    PropertyElement
}

internal sealed class XamlMemberNode
{
    public XamlMemberNode(
        XamlMemberName name,
        XamlMemberKind kind,
        XamlValueNode? value,
        IReadOnlyList<XamlObjectNode> objects,
        XamlSourceSpan span)
    {
        Name = name;
        Kind = kind;
        Value = value;
        Objects = objects;
        Span = span;
    }

    public XamlMemberName Name { get; }
    public XamlMemberKind Kind { get; }
    public XamlValueNode? Value { get; }
    public IReadOnlyList<XamlObjectNode> Objects { get; }
    public XamlSourceSpan Span { get; }
}

internal abstract class XamlValueNode
{
    protected XamlValueNode(XamlSourceSpan span)
    {
        Span = span;
    }

    public XamlSourceSpan Span { get; }
}

internal sealed class XamlLiteralNode : XamlValueNode
{
    public XamlLiteralNode(string text, XamlSourceSpan span) : base(span)
    {
        Text = text;
    }

    public string Text { get; }
}

internal sealed class XamlBindingNode : XamlValueNode
{
    public XamlBindingNode(
        string path,
        string? stringFormat,
        string? converterKey,
        string? converterParameter,
        XamlSourceSpan span,
        string? sourceName = null) : base(span)
    {
        Path = path;
        StringFormat = stringFormat;
        ConverterKey = converterKey;
        ConverterParameter = converterParameter;
        SourceName = sourceName;
    }

    public string Path { get; }
    public string? StringFormat { get; }

    /// <summary>Resource key of the <c>IValueConverter</c> referenced via <c>Converter={StaticResource ...}</c>.</summary>
    public string? ConverterKey { get; }

    public string? ConverterParameter { get; }

    /// <summary>x:Name of the element named by <c>Source={x:Reference name}</c>, if present.</summary>
    public string? SourceName { get; }
}

internal sealed class XamlStaticResourceNode : XamlValueNode
{
    public XamlStaticResourceNode(string key, XamlSourceSpan span) : base(span)
    {
        Key = key;
    }

    public string Key { get; }
}

internal sealed class XamlUnknownMarkupExtensionNode : XamlValueNode
{
    public XamlUnknownMarkupExtensionNode(string text, XamlSourceSpan span) : base(span)
    {
        Text = text;
    }

    public string Text { get; }
}
