using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements.Base;

namespace Pysar.Elements;

public class Frame : ReportContainer<Frame>, IRoundedElement
{
    public static BindableProperty CornerRadiusProperty { get; } =
        BindableProperty.Create(nameof(CornerRadius), typeof(CornerRadius), typeof(Frame), CornerRadius.Zero);

    /// <summary>
    ///     Corner radii, in the MAUI order (top-left, top-right, bottom-left, bottom-right). This is the
    ///     <em>content</em> corner — the border's inner edge — so the box's own corner comes out at
    ///     <c>CornerRadius + BorderThickness</c> and the rounding an author asks for survives however
    ///     thick the border gets. Rounds the background and the border, and — when
    ///     <see cref="ReportContainer{T}.IsClippedToBounds"/> is set — clips children along the inner arc.
    /// </summary>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty)!;
        set => SetValue(CornerRadiusProperty, value);
    }

    public Frame WithCornerRadius(float uniform)
    {
        CornerRadius = new CornerRadius(uniform);
        return this;
    }

    public Frame WithCornerRadius(float topLeft, float topRight, float bottomLeft, float bottomRight)
    {
        CornerRadius = new CornerRadius(topLeft, topRight, bottomLeft, bottomRight);
        return this;
    }
}
