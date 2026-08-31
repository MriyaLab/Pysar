using System.Collections;
using System.Reflection;
using Pysar.Binding;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Xaml.Model;

namespace Pysar.Xaml;

/// <summary>Constructs runtime report objects from backend-neutral XAML model nodes.</summary>
internal sealed class XamlObjectFactory
{
    private readonly XamlLoadContext _context;
    private readonly XamlMemberAssigner _members;

    public XamlObjectFactory(XamlLoadContext context)
    {
        _context = context;
        _members = new XamlMemberAssigner(context);
    }

    public object Build(XamlObjectNode node, ReportObject? existingRoot = null)
        => node.Type.LocalName switch
        {
            "Report" => BuildReport(node, existingRoot as Report),
            "ReportView" => BuildView(node, existingRoot as ReportView),
            "ResourceDictionary" => BuildRootDictionary(node),
            _ => BuildObject(node)
        };

    private ResourceDictionary BuildRootDictionary(XamlObjectNode node)
    {
        var dictionary = new ResourceDictionary();
        BuildResourceDictionary(node, dictionary);
        return dictionary;
    }

    public BindingInfo BuildBindingInfo(XamlBindingNode binding, object? source)
        => _members.BuildBindingInfo(binding, source);

    private object BuildObject(XamlObjectNode node)
    {
        var type = _context.ResolveType(node.Type);
        var instance = Activator.CreateInstance(type)
                       ?? throw new XamlException($"Cannot instantiate {type.FullName}.");

        if (_context.TryGetResource(type, out var implicitResource) && implicitResource is Style implicitStyle)
            _members.ApplyStyle(instance, implicitStyle);

        var styleMember = FindMember(node, XamlMemberKind.Attribute, "Style");
        if (styleMember?.Value is XamlStaticResourceNode resource)
        {
            if (_context.ResolveResource(resource.Key) is not Style style)
                throw new XamlException($"Resource '{resource.Key}' is not a Style.");
            if (instance is ReportObject reportObject)
                reportObject.Style = style;
            _members.ApplyStyle(instance, style);
        }

        _members.ApplyAttributes(instance, node);

        // Implicit style, explicit Style, and local attributes are now resolved in the correct
        // precedence order. StyleEngine.Apply runs again later, at Report.Build() - marking this
        // object tells it to leave the result alone rather than reapplying the implicit style's
        // setters on top and overwriting a local override.
        if (instance is ReportObject resolvedObject)
            resolvedObject.StylesResolved = true;

        _context.CaptureName(instance, node);
        ApplyContent(instance, node);
        return instance;
    }

    private Report BuildReport(XamlObjectNode node, Report? existingRoot)
    {
        var report = existingRoot ?? new Report();
        var resources = FindMember(node, XamlMemberKind.PropertyElement, "Resources");
        if (resources is not null)
            BuildResources(resources, report);

        _members.ApplyAttributes(report, node);
        _context.CaptureName(report, node);

        foreach (var childNode in node.Children)
        {
            var child = BuildObject(childNode);
            switch (child)
            {
                case PageFormat pageFormat: report.PageFormat = pageFormat; break;
                case Metadata metadata: report.Metadata = metadata; break;
                case Band band: report.Bands.Set(band); break;
                default:
                    throw new XamlException(
                        $"<Report> cannot contain <{childNode.Type.LocalName}>.");
            }
        }

        ApplyPropertyElements(report, node);
        return report;
    }

    private ReportView BuildView(XamlObjectNode node, ReportView? existingRoot)
    {
        var view = existingRoot ?? new ReportView();
        var resources = FindMember(node, XamlMemberKind.PropertyElement, "Resources");
        if (resources is not null)
            BuildResources(resources, view);

        _members.ApplyAttributes(view, node);
        _context.CaptureName(view, node);

        foreach (var childNode in node.Children)
        {
            var child = BuildObject(childNode);
            if (child is not IReportElement element)
                throw new XamlException(
                    $"<ReportView> cannot contain <{childNode.Type.LocalName}>.");
            _members.ApplyAttachedProperties(childNode, element);
            view.AddElement(element);
        }

        ApplyPropertyElements(view, node);
        return view;
    }

    private void BuildResources(XamlMemberNode resources, IResourceHost host)
    {
        foreach (var resourceNode in resources.Objects)
            AddResource(resourceNode, host);
    }

