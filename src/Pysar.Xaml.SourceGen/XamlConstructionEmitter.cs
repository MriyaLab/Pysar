using System.Text;
using Microsoft.CodeAnalysis;
using Pysar.Xaml.Model;

namespace Pysar.Xaml.SourceGen;

/// <summary>Emits InitializeComponent construction code from the shared XAML model.</summary>
internal sealed class XamlConstructionEmitter
{
    private const string Converter = "global::Pysar.Xaml.XamlValueConverter";

    private readonly Compilation _compilation;
    private readonly Func<string, string, string?> _resolveType;
    private readonly StringBuilder _builder = new();
    private readonly List<string> _deferredBindings = new();
    private int _localIndex;

    public XamlConstructionEmitter(
        Compilation compilation,
        Func<string, string, string?> resolveType)
    {
        _compilation = compilation;
        _resolveType = resolveType;
    }

    /// <summary>Emits the body for the document root: a report band layout or a view's child list.</summary>
    public string EmitBody(XamlObjectNode root, bool isView)
        => isView ? EmitViewBody(root) : EmitReportBody(root);

    private string EmitViewBody(XamlObjectNode view)
    {
        // The root's own x:Name field must point at the instance being constructed; a component's
        // internal bindings reference it as their source.
        if (GetDirective(view, "Name") is { } rootName)
            _builder.AppendLine($"            this.{rootName} = this;");

        foreach (var child in view.Children)
        {
            var (local, _) = EmitElement(child);
            EmitAttachedProperties(child, local);
            _builder.AppendLine($"            this.AddElement({local});");
        }

        FlushDeferredBindings();
        return _builder.ToString();
    }

    private string EmitReportBody(XamlObjectNode report)
    {
        // Same as ReportView: root x:Name must point at the instance so deferred
        // Source={x:Reference Root} bindings (page numbers) resolve against this report.
        if (GetDirective(report, "Name") is { } rootName)
            _builder.AppendLine($"            this.{rootName} = this;");

        foreach (var child in report.Children)
        {
            var (local, fullyQualifiedName) = EmitElement(child);
            if (fullyQualifiedName.EndsWith(".PageFormat", StringComparison.Ordinal))
                _builder.AppendLine($"            this.PageFormat = {local};");
            else if (fullyQualifiedName.EndsWith(".Metadata", StringComparison.Ordinal))
                _builder.AppendLine($"            this.Metadata = {local};");
            else
                _builder.AppendLine($"            this.Bands.Set({local});");
        }

        FlushDeferredBindings();
        return _builder.ToString();
    }

    /// <summary>Appends the bindings held back until the whole tree exists. Locals declared earlier in
    /// InitializeComponent's body are still in scope, so these lines may reference any element local.</summary>
    private void FlushDeferredBindings()
    {
        foreach (var line in _deferredBindings)
            _builder.AppendLine(line);
    }

    private (string Local, string FullyQualifiedName) EmitElement(XamlObjectNode node)
    {
        var fullyQualifiedName = ResolveFullyQualifiedName(node)
                                 ?? throw new XamlGenException(
                                     $"Cannot resolve <{node.Type.LocalName}>.");
        var symbol = _compilation.GetTypeByMetadataName(StripGlobalPrefix(fullyQualifiedName));
        var local = NextLocal();
        _builder.AppendLine($"            var {local} = new {fullyQualifiedName}();");

        EmitAttributes(node, symbol, fullyQualifiedName, local);
        if (GetDirective(node, "Name") is { } name)
            _builder.AppendLine($"            this.{name} = {local};");

        if (node.Children.Count > 0 && IsContainer(symbol))
        {
            foreach (var child in node.Children)
            {
                var (childLocal, _) = EmitElement(child);
                EmitAttachedProperties(child, childLocal);
                _builder.AppendLine($"            {local}.AddElement({childLocal});");
            }
        }
        else if (ContentProperty(symbol) is { } contentProperty
                 && !string.IsNullOrWhiteSpace(node.TextContent))
        {
            _builder.AppendLine(
                $"            {local}.{contentProperty} = \"{Escape(node.TextContent!)}\";");
        }

        foreach (var member in node.Members.Where(member =>
                     member.Kind == XamlMemberKind.PropertyElement
                     && member.Name.OwnerName == node.Type.LocalName))
            EmitPropertyElement(member, symbol, fullyQualifiedName, local);

        return (local, fullyQualifiedName);
    }

