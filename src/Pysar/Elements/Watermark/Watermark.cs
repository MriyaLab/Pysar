using Pysar.Binding;
using Pysar.Core.Enums;

namespace Pysar.Elements;

public sealed class Watermark : Frame
{
    public static BindableProperty LayerProperty { get; } =
        BindableProperty.Create(nameof(Layer), typeof(WatermarkLayer), typeof(Watermark), WatermarkLayer.Behind);

    /// <summary>
    ///     Paint order relative to report content. <see cref="WatermarkLayer.Behind"/> (default) draws
    ///     under the bands; <see cref="WatermarkLayer.Front"/> draws over them. The page border still
    ///     paints last either way.
    /// </summary>
    public WatermarkLayer Layer
    {
        get => (WatermarkLayer)GetValue(LayerProperty)!;
        set => SetValue(LayerProperty, value);
    }

    public Watermark WithLayer(WatermarkLayer layer)
    {
        Layer = layer;
        return this;
    }
}
