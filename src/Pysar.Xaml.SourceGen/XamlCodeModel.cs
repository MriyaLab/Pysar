using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Pysar.Xaml.Model;

namespace Pysar.Xaml.SourceGen;

internal sealed class XamlCodeModel
{
    private const string PalmyNs = "https://mriyalab.com/pysar";
    private const string ElementsClr = "Pysar.Elements";

    private XamlCodeModel(
        string ns,
        string className,
        string baseType,
        List<(string Name, string Type)> names,
        string xaml,
        string? baseDirectory,
        Compilation compilation,
        XamlObjectNode rootElement,
        bool requiresRuntimeLoader,
        bool isView)
    {
        IsView = isView;
        Namespace = ns;
        ClassName = className;
        BaseType = baseType;
        Names = names;
        Xaml = xaml;
        BaseDirectory = baseDirectory;
        Compilation = compilation;
        RootElement = rootElement;
        RequiresRuntimeLoader = requiresRuntimeLoader;
    }

    public string Namespace { get; }
    public string ClassName { get; }
    public string BaseType { get; }
    public List<(string Name, string Type)> Names { get; }
    public string Xaml { get; }
    public string? BaseDirectory { get; }
    public Compilation Compilation { get; }
    public XamlObjectNode RootElement { get; }
    public bool RequiresRuntimeLoader { get; }

    /// <summary>True when the document root is a &lt;ReportView&gt; component rather than a &lt;Report&gt;.</summary>
    public bool IsView { get; }

    public static XamlCodeModel? Parse(
        string xaml,
        Compilation compilation,
        string path,
        SourceProductionContext context)
    {
        XamlDocumentNode document;
        try
        {
            document = new XamlParser().Parse(xaml);
        }
        catch (Exception exception)
        {
            Report(context, "PQX001", $"Invalid XAML: {exception.Message}");
            return null;
        }

        var root = document.Root;
        var xClass = GetDirective(root, "Class");
        var legacyCodeBehind = GetLiteralAttribute(root, "CodeBehind");
        var generatedClass = xClass ?? legacyCodeBehind;

        // Validate bindings for every report, including runtime-only reports without x:Class
        // (which return early below and never reach code generation). The generated class type, when
        // it is declared in this compilation, is the scope for Source={x:Reference} bindings.
        BindingValidator.Validate(
            document, compilation, path, xaml, context,
            generatedClass is null ? null : compilation.GetTypeByMetadataName(generatedClass),
            ResolveTypeSymbol(compilation, root.Type.NamespaceName, root.Type.LocalName));

        if (xClass is not null && legacyCodeBehind is not null)
        {
            Report(
                context,
                "PQX004",
                "Use either x:Class or the legacy Report.CodeBehind attribute, not both.");
            return null;
        }

        if (generatedClass is null)
            return null;

        // Generated code always calls into Pysar.Xaml (ReportXaml.LoadInto, and
        // XamlValueConverter for converted literals), but this generator ships as an analyzer with
        // its dependencies suppressed, so nothing pulls that assembly in on the consumer's behalf.
        // Undiagnosed, the omission arrives as CS0234 inside code the author never wrote.
        if (compilation.GetTypeByMetadataName("Pysar.Xaml.ReportXaml") is null)
        {
            Report(
                context,
                "PQX007",
                "Generated report code requires a reference to Pysar.Xaml. Add the "
                + "Pysar.Xaml package or project reference.");
            return null;
        }

        if (xClass is null)
            Report(
                context,
                "PQX005",
                "Report.CodeBehind is retained for compatibility. Use the standard x:Class directive.",
                DiagnosticSeverity.Warning);

        var (ns, className) = SplitClass(generatedClass);
        var baseType = ResolveType(compilation, root.Type.NamespaceName, root.Type.LocalName);
        if (baseType is null)
        {
            Report(context, "PQX002", $"Cannot resolve root element <{root.Type.LocalName}>.");
            return null;
        }

        var rootLocalName = root.Type.LocalName;
        if (rootLocalName is not ("Report" or "ReportView"))
        {
            Report(
                context,
                "PQX006",
                $"x:Class is allowed only on <Report> or <ReportView>; got <{rootLocalName}>.");
            return null;
        }

        var names = new List<(string Name, string Type)>();
        foreach (var node in DescendantsAndSelf(root))
        {
            var name = GetDirective(node, "Name");
            if (name is null)
                continue;

            var type = ResolveType(compilation, node.Type.NamespaceName, node.Type.LocalName);
            if (type is null)
            {
                Report(
                    context,
                    "PQX003",
                    $"Cannot resolve type for x:Name='{name}' (<{node.Type.LocalName}>).");
                continue;
            }

            names.Add((name, type));
        }

        return new XamlCodeModel(
            ns,
            className,
            baseType,
            names,
            xaml,
            Path.GetDirectoryName(Path.GetFullPath(path)),
            compilation,
            root,
            DescendantsAndSelf(root).Any(RequiresRuntimeFallback),
            rootLocalName == "ReportView");
    }

