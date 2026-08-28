using System.Reflection;
using Pysar.Elements;

namespace Pysar.Xaml;

/// <summary>
///     Resolves an XML namespace + local name to a CLR <see cref="Type"/>. A default namespace maps to
///     CLR namespaces declared via <see cref="XmlnsDefinitionAttribute"/> on the Pysar
///     assembly; a "clr-namespace:Ns;assembly=Asm" namespace maps directly.
/// </summary>
public sealed class XamlTypeResolver
{
    // xmlns URI -> list of (assembly, clrNamespace) search scopes, parsed lazily on first resolve.
    private readonly Dictionary<string, List<(Assembly Asm, string Clr)>> _scopes = new();
    private readonly Dictionary<(string, string), Type?> _cache = new();

    /// <summary>
    ///     Resolves a type. Namespaces need no prior declaration: the clr-namespace is parsed and its
    ///     assembly loaded on first use. Resolving lazily keeps a namespace that is only declared for
    ///     design-time tooling (so <c>d:DesignInstance</c> can name a type) from forcing runtime
    ///     assembly resolution for an assembly the report never instantiates.
    /// </summary>
    public Type Resolve(string uri, string localName)
    {
        if (_cache.TryGetValue((uri, localName), out var cached))
            return cached ?? throw NotFound(uri, localName);

        var scopes = GetScopes(uri);

        Type? found = null;
        foreach (var (asm, clr) in scopes)
        {
            found = asm.GetType($"{clr}.{localName}");
            if (found is not null) break;
        }
        _cache[(uri, localName)] = found;
        return found ?? throw NotFound(uri, localName);
    }

    private List<(Assembly Asm, string Clr)> GetScopes(string uri)
    {
        if (_scopes.TryGetValue(uri, out var existing))
            return existing;

        var scopes = uri.StartsWith("clr-namespace:", StringComparison.Ordinal)
            ? new List<(Assembly, string)> { ParseClrNamespace(uri) }
            : ScopesFromXmlnsDefinitions(uri);
        _scopes[uri] = scopes;
        return scopes;
    }

    private static (Assembly, string) ParseClrNamespace(string uri)
    {
        var body = uri["clr-namespace:".Length..];
        var parts = body.Split(';', StringSplitOptions.TrimEntries);
        var clr = parts[0];
        var asmName = parts.FirstOrDefault(p => p.StartsWith("assembly=", StringComparison.Ordinal))?["assembly=".Length..]
                      ?? throw new XamlException($"clr-namespace requires ;assembly=… : {uri}");
        return (Assembly.Load(asmName), clr);
    }

    private static List<(Assembly, string)> ScopesFromXmlnsDefinitions(string uri)
    {
        var list = new List<(Assembly, string)>();
        var pysarAsm = typeof(Text).Assembly;
        foreach (var def in pysarAsm.GetCustomAttributes<XmlnsDefinitionAttribute>())
            if (def.XmlNamespace == uri)
                list.Add((pysarAsm, def.ClrNamespace));

        if (list.Count == 0)
            throw new XamlException(
                $"No XmlnsDefinition on '{pysarAsm.GetName().Name}' maps the namespace '{uri}'.");
        return list;
    }

    private static XamlException NotFound(string uri, string localName)
        => new($"Type '{localName}' not found in namespace '{uri}'.");
}
