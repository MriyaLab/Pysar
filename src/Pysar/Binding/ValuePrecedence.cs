namespace Pysar.Binding;

/// <summary>
///     Who wrote a member's current value. A write is applied only when its precedence is at least that
///     of the value already there, which is what keeps a style from overwriting a value the report
///     author assigned directly.
/// </summary>
public enum ValuePrecedence
{
    /// <summary>Never written, or written by the type itself (constructor defaults).</summary>
    Default = 0,

    /// <summary>A keyless <c>&lt;Style TargetType="…"&gt;</c> picked up from the resources.</summary>
    ImplicitStyle = 1,

    /// <summary>The style named by the object's own <c>Style</c> member.</summary>
    ExplicitStyle = 2,

    /// <summary>Assigned by the author: a XAML attribute, a code-first assignment, a resolved binding.</summary>
    Local = 3,

    /// <summary>Applied by a satisfied <c>DataTrigger</c> at build time. Beats everything.</summary>
    Trigger = 4
}
