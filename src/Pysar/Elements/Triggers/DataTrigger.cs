namespace Pysar.Elements;

/// <summary>
///     A value-compare conditional formatter attached to an element. When the value at
///     <see cref="Binding"/> (resolved against the element's data context) satisfies the
///     <see cref="CompareType"/> comparison against <see cref="Value"/>, the <see cref="Setters"/> are
///     applied to the owning element. Evaluated once, at build time (see TriggerEngine); there is no revert.
/// </summary>
[System.Windows.Markup.ContentProperty(nameof(Setters))]
public sealed class DataTrigger
{
    /// <summary>Property path evaluated against the owning element's data context.</summary>
    public string Binding { get; set; } = string.Empty;

    public CompareType CompareType { get; set; } = CompareType.Equal;

    /// <summary>The literal to compare against, coerced to the bound value's runtime type.</summary>
    public string? Value { get; set; }

    /// <summary>Property assignments applied to the owning element when the comparison holds.</summary>
    public List<Setter> Setters { get; } = new();
}
