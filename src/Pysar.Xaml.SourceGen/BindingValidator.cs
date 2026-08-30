using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Pysar.Xaml.Model;
using Pysar.Xaml.Model.Tooling;

namespace Pysar.Xaml.SourceGen;

/// <summary>
/// Validates <c>{Binding}</c> paths against the effective <c>DataType</c> at build time,
/// reporting <c>PQX010</c> for a path segment that does not name a member.
/// </summary>
/// <remarks>
/// The effective data type is inherited down the tree; an empty hint clears it. Crossing into the
/// content children of an element that carries a <c>DataSource</c> (a per-item template boundary,
/// e.g. <c>Repeater</c>/<c>DetailBand</c>) narrows the type to the collection's element type.
/// <para>
/// Whenever the scope cannot be determined the validator reports nothing rather than guessing: an
/// unresolved data type, a collection whose element type is unknown, and everything inside a
/// <c>Style</c> or <c>ResourceDictionary</c> (reused across scopes the declaration cannot see).
/// Silence is the safe failure mode here, since <c>PQX010</c> breaks the build.
/// </para>
/// <para>
/// An explicit <c>Source={x:Reference root}</c> is different: it reads the component root's own
/// properties, so it resolves against the <c>x:Class</c> type <i>or</i> the root element's type. Both
/// are needed because the <c>x:Class</c> partial declares no base type — the generator supplies it in
/// the other partial, which does not exist yet while this validation runs — so the class symbol alone
/// cannot see members it inherits, such as <c>Report.PageNumber</c>.
/// </para>
/// </remarks>
internal static class BindingValidator
{
    private static readonly DiagnosticDescriptor InvalidBindingPath = new(
        "PQX010",
        "XAML binding",
        "Binding path '{0}' does not resolve against DataType: member '{1}' was not found",
        "PysarXaml",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <remarks>
    ///     An error rather than a warning, which it used to be. The reasoning for a warning was that
    ///     the type might exist in an assembly this configuration cannot see - but the cost of being
    ///     lenient is that a typo in the type name switches binding validation off for the whole
    ///     scope beneath it and says so only in passing. That is the one mistake that costs the most
    ///     and shows the least. XAML treats an unresolved <c>x:DataType</c> as an error, and the IDE
    ///     plugins mirror this severity: a warning here and a red squiggle there would be worse than
    ///     either on its own.
    /// </remarks>
    private static readonly DiagnosticDescriptor UnresolvedDataType = new(
        "PQX011",
        "XAML binding",
        "DataType type '{0}' could not be resolved; bindings in this scope are not validated",
        "PysarXaml",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly char[] UnsupportedPathChars = { '[', ']', '(', ')', '/' };
    private static readonly char[] Quotes = { '"', '\'' };

    public static void Validate(
        XamlDocumentNode document,
        Compilation compilation,
        string filePath,
        string xamlText,
        SourceProductionContext context,
        object? rootClassType = null,
        object? rootElementType = null)
    {
        var namespaces = BuildNamespaceMap(document.Namespaces);
        var provider = new RoslynTypeMemberProvider(compilation);
        var resolver = new BindingPathResolver(provider);
        var sourceText = SourceText.From(xamlText);

        ValidateNode(
            document.Root, inheritedType: null, namespaces, provider, resolver, filePath, sourceText,
            context, rootClassType, rootElementType);
    }

    private static void ValidateNode(
        XamlObjectNode node,
        object? inheritedType,
        IReadOnlyDictionary<string, string> namespaces,
        RoslynTypeMemberProvider provider,
        BindingPathResolver resolver,
        string filePath,
        SourceText sourceText,
        SourceProductionContext context,
        object? rootClassType,
        object? rootElementType)
    {
        // A style is applied to elements in scopes its declaration knows nothing about, and the rest
        // of a resource dictionary is just as context-free, so nothing inside them can be validated.
        if (node.Type.LocalName is "Style" or "ResourceDictionary")
            return;

        var nodeType = inheritedType;
        var declared = DataTypeHint.Read(node);
        if (declared is not null)
        {
            if (declared.Length == 0)
            {
                nodeType = null;
            }
            else
            {
                nodeType = ResolveDataType(declared, provider, namespaces);
                if (nodeType is null)
                    context.ReportDiagnostic(Diagnostic.Create(
                        UnresolvedDataType,
                        CreateLocation(filePath, sourceText, DataTypeSpan(node)),
                        declared));
            }
        }

        foreach (var member in node.Members)
        {
            // Source={x:Reference …} reads the component's own properties, so it validates against the
            // x:Class type and is independent of the inherited DataType (which is usually null here).
            if (member.Value is XamlBindingNode { SourceName: not null } sourceBinding)
            {
                // Resolves against the x:Class type or the root element type it derives from — see the
                // comment in Validate for why neither alone is enough.
                if (rootClassType is not null || rootElementType is not null)
                    ValidateSourcePath(
                        sourceBinding.Path, sourceBinding.Span, rootClassType, rootElementType,
                        resolver, filePath, sourceText, context);
                continue;
            }

            // A DataContext binding replaces the node's own context, so the path itself is still
            // evaluated in the outer scope. Everything else — including DataSource, whose item scope
            // only applies to the children — resolves against this node's own type.
            var scope = IsContextMember(member) ? inheritedType : nodeType;
            if (scope is null)
                continue;

            switch (member.Value)
            {
                case XamlBindingNode binding:
                    ValidatePath(binding.Path, binding.Span, scope, resolver, filePath, sourceText, context);
                    break;
                // DataTrigger.Binding is a plain property path rather than a markup extension, but the
                // runtime resolves it against the data context exactly like a binding.
                case XamlLiteralNode literal when IsTriggerPath(node, member):
                    ValidatePath(literal.Text, member.Span, scope, resolver, filePath, sourceText, context);
                    break;
            }
        }

        // Content children (the repeated row template of a DataSource) bind against the item type;
        // property-element objects (e.g. DetailHeader/DetailFooter chrome) stay in the outer context.
        var (contentType, propertyType) = ChildContextTypes(node, nodeType, declared is not null, resolver);
        foreach (var child in node.Children)
            ValidateNode(child, contentType, namespaces, provider, resolver, filePath, sourceText, context, rootClassType, rootElementType);
        foreach (var member in node.Members)
            foreach (var contained in member.Objects)
                ValidateNode(contained, propertyType, namespaces, provider, resolver, filePath, sourceText, context, rootClassType, rootElementType);
    }

    /// <summary>
    ///     Validates an explicit-source path against the two types that together describe the component
    ///     root, reporting only when it resolves against neither. The failure is attributed to the
    ///     x:Class type when there is one, since that is the type the author writes members on.
    /// </summary>
    private static void ValidateSourcePath(
        string path,
        XamlSourceSpan span,
        object? rootClassType,
        object? rootElementType,
        BindingPathResolver resolver,
        string filePath,
        SourceText sourceText,
        SourceProductionContext context)
    {
        if (rootElementType is not null && Resolves(path, rootElementType, resolver))
            return;

        ValidatePath(path, span, rootClassType ?? rootElementType!, resolver, filePath, sourceText, context);
    }

    /// <summary>True when <paramref name="path"/> resolves against <paramref name="dataType"/>.</summary>
    private static bool Resolves(string path, object dataType, BindingPathResolver resolver)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(UnsupportedPathChars) >= 0)
            return true; // Not validated at all; treat as resolved so no diagnostic is produced.

        return resolver.TryResolvePath(dataType, path, out _);
    }

