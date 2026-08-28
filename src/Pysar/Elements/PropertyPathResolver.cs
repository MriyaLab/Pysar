using System.Reflection;

namespace Pysar.Elements;

/// <summary>
///     Resolves a dotted property path (e.g. "A.B") against an object, mirroring the binding engine's
///     semantics: each segment is looked up as an <see cref="IDictionary{TKey,TValue}"/> key first, then
///     as a public instance property. A null source, empty path, or missing/null segment yields null.
/// </summary>
internal static class PropertyPathResolver
{
    public static object? Resolve(object? source, string? path)
    {
        if (source is null || string.IsNullOrEmpty(path)) return null;

        var current = source;
        foreach (var part in path.Split('.'))
        {
            if (current is null) return null;

            if (current is IDictionary<string, object?> dictN && dictN.TryGetValue(part, out var vN))
            {
                current = vN;
                continue;
            }
            if (current is IDictionary<string, object> dict && dict.TryGetValue(part, out var v))
            {
                current = v;
                continue;
            }

            var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
            current = prop?.GetValue(current);
        }
        return current;
    }
}
