namespace System.Windows;

using System.Collections.ObjectModel;

/// <summary>
/// Lightweight compatibility surface for XAML language services that recognize
/// the standard WPF ResourceDictionary type name.
/// </summary>
/// <remarks>
///     <b>The namespace is the point, and it is deliberate.</b> Visual Studio, Rider and the VSCode
///     extension recognise this type by its full WPF name; declaring it anywhere else would silently
///     lose XAML IntelliSense. QReport's own loader and source generator do <i>not</i> read it - they
///     read the equivalent in <c>Pysar.Elements</c> - so the two are annotated side by side
///     on every type that needs them, and removing either one breaks something that the other does
///     not cover.
///     <para>
///     The cost: <c>Pysar.Core</c> is packable, so an application that references it with
///     <c>UseWPF</c> set gets CS0433 in its own code the moment it names this type unqualified under
///     <c>using System.Windows;</c>. Nothing in this repository does, which is the only reason the
///     collision has never surfaced.
///     </para>
/// </remarks>
public class ResourceDictionary : Dictionary<object, object>
{
    /// <summary>Resource dictionaries merged into this dictionary.</summary>
    public Collection<ResourceDictionary> MergedDictionaries { get; } = new();

    /// <summary>External dictionary location used by WPF-style XAML language services.</summary>
    public Uri? Source { get; set; }
}

/// <summary>
/// Compatibility surface for WPF-style <c>{StaticResource ...}</c> markup
/// extension recognition in XAML language services.
/// </summary>
public sealed class StaticResourceExtension : Markup.MarkupExtension
{
    public StaticResourceExtension()
    {
    }

    public StaticResourceExtension(object resourceKey)
    {
        ResourceKey = resourceKey;
    }

    public object? ResourceKey { get; set; }

    public override object? ProvideValue(IServiceProvider serviceProvider)
        => ResourceKey;
}