    private static void ValidatePath(
        string path,
        XamlSourceSpan span,
        object dataType,
        BindingPathResolver resolver,
        string filePath,
        SourceText sourceText,
        SourceProductionContext context)
    {
        if (string.IsNullOrWhiteSpace(path))
            return; // Empty path binds to the data context itself.
        if (path.IndexOfAny(UnsupportedPathChars) >= 0)
            return; // Indexers / attached-property paths are not validated.

        if (resolver.TryResolvePath(dataType, path, out var failingSegment))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            InvalidBindingPath,
            CreateLocation(filePath, sourceText, span),
            path,
            failingSegment));
    }

    /// <summary>True for <c>DataTrigger.Binding</c>, whose literal value is a property path.</summary>
    private static bool IsTriggerPath(XamlObjectNode node, XamlMemberNode member)
        => node.Type.LocalName == "DataTrigger"
           && member.Name.LocalName == "Binding"
           && string.IsNullOrEmpty(member.Name.NamespaceName);

    private static object? ResolveDataType(
        string value,
        RoslynTypeMemberProvider provider,
        IReadOnlyDictionary<string, string> namespaces)
    {
        var separator = value.IndexOf(':');
        var prefix = separator < 0 ? null : value.Substring(0, separator);
        var typeName = separator < 0 ? value : value.Substring(separator + 1);
        return provider.ResolveType(prefix, typeName, namespaces);
    }

    /// <summary>Computes the data types in effect for a node's children, distinguishing content
    /// children from property-element objects. A <c>DataSource</c> makes content children a per-item
    /// template scope (the collection's element type) while property elements (DetailHeader/Footer
    /// chrome) stay in the outer context. A <c>DataContext="{Binding path}"</c> re-targets both to
    /// the type of <c>path</c> — unless the node declared its own DataType, which already describes
    /// the context that DataContext produces and therefore wins. Context clears (<c>null</c>,
    /// skipping validation) when a source type cannot be resolved.</summary>
    private static (object? Content, object? PropertyElement) ChildContextTypes(
        XamlObjectNode node, object? nodeType, bool declaresOwnType, BindingPathResolver resolver)
    {
        var itemsPath = ItemsPath(node);
        if (itemsPath is not null)
        {
            var collectionType = nodeType is null
                ? null
                : resolver.ResolvePathType(nodeType, itemsPath) as ITypeSymbol;
            var itemType = collectionType is null ? null : GetEnumerableElementType(collectionType);
            return (itemType, nodeType);
        }

        // A DataSource that is not a path (a literal, or a collection assigned from code) leaves the
        // item type unknown, so its content children cannot be validated.
        if (FindMember(node, "DataSource") is not null)
            return (null, nodeType);

        var dataContext = declaresOwnType ? null : FindBinding(node, "DataContext");
        if (dataContext is null)
            return (nodeType, nodeType);

        var retargeted = nodeType is null ? null : resolver.ResolvePathType(nodeType, dataContext.Path);
        return (retargeted, retargeted);
    }

    /// <summary>The path to the node's item collection: a <c>DataSource="{Binding path}"</c>, or the
    /// legacy literal <c>DataSourcePath="path"</c>, which the runtime expands per item just the
    /// same (see <c>RepeaterExpander.ResolveItems</c>). Null when the node has neither.</summary>
    private static string? ItemsPath(XamlObjectNode node)
    {
        if (FindBinding(node, "DataSource") is { } dataSource)
            return dataSource.Path;
        return (FindMember(node, "DataSourcePath")?.Value as XamlLiteralNode)?.Text;
    }

    /// <summary>Returns the element type of a collection type (array or anything implementing
    /// <see cref="System.Collections.Generic.IEnumerable{T}"/>), or <c>null</c> if it is not an
    /// enumerable of a single element type.</summary>
    private static ITypeSymbol? GetEnumerableElementType(ITypeSymbol type)
    {
        // A string is IEnumerable<char>, but the runtime expander refuses to iterate one
        // (see RepeaterExpander.AsEnumerable), so it is not a collection of characters here either.
        if (type.SpecialType == SpecialType.System_String)
            return null;

        if (type is IArrayTypeSymbol array)
            return array.ElementType;

        if (IsEnumerableOfT(type, out var self))
            return self;

        foreach (var @interface in type.AllInterfaces)
            if (IsEnumerableOfT(@interface, out var element))
                return element;

        return null;
    }

    private static bool IsEnumerableOfT(ITypeSymbol type, out ITypeSymbol? element)
    {
        if (type is INamedTypeSymbol { IsGenericType: true } named
            && named.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            element = named.TypeArguments[0];
            return true;
        }

        element = null;
        return false;
    }

    /// <summary>The span of the attribute that supplied the data type, so the squiggle lands on the
    /// attribute actually in effect rather than on a hint that was passed over.</summary>
    private static XamlSourceSpan DataTypeSpan(XamlObjectNode node)
        => DataTypeHint.FindSource(node)?.Span ?? node.Span;

    /// <summary>True for the member that replaces the node's own data context: a plain (unprefixed)
    /// <c>DataContext</c>. Its path is evaluated before the new context exists, so it resolves in the
    /// outer scope. <c>DataSource</c> is deliberately excluded — it retargets only the children.</summary>
    private static bool IsContextMember(XamlMemberNode member)
        => member.Name.LocalName == "DataContext"
           && string.IsNullOrEmpty(member.Name.NamespaceName);

    /// <summary>The runtime binding of a plain (unprefixed) member — the designer's
    /// <c>d:DataContext</c> shares a local name but is a design-time hint, not a runtime binding.</summary>
    private static XamlBindingNode? FindBinding(XamlObjectNode node, string memberName)
        => FindMember(node, memberName)?.Value as XamlBindingNode;

    /// <summary>The node's plain (unprefixed) member with this name — the designer's
    /// <c>d:DataContext</c> shares a local name but lives in the design-time namespace.</summary>
    private static XamlMemberNode? FindMember(XamlObjectNode node, string memberName)
    {
        foreach (var member in node.Members)
            if (member.Name.LocalName == memberName && string.IsNullOrEmpty(member.Name.NamespaceName))
                return member;
        return null;
    }

    private static IReadOnlyDictionary<string, string> BuildNamespaceMap(
        IReadOnlyList<XamlNamespaceDeclaration> declarations)
    {
        var map = new Dictionary<string, string>();
        foreach (var declaration in declarations)
            map[declaration.Prefix ?? string.Empty] = declaration.NamespaceName;
        return map;
    }

    private static Location CreateLocation(string filePath, SourceText sourceText, XamlSourceSpan span)
    {
        var lineIndex = span.Line - 1;
        if (lineIndex < 0 || lineIndex >= sourceText.Lines.Count)
            return Location.None;

        var line = sourceText.Lines[lineIndex];
        var start = Math.Min(line.Start + Math.Max(0, span.Column - 1), line.End);
        var textSpan = TextSpan.FromBounds(start, AttributeEnd(sourceText, start, line.End));
        return Location.Create(filePath, textSpan, sourceText.Lines.GetLinePositionSpan(textSpan));
    }

    /// <summary>The end of the attribute starting at <paramref name="start"/>, i.e. just past its
    /// closing quote. <see cref="XamlSourceSpan"/> carries no length, so it is recovered from the
    /// text; anything that is not a single-line quoted attribute falls back to the end of the line.</summary>
    private static int AttributeEnd(SourceText sourceText, int start, int lineEnd)
    {
        var tail = sourceText.ToString(TextSpan.FromBounds(start, lineEnd));
        var openingQuote = tail.IndexOfAny(Quotes);
        if (openingQuote < 0)
            return lineEnd;

        var closingQuote = tail.IndexOf(tail[openingQuote], openingQuote + 1);
        return closingQuote < 0 ? lineEnd : start + closingQuote + 1;
    }
}
