using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Pysar.Xaml.Model;

namespace Pysar.Xaml.SourceGen;

/// <summary>
///     Checks the literal values of attributes that name an enum member.
/// </summary>
/// <remarks>
///     <para>
///     A literal attribute value is not turned into C# by the generator; it is emitted as a
///     <c>ValueConverter.Convert</c> call and parsed while the report is being built, and for an
///     enum property that ends in <c>Enum.Parse</c>. So a misspelled value compiled cleanly and
///     threw at render time - the most expensive moment to find out. The name is already known to be
///     wrong at build time, which is where this reports it.
///     </para>
///     <para>
///     Only enums. Sizes, colours and the rest have converters whose accepted forms are not a closed
///     set the generator can check, and guessing at them would reject values the runtime takes.
///     </para>
/// </remarks>
internal static class AttributeValueValidator
{
    private static readonly DiagnosticDescriptor InvalidEnumValue = new(
        "PQX012",
        "XAML attribute value",
        "Value '{0}' is not a member of enum '{1}'",
        "PysarXaml",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static void Validate(
        XamlDocumentNode document,
        Compilation compilation,
        string filePath,
        string xamlText,
        SourceProductionContext context)
        => ValidateNode(document.Root, compilation, filePath, SourceText.From(xamlText), context);

    private static void ValidateNode(
        XamlObjectNode node,
        Compilation compilation,
        string filePath,
        SourceText sourceText,
        SourceProductionContext context)
    {
        var type = XamlCodeModel.ResolveTypeSymbol(compilation, node.Type.NamespaceName, node.Type.LocalName);

        foreach (var member in node.Members)
        {
            if (member.Kind != XamlMemberKind.Attribute
                || member.Value is not XamlLiteralNode literal
                || XamlNamespaces.IsIgnorable(member.Name.NamespaceName))
            {
                continue;
            }

            if (EnumTypeOf(type, member.Name.LocalName) is not { } enumType)
                continue;

            if (IsMember(enumType, literal.Text))
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                InvalidEnumValue,
                ValueLocation(filePath, sourceText, member, literal),
                literal.Text,
                enumType.Name));
        }

        foreach (var child in node.Children)
            ValidateNode(child, compilation, filePath, sourceText, context);

        foreach (var member in node.Members)
        foreach (var contained in member.Objects)
            ValidateNode(contained, compilation, filePath, sourceText, context);
    }

    /// <summary>The enum behind a property, unwrapping <c>Nullable&lt;T&gt;</c>, or null when the
    /// property is absent or not enum-typed.</summary>
    private static INamedTypeSymbol? EnumTypeOf(INamedTypeSymbol? owner, string propertyName)
    {
        for (var current = owner; current is not null; current = current.BaseType)
        foreach (var member in current.GetMembers(propertyName))
        {
            if (member is not IPropertySymbol property)
                continue;

            var type = property.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                ? nullable.TypeArguments[0]
                : property.Type;

            return type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType ? enumType : null;
        }

        return null;
    }

    /// <summary>
    ///     Whether the runtime would parse this value. Mirrors <c>Enum.Parse(type, text, true)</c>:
    ///     case-insensitive, a comma-separated list for a flags enum, and a plain number for any of
    ///     them. Being laxer than the runtime is the safe direction - the point is to catch a
    ///     misspelling, not to second-guess the parser.
    /// </summary>
    private static bool IsMember(INamedTypeSymbol enumType, string text)
    {
        var names = enumType.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name).ToArray();

        foreach (var part in text.Split(','))
        {
            var name = part.Trim();
            if (name.Length == 0)
                return true; // Trailing or doubled comma: malformed, but not a misspelled name.

            if (long.TryParse(name, out _))
                continue;

            if (!names.Any(candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    /// <summary>
    ///     The value itself, not the whole attribute: the name is what is wrong, and pointing at the
    ///     attribute would underline text the message never mentions. Located by finding the literal
    ///     inside its own line, since the parsed span addresses the attribute.
    /// </summary>
    private static Location ValueLocation(
        string filePath,
        SourceText sourceText,
        XamlMemberNode member,
        XamlLiteralNode literal)
    {
        var lineIndex = member.Span.Line - 1;
        if (lineIndex < 0 || lineIndex >= sourceText.Lines.Count || literal.Text.Length == 0)
            return Location.None;

        var line = sourceText.Lines[lineIndex];
        var from = Math.Min(line.Start + Math.Max(0, member.Span.Column - 1), line.End);
        var found = sourceText.ToString(TextSpan.FromBounds(from, line.End))
            .IndexOf(literal.Text, StringComparison.Ordinal);

        if (found < 0)
            return Location.None;

        var textSpan = new TextSpan(from + found, literal.Text.Length);
        return Location.Create(filePath, textSpan, sourceText.Lines.GetLinePositionSpan(textSpan));
    }
}