    internal string? ResolveTypeFqn(string xmlNamespace, string localName)
        => ResolveType(Compilation, xmlNamespace, localName);

    public string Emit()
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine($"namespace {Namespace}");
        builder.AppendLine("{");
        builder.AppendLine($"    partial class {ClassName} : {BaseType}");
        builder.AppendLine("    {");
        foreach (var (name, type) in Names)
            builder.AppendLine($"        private {type} {name} = default!;");
        builder.AppendLine("        private void InitializeComponent()");
        builder.AppendLine("        {");
        // Only a root report is repopulated from disk by the preview host (ReportXaml.LoadFileInto),
        // so building it here as well would be wasted work. A component is built solely by this
        // method - nothing else populates it - so guarding it would render an empty view.
        if (!IsView)
            builder.AppendLine("            if (global::Pysar.Core.ReportDesignMode.IsEnabled) return;");
        if (RequiresRuntimeLoader)
            EmitRuntimeBody(builder);
        else
            builder.Append(EmitConstructionOrFallBack());
        builder.AppendLine("        }");
        EmitNameBinder(builder);
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    /// <summary>
    ///     Emits the compiled construction body, falling back to the runtime loader for anything the
    ///     emitter cannot express. <see cref="RequiresRuntimeFallback"/> catches the constructs known
    ///     to need it up front; this catches the rest. Throwing instead would surface as CS8785 with
    ///     no file or line, and would take down every other report in the compilation with it.
    /// </summary>
    private string EmitConstructionOrFallBack()
    {
        try
        {
            return new XamlConstructionEmitter(Compilation, ResolveTypeFqn).EmitBody(RootElement, IsView);
        }
        catch (XamlGenException)
        {
            var builder = new StringBuilder();
            EmitRuntimeBody(builder);
            return builder.ToString();
        }
    }

    private void EmitRuntimeBody(StringBuilder builder)
    {
        var baseDirectory = BaseDirectory;
        if (string.IsNullOrWhiteSpace(baseDirectory))
            builder.AppendLine(
                $"            var __names = global::Pysar.Xaml.ReportXaml.LoadInto(this, {Literal(Xaml)}).Names;");
        else
            builder.AppendLine(
                $"            var __names = global::Pysar.Xaml.ReportXaml.LoadInto(this, {Literal(Xaml)}, {Literal(baseDirectory!)}).Names;");
        builder.AppendLine("            __BindNames(__names);");
    }

