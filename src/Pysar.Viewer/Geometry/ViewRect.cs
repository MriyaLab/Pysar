namespace Pysar.Viewer.Geometry;

/// <summary>A point in the units a viewer lays out in.</summary>
public readonly record struct ViewPoint(double X, double Y);

/// <summary>
///     A rectangle in the units a viewer lays out in.
/// </summary>
/// <remarks>
///     The core carries its own rather than a framework's: depending on Microsoft.Maui.Graphics or
///     Avalonia here would defeat the point of having one implementation for both. Each control
///     converts at its own boundary, which is a line.
/// </remarks>
public readonly record struct ViewRect(double X, double Y, double Width, double Height)
{
    /// <summary>Grows the rectangle by <paramref name="amount"/> on every side.</summary>
    public ViewRect Inflate(double amount) => new(
        X - amount, Y - amount, Width + 2 * amount, Height + 2 * amount);
}
