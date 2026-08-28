namespace Pysar.Core.Structs;

public readonly struct Size
{
    public SizeLength Width  { get; }
    public SizeLength Height { get; }

    public Size(SizeLength width, SizeLength height)
    {
        Width  = width;
        Height = height;
    }

    /// <summary>Both dimensions fill available space (replaces the old -1,-1 convention).</summary>
    public static Size Fill => new(SizeLength.Fill, SizeLength.Fill);

    /// <summary>Both dimensions measured from content.</summary>
    public static Size Auto => new(SizeLength.Auto, SizeLength.Auto);
}
