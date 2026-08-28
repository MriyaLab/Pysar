using Pysar.Core.Structs;

namespace Pysar.Skia.Layout;

internal static class SizeConstraints
{
    public static (float Width, float Height) Clamp(
        float width, float height, SizeConstraint min, SizeConstraint max)
    {
        return (ClampAxis(width, min.Width, max.Width), ClampAxis(height, min.Height, max.Height));
    }

    private static float ClampAxis(float value, MinMaxLength min, MinMaxLength max)
    {
        var v = value;
        if (min.IsFixed) v = Math.Max(v, min.Value);
        if (max.IsFixed) v = Math.Min(v, max.Value);
        return v;
    }
}
