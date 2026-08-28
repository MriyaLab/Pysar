using Pysar.Binding;
using Pysar.Core.Enums;
using Pysar.Elements.Base;

namespace Pysar.Elements;

/// <summary>
///     A container that stacks its children one after another — vertically (top to bottom) or
///     horizontally (left to right), depending on <see cref="Orientation"/>.
/// </summary>
public sealed class StackPanel : ReportContainer<StackPanel>
{
    public static BindableProperty OrientationProperty { get; } =
        BindableProperty.Create(nameof(Orientation), typeof(StackOrientation), typeof(StackPanel), StackOrientation.Vertical);

    public static BindableProperty SpacingProperty { get; } =
        BindableProperty.Create(nameof(Spacing), typeof(float), typeof(StackPanel), 0f);

    public StackOrientation Orientation
    {
        get => (StackOrientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gap in points between consecutive children along the stacking axis. Default 0.</summary>
    public float Spacing
    {
        get => (float)GetValue(SpacingProperty)!;
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>Sets the stacking direction (<see cref="StackOrientation.Vertical"/> by default).</summary>
    public StackPanel WithOrientation(StackOrientation orientation)
    {
        Orientation = orientation;
        return this;
    }

    public StackPanel WithSpacing(float spacing)
    {
        Spacing = spacing;
        return this;
    }
}
