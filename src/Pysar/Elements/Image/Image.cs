using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Elements.Base;

namespace Pysar.Elements;

public class Image : ReportElement<Image>
{
    public static BindableProperty SourceProperty { get; } =
        BindableProperty.Create(nameof(Source), typeof(ImageSource), typeof(Image), null);

    public static BindableProperty AspectProperty { get; } =
        BindableProperty.Create(nameof(Aspect), typeof(Aspect), typeof(Image), Aspect.AspectFit);

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Aspect Aspect
    {
        get => (Aspect)GetValue(AspectProperty)!;
        set => SetValue(AspectProperty, value);
    }

    public Image WithAspect(Aspect aspect) { Aspect = aspect; return this; }

    public override IReportElement Clone()
    {
        var clone = (Image)base.Clone();
        if (Source is not null)
            clone.Source = Source.Clone();
        return clone;
    }
}