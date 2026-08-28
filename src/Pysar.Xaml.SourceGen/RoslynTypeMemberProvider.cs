using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Pysar.Xaml.Model.Tooling;

namespace Pysar.Xaml.SourceGen;

/// <summary>
/// Resolves <c>x:DataType</c> type names and enumerates their members via the Roslyn
/// <see cref="Compilation"/> symbol model, adapting them to the shared
/// <see cref="ITypeMemberProvider"/> used by <see cref="BindingPathResolver"/> for
/// build-time binding validation. Type handles are <see cref="ITypeSymbol"/> instances.
/// </summary>
internal sealed class RoslynTypeMemberProvider : ITypeMemberProvider
{
    private readonly Compilation _compilation;

    // Members are enumerated once per type: the generator runs on every keystroke in the IDE, and a
    // report typically walks the same few types across hundreds of binding segments.
    private readonly Dictionary<ISymbol, IReadOnlyList<BindingMember>> _members =
        new(SymbolEqualityComparer.Default);

    public RoslynTypeMemberProvider(Compilation compilation) => _compilation = compilation;

    public object? ResolveType(string? prefix, string typeName, IReadOnlyDictionary<string, string> namespaces)
    {
        var uri = prefix is null
            ? namespaces.TryGetValue(string.Empty, out var defaultUri) ? defaultUri : null
            : namespaces.TryGetValue(prefix, out var prefixedUri) ? prefixedUri : null;

        if (uri is null || !uri.StartsWith("clr-namespace:", StringComparison.Ordinal))
            return null;

        var clrNamespace = uri.Substring("clr-namespace:".Length).Split(';')[0];
        return _compilation.GetTypeByMetadataName($"{clrNamespace}.{typeName}");
    }

    public IReadOnlyList<BindingMember> EnumerateMembers(object typeHandle)
    {
        if (typeHandle is not ITypeSymbol type)
            return Array.Empty<BindingMember>();

        if (_members.TryGetValue(type, out var cached))
            return cached;

        var members = CollectMembers(type);
        _members[type] = members;
        return members;
    }

    /// <summary>Collects the bindable members of a type: public instance properties declared on it
    /// or inherited. Base classes are walked explicitly, and — since an interface has no base type —
    /// so are extended interfaces, otherwise a member inherited from a base interface would look
    /// like an unknown one.</summary>
    private static IReadOnlyList<BindingMember> CollectMembers(ITypeSymbol type)
    {
        // Mirror the runtime PropertyPathResolver: only public instance properties are bindable
        // (fields are not resolved at runtime, so they must not be treated as valid members).
        var members = new List<BindingMember>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = type; current is not null; current = current.BaseType)
            AddDeclaredMembers(current, members, seen);

        if (type.TypeKind == TypeKind.Interface)
            foreach (var extended in type.AllInterfaces)
                AddDeclaredMembers(extended, members, seen);

        return members;
    }

    /// <summary>Adds the type's own public instance properties, skipping names already collected so
    /// that <c>override</c>/<c>new</c> declarations and repeated interface members appear once.</summary>
    private static void AddDeclaredMembers(ITypeSymbol type, List<BindingMember> members, HashSet<string> seen)
    {
        foreach (var member in type.GetMembers())
            if (member is IPropertySymbol { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public } property
                && seen.Add(property.Name))
                members.Add(new BindingMember(property.Name, property.Type));
    }

    public bool IsDynamicMemberContainer(object typeHandle)
    {
        if (typeHandle is not ITypeSymbol type)
            return false;

        return IsStringKeyedDictionary(type) || System.Linq.Enumerable.Any(type.AllInterfaces, IsStringKeyedDictionary);
    }

    private static bool IsStringKeyedDictionary(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol { IsGenericType: true } named)
            return false;

        var definition = named.ConstructedFrom;
        return definition.Name == "IDictionary"
            && definition.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic"
            && named.TypeArguments.Length == 2
            && named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }
}
