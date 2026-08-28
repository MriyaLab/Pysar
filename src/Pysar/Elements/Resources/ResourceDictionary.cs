namespace Pysar.Elements;

/// <summary>A keyed store of reusable XAML resources (values and styles) looked up by x:Key or TargetType.</summary>
/// <remarks>
/// Inherits <c>Source</c> (Uri) and <c>MergedDictionaries</c> from the WPF-compatible base so
/// Rider/ReSharper's XAML resource resolver recognizes them and follows merged dictionaries.
/// Hiding them with differently typed members stops the resolver from treating <c>Source</c> as a
/// merged-dictionary link. The runtime loader reads these members by name from the XAML node tree
/// (see <c>XamlObjectFactory.BuildResourceDictionary</c>), so the base member types are irrelevant
/// at load time.
/// </remarks>
[System.Windows.Markup.Ambient]
public sealed class ResourceDictionary : System.Windows.ResourceDictionary, Base.IResourceHost
{
    ResourceDictionary Base.IResourceHost.Resources => this;
}
