namespace Pysar.Elements;

/// <summary>A reusable set of property setters targeting elements of <see cref="TargetType"/>.</summary>
[System.Windows.Markup.ContentProperty(nameof(Setters))]
public sealed class Style
{
    public Type? TargetType { get; set; }
    public List<Setter> Setters { get; } = new();
}