    private void AddResource(XamlObjectNode resourceNode, IResourceHost host)
    {
        var type = _context.ResolveType(resourceNode.Type);
        if (type == typeof(ResourceDictionary))
        {
            BuildResourceDictionary(resourceNode, host);
            return;
        }

        var key = _context.GetKey(resourceNode);
        var value = BuildResource(resourceNode);

        if (value is Style style && key is null)
        {
            if (style.TargetType is null)
                throw new XamlException("A Style without x:Key requires TargetType.");
            _context.AddResource(host, style.TargetType, style);
            return;
        }
        if (key is not null)
            _context.AddResource(host, key, value);
    }

    private void BuildResourceDictionary(XamlObjectNode node, IResourceHost host)
    {
        if (GetLiteral(node, nameof(ResourceDictionary.Source)) is { } source)
            LoadResourceDictionarySource(source, host);

        var merged = FindMember(node, XamlMemberKind.PropertyElement, nameof(ResourceDictionary.MergedDictionaries));
        if (merged is not null)
        {
            foreach (var dictionaryNode in merged.Objects)
            {
                if (_context.ResolveType(dictionaryNode.Type) != typeof(ResourceDictionary))
                    throw new XamlException(
                        "<ResourceDictionary.MergedDictionaries> can contain only <ResourceDictionary> entries.");
                BuildResourceDictionary(dictionaryNode, host);
            }
        }

        foreach (var child in node.Children)
            AddResource(child, host);
    }

    /// <summary>
    ///     Loads a merged dictionary, preferring the application package over the directory the
    ///     document was loaded from.
    /// </summary>
    /// <remarks>
    ///     A compiled report carries the absolute directory it was built from, which only exists on
    ///     the machine that built it: inside a packaged application - MAUI, or any single-file
    ///     publish - reading it fails. The package is therefore consulted first, under the path
    ///     exactly as authored, which is also how fonts and images already resolve.
    /// </remarks>
    private void LoadResourceDictionarySource(string source, IResourceHost host)
    {
        var packagePath = _context.ResolvePackagePath(source);
        var filePath = _context.ResolveResourceDictionaryPath(source);

        var fromPackage = packagePath is not null && ReportPlatformHandler.FileSystem.Exists(packagePath);

        if (!fromPackage && filePath is null)
            throw new XamlException(
                $"ResourceDictionary Source '{source}' was not found in the application package, and "
                + "the document has no directory to resolve it against. Use ReportXaml.LoadFile(path) "
                + "or package the dictionary as an application asset.");

        if (!_context.MarkResourceDictionaryLoaded(fromPackage ? packagePath! : filePath!))
            return;

        if (!fromPackage && !File.Exists(filePath!))
            throw new XamlException($"ResourceDictionary Source file not found: {filePath}");

        using Stream stream = fromPackage
            ? new MemoryStream(ReadPackageFile(packagePath!))
            : File.OpenRead(filePath!);

        var document = new XamlParser().Parse(stream);
        if (_context.ResolveType(document.Root.Type) != typeof(ResourceDictionary))
            throw new XamlException("ResourceDictionary Source root must be <ResourceDictionary>.");

        // Nested sources inside the loaded file resolve against that file's directory,
        // not the root document's directory - on both routes.
        var previousPackageDirectory = _context.CurrentPackageDirectory;
        var previousDirectory = _context.CurrentDirectory;

        if (packagePath is not null)
            _context.CurrentPackageDirectory = XamlLoadContext.GetPackageDirectory(packagePath);
        if (filePath is not null)
            _context.CurrentDirectory = Path.GetDirectoryName(filePath);

        try
        {
            BuildResourceDictionary(document.Root, host);
        }
        finally
        {
            _context.CurrentPackageDirectory = previousPackageDirectory;
            _context.CurrentDirectory = previousDirectory;
        }
    }

    private static byte[] ReadPackageFile(string packagePath)
    {
        var fileSystem = ReportPlatformHandler.FileSystem;
        if (fileSystem is not ISyncFileSystem syncFileSystem)
        {
            throw new XamlException(
                "ResourceDictionary Source requires ISyncFileSystem on ReportPlatformHandler.FileSystem. " +
                $"Cannot read '{packagePath}' without blocking on async IO.");
        }

        return syncFileSystem.ReadFile(packagePath)
               ?? throw new XamlException($"ResourceDictionary Source could not be read: {packagePath}");
    }

