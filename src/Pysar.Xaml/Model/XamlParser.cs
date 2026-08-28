using System.Xml;
using System.Xml.Linq;

namespace Pysar.Xaml.Model;

internal sealed class XamlParser
{
    public XamlDocumentNode Parse(string xaml)
    {
        if (xaml is null)
            throw new ArgumentNullException(nameof(xaml));

        using var reader = new StringReader(xaml);
        return Parse(reader);
    }

    public XamlDocumentNode Parse(TextReader xaml)
    {
        if (xaml is null)
            throw new ArgumentNullException(nameof(xaml));

        return ParseDocument(XDocument.Load(xaml, LoadOptions.SetLineInfo));
    }

    public XamlDocumentNode Parse(Stream xaml)
    {
        if (xaml is null)
            throw new ArgumentNullException(nameof(xaml));

        return ParseDocument(XDocument.Load(xaml, LoadOptions.SetLineInfo));
    }

    private static XamlDocumentNode ParseDocument(XDocument document)
    {
        var root = document.Root ?? throw new XmlException("Empty XAML document.");
        var namespaces = root.Attributes()
            .Where(attribute => attribute.IsNamespaceDeclaration)
            .Select(attribute => new XamlNamespaceDeclaration(
                attribute.Name.LocalName == "xmlns" ? null : attribute.Name.LocalName,
                attribute.Value,
                GetSpan(attribute)))
            .ToArray();

        return new XamlDocumentNode(ParseObject(root), namespaces);
    }

    private static XamlObjectNode ParseObject(XElement element)
    {
        // Design-time members (d:, mc:) are kept in the model — tooling reads
        // d:DataContext="{d:DesignInstance Type=…}" as the binding data type — but every consumer
        // that materialises objects skips them (see XamlNamespaces.IsIgnorable).
        var members = new List<XamlMemberNode>();
        foreach (var attribute in element.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration))
            members.Add(ParseAttribute(attribute));

        var children = new List<XamlObjectNode>();
        foreach (var child in element.Elements())
        {
            if (XamlNamespaces.IsIgnorable(child.Name.NamespaceName))
                continue;
            if (TrySplitMemberName(child.Name.LocalName, out var ownerName, out var memberName))
                members.Add(ParsePropertyElement(child, ownerName, memberName));
            else
                children.Add(ParseObject(child));
        }

        var text = element.Nodes()
            .OfType<XText>()
            .Select(node => node.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?.Trim();

        return new XamlObjectNode(
            new XamlTypeName(element.Name.NamespaceName, element.Name.LocalName),
            members,
            children,
            text,
            GetSpan(element));
    }

    private static XamlMemberNode ParseAttribute(XAttribute attribute)
    {
        var isAttached = TrySplitMemberName(attribute.Name.LocalName, out var ownerName, out var memberName);
        var isDirective = XamlNamespaces.IsXaml(attribute.Name.NamespaceName);
        var kind = isDirective
            ? XamlMemberKind.Directive
            : isAttached
                ? XamlMemberKind.AttachedProperty
                : XamlMemberKind.Attribute;
        var name = new XamlMemberName(
            attribute.Name.NamespaceName,
            isAttached ? ownerName : null,
            isAttached ? memberName : attribute.Name.LocalName);
        var span = GetSpan(attribute);

        return new XamlMemberNode(
            name,
            kind,
            MarkupExtensionParser.Parse(attribute.Value, span),
            Array.Empty<XamlObjectNode>(),
            span);
    }

    private static XamlMemberNode ParsePropertyElement(
        XElement element,
        string ownerName,
        string memberName)
    {
        var hasValueAttributes = element.Attributes().Any(attribute => !attribute.IsNamespaceDeclaration);
        var objects = hasValueAttributes
            ? new[] { ParseObject(element) }
            : element.Elements().Select(ParseObject).ToArray();
        var text = element.Nodes()
            .OfType<XText>()
            .Select(node => node.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?.Trim();
        var span = GetSpan(element);

        return new XamlMemberNode(
            new XamlMemberName(element.Name.NamespaceName, ownerName, memberName),
            XamlMemberKind.PropertyElement,
            text is null ? null : MarkupExtensionParser.Parse(text, span),
            objects,
            span);
    }

    private static bool TrySplitMemberName(
        string localName,
        out string ownerName,
        out string memberName)
    {
        var separator = localName.IndexOf('.');
        if (separator <= 0 || separator == localName.Length - 1)
        {
            ownerName = string.Empty;
            memberName = localName;
            return false;
        }

        ownerName = localName.Substring(0, separator);
        memberName = localName.Substring(separator + 1);
        return true;
    }

    private static XamlSourceSpan GetSpan(XObject value)
    {
        var lineInfo = (IXmlLineInfo)value;
        return lineInfo.HasLineInfo()
            ? new XamlSourceSpan(lineInfo.LineNumber, lineInfo.LinePosition)
            : new XamlSourceSpan(0, 0);
    }
}
