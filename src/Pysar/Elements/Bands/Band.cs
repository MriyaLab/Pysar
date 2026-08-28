using Pysar.Binding;
using Pysar.Core.Enums;

namespace Pysar.Elements;

public abstract class Band : Frame
{
    public static BindableProperty PageBreakProperty { get; } =
        BindableProperty.Create(nameof(PageBreak), typeof(PageBreakMode), typeof(Band), PageBreakMode.None);

    public static BindableProperty KeepTogetherProperty { get; } =
        BindableProperty.Create(nameof(KeepTogether), typeof(bool), typeof(Band), false);

    /// <summary>Applies only to flow bands (ReportHeader/Detail/ReportFooter).</summary>
    public PageBreakMode PageBreak
    {
        get => (PageBreakMode)GetValue(PageBreakProperty)!;
        set => SetValue(PageBreakProperty, value);
    }

    /// <summary>Ignored on PageHeader/PageFooter — they are outside the flow.</summary>
    public bool KeepTogether
    {
        get => (bool)GetValue(KeepTogetherProperty)!;
        set => SetValue(KeepTogetherProperty, value);
    }
}
