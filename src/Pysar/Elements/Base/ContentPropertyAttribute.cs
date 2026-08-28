namespace Pysar.Elements.Base;

/// <summary>Names the property that receives an element's XAML child content (WPF-style).</summary>
/// <remarks>
///     This is the one Pysar itself reads - <c>XamlObjectFactory</c> resolves it by type, and the
///     source generator by simple name. Types that carry it also carry
///     <c>System.Windows.Markup.ContentPropertyAttribute</c>, which only the IDE XAML editors read.
///     Both are needed; see the remarks on that type for why.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class ContentPropertyAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
