using System.Text;
using Pysar.Core.Abstractions;
using Pysar.Elements;
using Pysar.Elements.Base;

namespace Pysar.Xaml;

/// <summary>Loads report XAML markup into a <see cref="Report"/> element tree at runtime.</summary>
public static class ReportXaml
{
    public static Report Load(string xaml)
    {
        ArgumentNullException.ThrowIfNull(xaml);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        return Load(stream);
    }

    public static Report Load(Stream xaml)
    {
        ArgumentNullException.ThrowIfNull(xaml);
        var result = new XamlLoader().Load(xaml);
        return result.Root as Report
            ?? throw new XamlException($"Root element must be <Report>; got {result.Root.GetType().Name}.");
    }

    public static Report LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        var baseDirectory = Path.GetDirectoryName(fullPath);
        var result = new XamlLoader().Load(stream, baseDirectory: baseDirectory);
        return result.Root as Report
            ?? throw new XamlException($"Root element must be <Report>; got {result.Root.GetType().Name}.");
    }

    // Binary-compatibility overloads. These four widened from Report to ReportObject when component
    // roots were introduced. That is source-compatible but NOT binary-compatible, and PreviewHost is
    // bundled inside the IDE plugins and run against whatever QReport version the user's project was
    // built with — so a plugin compiled before the widening would fail at run time with
    // "Method not found: … LoadFileInto(Report, String)". Keeping the original Report-typed entry
    // points preserves that contract; the compiler binds Report arguments here, and everything else
    // to the ReportObject overloads below.
    public static XamlLoadResult LoadInto(Report root, string xaml)
        => LoadInto((ReportObject)root, xaml);

    public static XamlLoadResult LoadInto(Report root, string xaml, string baseDirectory)
        => LoadInto((ReportObject)root, xaml, baseDirectory);

    public static XamlLoadResult LoadInto(Report root, Stream xaml)
        => LoadInto((ReportObject)root, xaml);

    public static XamlLoadResult LoadFileInto(Report root, string path)
        => LoadFileInto((ReportObject)root, path);

    /// <summary>Populates an existing <see cref="ReportObject"/> (a report or a component root, e.g. an
    /// x:Class subclass) from XAML, returning the x:Name map. The root element's x:Class is ignored at
    /// load time.</summary>
    public static XamlLoadResult LoadInto(ReportObject root, string xaml)
    {
        ArgumentNullException.ThrowIfNull(xaml);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        return LoadInto(root, stream);
    }

    public static XamlLoadResult LoadInto(ReportObject root, string xaml, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(xaml);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xaml));
        return new XamlLoader().Load(stream, root, Path.GetFullPath(baseDirectory));
    }

    public static XamlLoadResult LoadInto(ReportObject root, Stream xaml)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(xaml);
        return new XamlLoader().Load(xaml, root);
    }

    public static XamlLoadResult LoadFileInto(ReportObject root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        var baseDirectory = Path.GetDirectoryName(fullPath);
        return new XamlLoader().Load(stream, root, baseDirectory);
    }
}

/// <summary>The outcome of a load: the root object and the map of x:Name'd elements.</summary>
public sealed record XamlLoadResult(object Root, IReadOnlyDictionary<string, IReportObject> Names);

public sealed class XamlException(string message) : Exception(message);
