using Pysar.Binding;

namespace Pysar.Elements;

/// <summary>
///     Resolves a dotted property path against an object. One walker with
///     <see cref="BindingEngine.ResolvePath"/>.
/// </summary>
internal static class PropertyPathResolver
{
    public static object? Resolve(object? source, string? path)
        => BindingEngine.ResolvePath(source, path);
}
