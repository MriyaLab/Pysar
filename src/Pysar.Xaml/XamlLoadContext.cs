using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Xaml.Model;

namespace Pysar.Xaml;

/// <summary>Holds mutable state scoped to one XAML load operation.</summary>
internal sealed class XamlLoadContext
{
    private readonly Dictionary<string, IReportObject> _names = new();
    private readonly ResourceDictionary _resources = new();
    private readonly HashSet<string> _loadedResourceDictionaries = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<(IBindableObject Target, BindableProperty Property, XamlBindingNode Node)> _deferredBindings = new();

    public XamlLoadContext(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory;
        CurrentDirectory = baseDirectory;
    }

    public XamlTypeResolver Types { get; } = new();
    public IReadOnlyDictionary<string, IReportObject> Names => _names;
    public string? BaseDirectory { get; }

    /// <summary>
    /// Directory that relative <c>ResourceDictionary.Source</c> paths resolve against. Equals
    /// <see cref="BaseDirectory"/> for the root document and the directory of a merged dictionary
    /// file while its own nested sources are being loaded.
    /// </summary>
    public string? CurrentDirectory { get; set; }

    /// <summary>
    /// The same position expressed inside the application package, where paths stay relative to the
    /// package root. Empty for the root document, and the directory of a merged dictionary while its
    /// own nested sources are being loaded.
    /// </summary>
    public string CurrentPackageDirectory { get; set; } = string.Empty;

    public Type ResolveType(XamlTypeName type)
        => Types.Resolve(type.NamespaceName, type.LocalName);

    public void CaptureName(object instance, XamlObjectNode node)
    {
        var name = node.Members.FirstOrDefault(member =>
            member.Kind == XamlMemberKind.Directive
            && XamlNamespaces.IsXaml(member.Name.NamespaceName)
            && member.Name.LocalName == "Name");

        if (name?.Value is XamlLiteralNode literal && instance is IReportObject reportObject)
            _names[literal.Text] = reportObject;
    }

    public string? GetKey(XamlObjectNode node)
    {
        var key = node.Members.FirstOrDefault(member =>
            member.Kind == XamlMemberKind.Directive
            && XamlNamespaces.IsXaml(member.Name.NamespaceName)
            && member.Name.LocalName == "Key");
        return (key?.Value as XamlLiteralNode)?.Text;
    }

    public void AddResource(IResourceHost host, object key, object value)
    {
        _resources[key] = value;
        host.Resources[key] = value;
    }

    public bool TryGetResource(object key, out object? value)
        => _resources.TryGetValue(key, out value);

    public object ResolveResource(string key)
        => _resources.TryGetValue(key, out var value)
            ? value
            : throw new XamlException($"Resource '{{StaticResource {key}}}' not found.");

    /// <summary>
    /// The on-disk path a <c>ResourceDictionary.Source</c> points at, or <c>null</c> when the load
    /// has no directory to resolve against - a document loaded from a string or from a package.
    /// </summary>
    public string? ResolveResourceDictionaryPath(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new XamlException("ResourceDictionary Source cannot be empty.");
        if (Path.IsPathRooted(source))
            return Path.GetFullPath(source);
        if (CurrentDirectory is null)
            return null;
        return Path.GetFullPath(Path.Combine(CurrentDirectory, source));
    }

    /// <summary>
    /// The package-relative path a <c>ResourceDictionary.Source</c> points at, or <c>null</c> for an
    /// absolute source, which no package can hold.
    /// </summary>
    public string? ResolvePackagePath(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new XamlException("ResourceDictionary Source cannot be empty.");
        if (Path.IsPathRooted(source))
            return null;

        return CombinePackagePath(CurrentPackageDirectory, source);
    }

    /// <summary>The directory part of a package path, empty when it names a file at the root.</summary>
    public static string GetPackageDirectory(string packagePath)
    {
        var separator = packagePath.LastIndexOf('/');
        return separator < 0 ? string.Empty : packagePath[..separator];
    }

    /// <summary>
    /// Joins a package directory and a source, resolving "." and ".." segments. Package paths are
    /// forward-slashed and relative on every platform, so <see cref="Path"/> is not used here: it
    /// would produce backslashes on Windows and absolute paths from rooted-looking segments.
    /// </summary>
    /// <remarks>
    /// A source may climb above the document - a component in <c>Views/</c> merging
    /// <c>"../Styles/…"</c> - which on disk lands inside the project and in the package lands above
    /// its root. A package has no parent of its root, and assets are packaged under their
    /// project-relative paths, so a leading parent segment is dropped rather than kept: the same
    /// dictionary is then found at <c>Styles/…</c>. Should that miss, the on-disk route still runs.
    /// </remarks>
    private static string CombinePackagePath(string directory, string source)
    {
        var segments = new List<string>();

        foreach (var part in $"{directory}/{source}".Replace('\\', '/').Split('/'))
        {
            if (part.Length == 0 || part == ".")
                continue;

            if (part != "..")
                segments.Add(part);
            else if (segments.Count > 0)
                segments.RemoveAt(segments.Count - 1);
        }

        return string.Join('/', segments);
    }

    public bool MarkResourceDictionaryLoaded(string path)
        => _loadedResourceDictionaries.Add(path);

    /// <summary>
    /// Queues a binding that names an explicit source. Resolution is deferred to
    /// <see cref="ApplyDeferredBindings"/> so a name may be referenced before it is declared —
    /// notably the component root, whose children are built after it.
    /// </summary>
    public void DeferSourceBinding(IBindableObject target, BindableProperty property, XamlBindingNode node)
        => _deferredBindings.Add((target, property, node));

    /// <summary>Applies every queued source binding once the object tree is complete.</summary>
    public void ApplyDeferredBindings(Func<XamlBindingNode, object?, BindingInfo> build)
    {
        foreach (var (target, property, node) in _deferredBindings)
        {
            if (!_names.TryGetValue(node.SourceName!, out var source))
                throw new XamlException(
                    $"Binding Source={{x:Reference {node.SourceName}}} does not name any x:Name in this document.");
            target.SetBinding(property, build(node, source));
        }

        _deferredBindings.Clear();
    }
}
