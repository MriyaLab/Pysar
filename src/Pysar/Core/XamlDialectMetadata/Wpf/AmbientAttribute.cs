namespace System.Windows.Markup;

/// <summary>
/// Marks XAML-scoped values such as resource dictionaries for language services
/// that recognize the standard WPF attribute name, without adding a WPF dependency.
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
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = true)]
public sealed class AmbientAttribute : Attribute;
