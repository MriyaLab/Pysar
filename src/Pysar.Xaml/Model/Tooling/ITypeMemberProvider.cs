using System.Collections.Generic;

namespace Pysar.Xaml.Model.Tooling;

/// <summary>Host-specific bridge that maps XAML type names to a type handle and
/// enumerates that type's bindable members. Rider implements this over ReSharper
/// PSI; tests implement it over reflection.</summary>
public interface ITypeMemberProvider
{
    /// <summary>Resolves an <c>x:DataType</c> type name to an opaque type handle.</summary>
    /// <param name="prefix">The xmlns prefix used in the type name, or null if none.</param>
    /// <param name="typeName">The local type name.</param>
    /// <param name="namespaces">Prefix → namespace-URI map from the document.</param>
    object? ResolveType(string? prefix, string typeName, IReadOnlyDictionary<string, string> namespaces);

    /// <summary>Enumerates the bindable members declared on the given type handle.</summary>
    IReadOnlyList<BindingMember> EnumerateMembers(object typeHandle);

    /// <summary>Returns <c>true</c> when the type resolves arbitrary string keys at runtime (an
    /// <c>IDictionary&lt;string, object&gt;</c>-like context). Any path segment against such a type is
    /// valid, so validation stops descending there rather than reporting an unknown member.</summary>
    bool IsDynamicMemberContainer(object typeHandle);
}
