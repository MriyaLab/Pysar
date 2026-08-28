namespace Pysar.Xaml.Model;

internal static class XamlNamespaces
{
    /// <summary>The original (WPF) XAML language namespace.</summary>
    public const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The 2009 XAML language namespace, accepted for compatibility with XAML
    /// authored for MAUI/modern tooling. Note ReSharper only recognises directives like
    /// <c>x:DataType</c> for its built-in dialects, so Pysar's primary data-type hint is the
    /// designer idiom <c>d:DataContext="{d:DesignInstance Type=…}"</c> (see
    /// <c>Tooling.DataTypeHint</c>); <c>x:DataType</c> is an interop fallback.</summary>
    public const string Xaml2009 = "http://schemas.microsoft.com/winfx/2009/xaml";

    /// <summary>True for either XAML language namespace (2006 or 2009). Directives (<c>x:*</c>)
    /// are accepted from both.</summary>
    public static bool IsXaml(string? namespaceName)
        => namespaceName == Xaml || namespaceName == Xaml2009;

    /// <summary>The Blend designer namespace (conventionally <c>d</c>). Carries design-time-only
    /// members such as <c>d:DataContext="{d:DesignInstance Type=…}"</c>, which IDE XAML language
    /// services use to type <c>{Binding}</c> paths.</summary>
    public const string Designer = "http://schemas.microsoft.com/expression/blend/2008";

    /// <summary>The markup-compatibility namespace (conventionally <c>mc</c>), e.g. <c>mc:Ignorable</c>.</summary>
    public const string MarkupCompatibility = "http://schemas.openxmlformats.org/markup-compatibility/2006";

    /// <summary>True for namespaces whose members are design-time only and must be ignored when
    /// loading: the designer and markup-compatibility namespaces.</summary>
    public static bool IsIgnorable(string? namespaceName)
        => namespaceName == Designer || namespaceName == MarkupCompatibility;
}

internal sealed class XamlTypeName
{
    public XamlTypeName(string namespaceName, string localName)
    {
        NamespaceName = namespaceName;
        LocalName = localName;
    }

    public string NamespaceName { get; }
    public string LocalName { get; }
}

internal sealed class XamlMemberName
{
    public XamlMemberName(string namespaceName, string? ownerName, string localName)
    {
        NamespaceName = namespaceName;
        OwnerName = ownerName;
        LocalName = localName;
    }

    public string NamespaceName { get; }
    public string? OwnerName { get; }
    public string LocalName { get; }
}
