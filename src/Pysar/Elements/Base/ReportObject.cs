using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;

namespace Pysar.Elements.Base;

public abstract class ReportObject : BindableObject, IReportObject
{
    public static BindableProperty DataContextProperty { get; } =
        BindableProperty.Create(nameof(DataContext), typeof(object), typeof(ReportObject), null);

    public static BindableProperty BackgroundColorProperty { get; } =
        BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(ReportObject), Colors.Transparent);

    public static BindableProperty BorderLineStyleProperty { get; } =
        BindableProperty.Create(nameof(BorderLineStyle), typeof(BorderLineStyle), typeof(ReportObject), BorderLineStyle.Solid);

    public static BindableProperty BorderThicknessProperty { get; } =
        BindableProperty.Create(nameof(BorderThickness), typeof(Thickness), typeof(ReportObject), Thickness.Zero);

    public static BindableProperty BorderColorProperty { get; } =
        BindableProperty.Create(nameof(BorderColor), typeof(Color), typeof(ReportObject), Colors.Black);

    public static BindableProperty PaddingProperty { get; } =
        BindableProperty.Create(nameof(Padding), typeof(Thickness), typeof(ReportObject), Thickness.Zero);

    public static BindableProperty MarginProperty { get; } =
        BindableProperty.Create(nameof(Margin), typeof(Thickness), typeof(ReportObject), Thickness.Zero);

    public object? DataContext
    {
        get => GetValue(DataContextProperty);
        set => SetValue(DataContextProperty, value);
    }

    /// <summary>
    /// Identifies an explicitly assigned report style for CLR-based XAML language services.
    /// Runtime loading resolves and applies the referenced style before member assignment.
    /// </summary>
    public Style? Style { get; set; }

    /// <summary>Conditional formatters evaluated at build time (see TriggerEngine). Empty by default.</summary>
    public IList<DataTrigger> Triggers { get; } = new List<DataTrigger>();

    public Color BackgroundColor
    {
        get => (Color)GetValue(BackgroundColorProperty)!;
        set => SetValue(BackgroundColorProperty, value);
    }

    public BorderLineStyle BorderLineStyle
    {
        get => (BorderLineStyle)GetValue(BorderLineStyleProperty)!;
        set => SetValue(BorderLineStyleProperty, value);
    }

    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty)!;
        set => SetValue(BorderThicknessProperty, value);
    }

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty)!;
        set => SetValue(BorderColorProperty, value);
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty)!;
        set => SetValue(PaddingProperty, value);
    }

    public Thickness Margin
    {
        get => (Thickness)GetValue(MarginProperty)!;
        set => SetValue(MarginProperty, value);
    }
}