    private void EmitAttributes(
        XamlObjectNode node,
        INamedTypeSymbol? symbol,
        string fullyQualifiedName,
        string local)
    {
        foreach (var member in node.Members.Where(member =>
                     member.Kind == XamlMemberKind.Attribute
                     && member.Name.LocalName != "Style"
                     // Design-time markup (d:, mc:) is for IDE language services only.
                     && !XamlNamespaces.IsIgnorable(member.Name.NamespaceName)))
        {
            var name = member.Name.LocalName;
            switch (member.Value)
            {
                case XamlBindingNode binding:
                    var bindableProperty = $"{fullyQualifiedName}.{name}Property";
                    if (binding.SourceName is not null)
                    {
                        // Deferred: the named source may be declared later in the document, and the
                        // root's own field is assigned at the very start of construction.
                        _deferredBindings.Add(
                            $"            {local}.SetBinding({bindableProperty}, new global::Pysar.Binding.BindingInfo(\"{Escape(binding.Path)}\", stringFormat: {StringLiteral(binding.StringFormat)}, source: this.{binding.SourceName}));");
                        break;
                    }

                    _builder.AppendLine(binding.StringFormat is null
                        ? $"            {local}.SetBinding({bindableProperty}, \"{Escape(binding.Path)}\");"
                        : $"            {local}.SetBinding({bindableProperty}, \"{Escape(binding.Path)}\", \"{Escape(binding.StringFormat)}\");");
                    break;
                case XamlLiteralNode literal:
                    var propertyType = PropertyTypeFqn(symbol, name)
                                       ?? throw new XamlGenException(
                                           $"No property '{name}' on {fullyQualifiedName}.");
                    _builder.AppendLine(
                        $"            {local}.{name} = ({propertyType}){Converter}.Convert(\"{Escape(literal.Text)}\", typeof({propertyType}))!;");
                    break;
                default:
                    throw new XamlGenException(
                        $"Member '{name}' requires the runtime XAML loader.");
            }
        }
    }

    private void EmitPropertyElement(
        XamlMemberNode member,
        INamedTypeSymbol? ownerSymbol,
        string ownerType,
        string ownerLocal)
    {
        var property = PropertySymbol(ownerSymbol, member.Name.LocalName)
                       ?? throw new XamlGenException(
                           $"No property '{member.Name.LocalName}' on {ownerType}.");

        var propertyType = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // A collection property is filled, not assigned - matching XamlObjectFactory.FillCollection.
        // Assigning here would drop every child but the first.
        if (IsCollection(property.Type))
        {
            EmitCollectionPropertyElement(member, propertyType, ownerLocal);
            return;
        }

        if (member.Objects.Count == 1 && !IsShorthand(member.Objects[0], member))
        {
            var (valueLocal, _) = EmitElement(member.Objects[0]);
            _builder.AppendLine(
                $"            {ownerLocal}.{member.Name.LocalName} = {valueLocal};");
            return;
        }

        var valueLocalName = NextLocal();
        _builder.AppendLine($"            var {valueLocalName} = new {propertyType}();");
        if (member.Objects.Count == 1)
        {
            var shorthand = member.Objects[0];
            var propertySymbol = _compilation.GetTypeByMetadataName(StripGlobalPrefix(propertyType));
            EmitAttributes(shorthand, propertySymbol, propertyType, valueLocalName);
        }
        _builder.AppendLine(
            $"            {ownerLocal}.{member.Name.LocalName} = {valueLocalName};");
    }