    /// <summary>
    ///     Emits the x:Name field binder. The design-time preview host repopulates a root report from the
    ///     on-disk markup instead of running <c>InitializeComponent</c>, so without this entry point every
    ///     generated field stays null there and any code-behind touching one fails.
    /// </summary>
    private void EmitNameBinder(StringBuilder builder)
    {
        builder.AppendLine();
        builder.AppendLine(
            "        internal void __BindNames("
            + "global::System.Collections.Generic.IReadOnlyDictionary<string, "
            + "global::Pysar.Core.Abstractions.IReportObject> names)");
        builder.AppendLine("        {");
        foreach (var (name, type) in Names)
        {
            // A field whose element is missing from the markup stays null rather than throwing: the
            // markup can legitimately be ahead of the last build, which the host reports separately.
            builder.AppendLine(
                $"            if (names.TryGetValue(\"{name}\", out var __{name}))");
            builder.AppendLine($"                {name} = ({type})__{name};");
        }
        builder.AppendLine("        }");
    }

    private static bool RequiresRuntimeFallback(XamlObjectNode node)
    {
        foreach (var member in node.Members)
        {
            if (member.Kind == XamlMemberKind.PropertyElement
                && (member.Name.LocalName == "Resources" || member.Name.LocalName == "Triggers"))
                return true;
            if (member.Kind == XamlMemberKind.Attribute && member.Name.LocalName == "Style")
                return true;
            if (member.Value is XamlStaticResourceNode)
                return true;
            // Converters are resolved from resources at load time; defer such bindings to the runtime loader.
            if (member.Value is XamlBindingNode { ConverterKey: not null })
                return true;
        }

        return false;
    }

    private static IEnumerable<XamlObjectNode> DescendantsAndSelf(XamlObjectNode root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var descendant in DescendantsAndSelf(child))
                yield return descendant;
        foreach (var member in root.Members)
            foreach (var value in member.Objects)
                foreach (var descendant in DescendantsAndSelf(value))
                    yield return descendant;
    }

    private static string? GetDirective(XamlObjectNode node, string name)
        => node.Members.FirstOrDefault(member =>
               member.Kind == XamlMemberKind.Directive
               && XamlNamespaces.IsXaml(member.Name.NamespaceName)
               && member.Name.LocalName == name)?.Value
           is XamlLiteralNode literal
            ? literal.Text
            : null;

    private static string? GetLiteralAttribute(XamlObjectNode node, string name)
        => node.Members.FirstOrDefault(member =>
               member.Kind == XamlMemberKind.Attribute
               && member.Name.LocalName == name)?.Value
           is XamlLiteralNode literal
            ? literal.Text
            : null;

    private static bool IsPalmy(string xmlNamespace)
        => xmlNamespace == PalmyNs || string.IsNullOrEmpty(xmlNamespace);

    private static string? ResolveType(
        Compilation compilation,
        string xmlNamespace,
        string localName)
        => ResolveTypeSymbol(compilation, xmlNamespace, localName)
            ?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static INamedTypeSymbol? ResolveTypeSymbol(
        Compilation compilation,
        string xmlNamespace,
        string localName)
    {
        string clrNamespace;
        if (IsPalmy(xmlNamespace))
            clrNamespace = ElementsClr;
        else if (xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
            clrNamespace = xmlNamespace.Substring("clr-namespace:".Length).Split(';')[0];
        else
            return null;

        return compilation.GetTypeByMetadataName($"{clrNamespace}.{localName}");
    }

    private static (string Namespace, string ClassName) SplitClass(string xClass)
    {
        var separator = xClass.LastIndexOf('.');
        return separator < 0
            ? ("", xClass)
            : (xClass.Substring(0, separator), xClass.Substring(separator + 1));
    }

    private static string Literal(string value)
        => SymbolDisplay.FormatLiteral(value, quote: true);

    private static void Report(
        SourceProductionContext context,
        string id,
        string message,
        DiagnosticSeverity severity = DiagnosticSeverity.Error)
        => context.ReportDiagnostic(
            Diagnostic.Create(
                new DiagnosticDescriptor(
                    id,
                    "XAML code-behind",
                    message,
                    "PysarXaml",
                    severity,
                    isEnabledByDefault: true),
                Location.None));
}