    private ResourceDictionary BuildStandaloneResourceDictionary(XamlObjectNode node)
    {
        var host = new ReportView();
        BuildResourceDictionary(node, host);
        var dictionary = new ResourceDictionary();
        foreach (var item in host.Resources)
        {
            dictionary[item.Key] = item.Value;
        }
        return dictionary;
    }

    private object BuildResource(XamlObjectNode node)
    {
        var type = _context.ResolveType(node.Type);
        if (type == typeof(ResourceDictionary))
            return BuildStandaloneResourceDictionary(node);
        if (type == typeof(Style))
            return BuildStyle(node);
        if (XamlValueConverter.IsConvertible(type))
            return XamlValueConverter.Convert(node.TextContent ?? string.Empty, type)!;
        return BuildObject(node);
    }

    private Style BuildStyle(XamlObjectNode node)
    {
        var style = new Style();
        if (GetLiteral(node, "TargetType") is { } targetType)
            style.TargetType = _context.Types.Resolve(node.Type.NamespaceName, targetType);

        foreach (var setterNode in node.Children.Where(child => child.Type.LocalName == "Setter"))
        {
            var member = GetLiteral(setterNode, "Member")
                         ?? throw new XamlException("<Setter> requires Member.");
            var value = GetSetterValue(setterNode)
                        ?? throw new XamlException(
                            $"<Setter Member=\"{member}\"> requires Value.");
            style.Setters.Add(new Setter { Member = member, Value = value });
        }

        return style;
    }

    private void ApplyContent(object instance, XamlObjectNode node)
    {
        var contentProperty = instance.GetType()
            .GetCustomAttribute<ContentPropertyAttribute>()?.Name;

        if (node.Children.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(node.TextContent) && contentProperty is not null)
                _members.AssignValue(
                    instance,
                    contentProperty,
                    new XamlLiteralNode(node.TextContent!, node.Span));
            ApplyPropertyElements(instance, node);
            return;
        }

        if (instance is IContainerMutator container)
        {
            foreach (var childNode in node.Children)
            {
                if (TryFillCollectionChild(instance, childNode))
                    continue;

                var child = (IReportElement)BuildObject(childNode);
                _members.ApplyAttachedProperties(childNode, child);
                container.AddChild(child);
            }
        }
        else if (contentProperty is not null && node.Children.Count == 1)
        {
            _members.AssignObject(instance, contentProperty, BuildObject(node.Children[0]));
        }
        else
        {
            throw new XamlException($"{instance.GetType().Name} cannot hold child elements.");
        }

