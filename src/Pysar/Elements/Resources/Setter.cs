namespace Pysar.Elements;

/// <summary>A single property assignment applied by a <see cref="Style"/>.</summary>
public sealed class Setter
{
    /// <summary>The CLR member assigned by this setter.</summary>
    public string Member { get; set; } = string.Empty;

    public object? Value { get; set; }
}