    /// <summary>
    ///     Builds each child and adds it to the property's existing collection, which the element
    ///     already initialises. Mirrors the runtime loader's FillCollection so both paths agree.
    /// </summary>
    private void EmitCollectionPropertyElement(XamlMemberNode member, string propertyType, string ownerLocal)
    {
        var listLocal = NextLocal();
        _builder.AppendLine(
            $"            var {listLocal} = {ownerLocal}.{member.Name.LocalName} ?? new {propertyType}();");

        foreach (var itemNode in member.Objects)
        {
            var (itemLocal, _) = EmitElement(itemNode);
            _builder.AppendLine($"            {listLocal}.Add({itemLocal});");
        }

        _builder.AppendLine($"            {ownerLocal}.{member.Name.LocalName} = {listLocal};");
    }

    private static bool IsCollection(ITypeSymbol type)
        => type.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IList");

    private IPropertySymbol? PropertySymbol(INamedTypeSymbol? type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var member in current.GetMembers(name))
                if (member is IPropertySymbol property)
                    return property;
        return null;
    }

    private void EmitAttachedProperties(XamlObjectNode node, string childLocal)
    {
        foreach (var member in node.Members.Where(member =>
                     member.Kind == XamlMemberKind.AttachedProperty
                     && member.Name.OwnerName == "Grid"))
        {
            var setter = member.Name.LocalName switch
            {
                "Row" => "SetRow",
                "Column" => "SetColumn",
                "RowSpan" => "SetRowSpan",
                "ColumnSpan" => "SetColumnSpan",
                _ => null
            };
            if (setter is not null && member.Value is XamlLiteralNode literal)
                _builder.AppendLine(
                    $"            global::Pysar.Elements.GridAttached.{setter}({childLocal}, {literal.Text});");
        }
    }

    private string NextLocal() => $"__e{_localIndex++}";

    private string? ResolveFullyQualifiedName(XamlObjectNode node)
        => _resolveType(node.Type.NamespaceName, node.Type.LocalName);

    private static string StripGlobalPrefix(string fullyQualifiedName)
        => fullyQualifiedName.StartsWith("global::", StringComparison.Ordinal)
            ? fullyQualifiedName.Substring("global::".Length)
            : fullyQualifiedName;

    private string? PropertyTypeFqn(INamedTypeSymbol? type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var member in current.GetMembers(name))
                if (member is IPropertySymbol property)
                    return property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return null;
    }

    private static bool IsContainer(INamedTypeSymbol? type)
        => type is not null && type.AllInterfaces.Any(item => item.Name == "IReportContainer");

    private static string? ContentProperty(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var attribute in current.GetAttributes())
                if (attribute.AttributeClass?.Name == "ContentPropertyAttribute"
                    && attribute.ConstructorArguments.Length == 1)
                    return attribute.ConstructorArguments[0].Value as string;
        return null;
    }

    private static string? GetDirective(XamlObjectNode node, string name)
        => node.Members.FirstOrDefault(member =>
               member.Kind == XamlMemberKind.Directive
               && XamlNamespaces.IsXaml(member.Name.NamespaceName)
               && member.Name.LocalName == name)?.Value
           is XamlLiteralNode literal
            ? literal.Text
            : null;

    private static bool IsShorthand(XamlObjectNode node, XamlMemberNode member)
        => node.Type.LocalName == $"{member.Name.OwnerName}.{member.Name.LocalName}";

    /// <summary>Renders a nullable string as a C# literal, or <c>null</c>.</summary>
    private static string StringLiteral(string? value)
        => value is null ? "null" : $"\"{Escape(value)}\"";

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

internal sealed class XamlGenException : Exception
{
    public XamlGenException(string message) : base(message)
    {
    }
}