        ApplyPropertyElements(instance, node);
    }

    private void ApplyPropertyElements(object instance, XamlObjectNode node)
    {
        foreach (var member in node.Members.Where(member =>
                     member.Kind == XamlMemberKind.PropertyElement
                     && member.Name.LocalName != "Resources"))
        {
            if (member.Name.OwnerName != node.Type.LocalName)
                continue;
            if (member.Name.LocalName == "Triggers")
            {
                BuildTriggers(instance, member);
                continue;
            }

            var property = _members.GetProperty(instance.GetType(), member.Name.LocalName);
            if (typeof(IList).IsAssignableFrom(property.PropertyType))
            {
                FillCollection(property, instance, member.Objects);
                continue;
            }

            if (member.Value is not null && member.Objects.Count == 0)
            {
                _members.AssignValue(instance, property.Name, member.Value);
                continue;
            }

            object value;
            if (member.Objects.Count == 1 && IsShorthandNode(member.Objects[0], member))
                value = BuildInferredValue(property.PropertyType, member.Objects[0]);
            else if (member.Objects.Count == 1)
                value = BuildObject(member.Objects[0]);
            else
                value = BuildInferredValue(property.PropertyType, null);

            property.SetValue(instance, value);
        }
    }

    private object BuildInferredValue(Type type, XamlObjectNode? shorthand)
    {
        var value = Activator.CreateInstance(type)
                    ?? throw new XamlException($"Cannot instantiate {type.Name}.");
        if (shorthand is not null)
        {
            _members.ApplyAttributes(value, shorthand);
            ApplyContent(value, shorthand);
        }
        return value;
    }

    private bool TryFillCollectionChild(object instance, XamlObjectNode child)
    {
        var property = instance.GetType().GetProperty(
            child.Type.LocalName,
            BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !typeof(IList).IsAssignableFrom(property.PropertyType))
            return false;

        FillCollection(property, instance, child.Children);
        return true;
    }

    private void FillCollection(
        PropertyInfo property,
        object instance,
        IEnumerable<XamlObjectNode> itemNodes)
    {
        var list = property.GetValue(instance) as IList
                   ?? (IList)(Activator.CreateInstance(property.PropertyType)
                              ?? throw new XamlException(
                                  $"Cannot instantiate {property.PropertyType.Name}."));
        foreach (var itemNode in itemNodes)
            list.Add(BuildObject(itemNode));
        if (property.CanWrite)
            property.SetValue(instance, list);
    }

    private void BuildTriggers(object instance, XamlMemberNode triggers)
    {
        var list = instance switch
        {
            ReportObject reportObject => reportObject.Triggers,
            ImageSource imageSource => imageSource.Triggers,
            _ => throw new XamlException($"{instance.GetType().Name} does not support Triggers.")
        };

        foreach (var triggerNode in triggers.Objects.Where(node => node.Type.LocalName == "DataTrigger"))
            list.Add(BuildTrigger(triggerNode));
    }

    private DataTrigger BuildTrigger(XamlObjectNode node)
    {
        var bindingValue = FindMember(node, XamlMemberKind.Attribute, "Binding")?.Value
                           ?? throw new XamlException("<DataTrigger> requires Binding.");
        var path = bindingValue switch
        {
            XamlBindingNode binding => binding.Path,
            XamlLiteralNode literal => literal.Text,
            _ => throw new XamlException("<DataTrigger> requires Binding.")
        };

        var trigger = new DataTrigger
        {
            Binding = path,
            Value = GetRawValue(node, "Value")
        };
        if (GetLiteral(node, "CompareType") is { } compareType)
            trigger.CompareType = Enum.Parse<CompareType>(compareType, ignoreCase: true);

        foreach (var setterNode in node.Children.Where(child => child.Type.LocalName == "Setter"))
        {
            var member = GetLiteral(setterNode, "Member")
                         ?? throw new XamlException("<Setter> requires Member.");
            var value = GetSetterValue(setterNode)
                        ?? throw new XamlException(
                            $"<Setter Member=\"{member}\"> requires Value.");
            trigger.Setters.Add(new Setter { Member = member, Value = value });
        }
        return trigger;
    }

    private static bool IsShorthandNode(XamlObjectNode node, XamlMemberNode member)
        => node.Type.LocalName == $"{member.Name.OwnerName}.{member.Name.LocalName}";

    private static XamlMemberNode? FindMember(
        XamlObjectNode node,
        XamlMemberKind kind,
        string name)
        => node.Members.FirstOrDefault(member =>
            member.Kind == kind && member.Name.LocalName == name);

    private static string? GetLiteral(XamlObjectNode node, string name)
        => FindMember(node, XamlMemberKind.Attribute, name)?.Value is XamlLiteralNode literal
            ? literal.Text
            : null;

    private static string? GetRawValue(XamlObjectNode node, string name)
        => FindMember(node, XamlMemberKind.Attribute, name)?.Value switch
        {
            XamlLiteralNode literal => literal.Text,
            XamlStaticResourceNode resource => $"{{StaticResource {resource.Key}}}",
            XamlBindingNode binding => binding.StringFormat is null
                ? $"{{Binding {binding.Path}}}"
                : $"{{Binding Path={binding.Path}, StringFormat={binding.StringFormat}}}",
            XamlUnknownMarkupExtensionNode unknown => unknown.Text,
            _ => null
        };

    private object? GetSetterValue(XamlObjectNode node)
        => FindMember(node, XamlMemberKind.Attribute, "Value")?.Value switch
        {
            XamlStaticResourceNode resource => _context.ResolveResource(resource.Key),
            XamlLiteralNode literal => literal.Text,
            XamlBindingNode binding => binding.StringFormat is null
                ? $"{{Binding {binding.Path}}}"
                : $"{{Binding Path={binding.Path}, StringFormat={binding.StringFormat}}}",
            XamlUnknownMarkupExtensionNode unknown => unknown.Text,
            _ => null
        };
}
