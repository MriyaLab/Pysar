using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;

namespace Pysar.Elements.Base;

public abstract class ReportElement : ReportObject, IReportElement
{
    public static BindableProperty NameProperty { get; } =
        BindableProperty.Create(nameof(Name), typeof(string), typeof(ReportElement), null);

    public static BindableProperty SizeProperty { get; } =
        BindableProperty.Create(nameof(Size), typeof(Size), typeof(ReportElement), Size.Fill);

    public static BindableProperty MinSizeProperty { get; } =
        BindableProperty.Create(nameof(MinSize), typeof(SizeConstraint), typeof(ReportElement), SizeConstraint.None);

    public static BindableProperty MaxSizeProperty { get; } =
        BindableProperty.Create(nameof(MaxSize), typeof(SizeConstraint), typeof(ReportElement), SizeConstraint.None);

    public static BindableProperty PositionProperty { get; } =
        BindableProperty.Create(nameof(Position), typeof(Position), typeof(ReportElement), Position.Empty);

    public static BindableProperty IsVisibleProperty { get; } =
        BindableProperty.Create(nameof(IsVisible), typeof(bool), typeof(ReportElement), true);

    public static BindableProperty HorizontalAlignmentProperty { get; } =
        BindableProperty.Create(nameof(HorizontalAlignment), typeof(Alignment), typeof(ReportElement), Alignment.Start);

    public static BindableProperty VerticalAlignmentProperty { get; } =
        BindableProperty.Create(nameof(VerticalAlignment), typeof(Alignment), typeof(ReportElement), Alignment.Start);

    public static BindableProperty ZIndexProperty { get; } =
        BindableProperty.Create(nameof(ZIndex), typeof(int), typeof(ReportElement), 0);
    
    public string? Name
    {
        get => (string?)GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    public Size Size
    {
        get => (Size)GetValue(SizeProperty)!;
        set => SetValue(SizeProperty, value);
    }

    // The size facades below record their own member name for the same reason the font facades in
    // Text do: they write a differently named backing property, so without this a style would keep
    // overwriting a locally set part. Note that MinSize/MaxSize both flow through SetMinMax, which
    // writes both backing properties - so any min/max write records both of those container member
    // names. A style setting MinSize/MaxSize wholesale is therefore blocked by any local min/max
    // write; the per-part members (MinWidth, ...) stay precise.
    /// <summary>Facade over <see cref="Size"/>'s width (e.g. XAML <c>Width="Fill"</c> / <c>Width="60"</c>).</summary>
    public SizeLength Width
    {
        get => Size.Width;
        set { Size = new Size(value, Size.Height); RecordValue(nameof(Width)); }
    }

    /// <summary>Facade over <see cref="Size"/>'s height.</summary>
    public SizeLength Height
    {
        get => Size.Height;
        set { Size = new Size(Size.Width, value); RecordValue(nameof(Height)); }
    }

    public SizeConstraint MinSize
    {
        get => (SizeConstraint)GetValue(MinSizeProperty)!;
        set => SetMinMax(value, MaxSize);
    }

    public SizeConstraint MaxSize
    {
        get => (SizeConstraint)GetValue(MaxSizeProperty)!;
        set => SetMinMax(MinSize, value);
    }

    /// <summary>Facade over <see cref="MinSize"/>'s width.</summary>
    public MinMaxLength MinWidth
    {
        get => MinSize.Width;
        set { MinSize = new SizeConstraint(value, MinSize.Height); RecordValue(nameof(MinWidth)); }
    }

    /// <summary>Facade over <see cref="MinSize"/>'s height.</summary>
    public MinMaxLength MinHeight
    {
        get => MinSize.Height;
        set { MinSize = new SizeConstraint(MinSize.Width, value); RecordValue(nameof(MinHeight)); }
    }

    /// <summary>Facade over <see cref="MaxSize"/>'s width.</summary>
    public MinMaxLength MaxWidth
    {
        get => MaxSize.Width;
        set { MaxSize = new SizeConstraint(value, MaxSize.Height); RecordValue(nameof(MaxWidth)); }
    }

    /// <summary>Facade over <see cref="MaxSize"/>'s height.</summary>
    public MinMaxLength MaxHeight
    {
        get => MaxSize.Height;
        set { MaxSize = new SizeConstraint(MaxSize.Width, value); RecordValue(nameof(MaxHeight)); }
    }

    public Position Position
    {
        get => (Position)GetValue(PositionProperty)!;
        set => SetValue(PositionProperty, value);
    }

    public bool IsVisible
    {
        get => (bool)GetValue(IsVisibleProperty)!;
        set => SetValue(IsVisibleProperty, value);
    }

    public Alignment HorizontalAlignment
    {
        get => (Alignment)GetValue(HorizontalAlignmentProperty)!;
        set => SetValue(HorizontalAlignmentProperty, value);
    }

    public Alignment VerticalAlignment
    {
        get => (Alignment)GetValue(VerticalAlignmentProperty)!;
        set => SetValue(VerticalAlignmentProperty, value);
    }

    public int ZIndex
    {
        get => (int)GetValue(ZIndexProperty)!;
        set => SetValue(ZIndexProperty, value);
    }

    public IReportObject ParentElement { get; set; } = null!;

    public Rect Bounds { get; set; } =  Rect.Empty;

    public virtual IReportElement Clone()
    {
        var clone = (ReportElement)Activator.CreateInstance(GetType())!;
        CopyStateTo(clone);
        // Triggers are plain config (not bindable state), so CopyStateTo skips them; carry them over so a
        // cloned row template keeps its conditional formatting. Sharing instances is safe — triggers only
        // mutate the element they run on, never themselves.
        foreach (var trigger in Triggers)
            clone.Triggers.Add(trigger);
        return clone;
    }

    private void SetMinMax(SizeConstraint min, SizeConstraint max)
    {
        var (normalizedMin, normalizedMax) = Normalize(min, max);
        SetValue(MinSizeProperty, normalizedMin);
        SetValue(MaxSizeProperty, normalizedMax);
    }

    private static (SizeConstraint Min, SizeConstraint Max) Normalize(SizeConstraint min, SizeConstraint max)
    {
        var widthMax = NormalizeAxis(min.Width, max.Width);
        var heightMax = NormalizeAxis(min.Height, max.Height);
        return (min, new SizeConstraint(widthMax, heightMax));
    }

    private static MinMaxLength NormalizeAxis(MinMaxLength min, MinMaxLength max)
    {
        if (min.IsFixed && max.IsFixed && max.Value < min.Value)
            return min;
        return max;
    }
}

public abstract class ReportElement<T> : ReportElement where T : ReportElement<T>
{
    public T WithDataContext(object? context)
    {
        DataContext = context;
        return (T)this;
    }

    public T WithName(string name)
    {
        Name = name;
        return (T)this;
    }

    public T At(Position position)
    {
        Position = position;
        return (T)this;
    }

    public T At(float x, float y)
    {
        Position = new Position(x, y);
        return (T)this;
    }

    public T WithVisible(bool visible)
    {
        IsVisible = visible;
        return (T)this;
    }

    public T WithSize(Size size)
    {
        Size = size;
        return (T)this;
    }

    public T WithSize(SizeLength width, SizeLength height)
    {
        Size = new Size(width, height);
        return (T)this;
    }

    public T WithMinSize(SizeConstraint minSize)
    {
        MinSize = minSize;
        return (T)this;
    }

    public T WithMaxSize(SizeConstraint maxSize)
    {
        MaxSize = maxSize;
        return (T)this;
    }

    public T WithMinWidth(MinMaxLength minWidth)
    {
        MinWidth = minWidth;
        return (T)this;
    }

    public T WithMinHeight(MinMaxLength minHeight)
    {
        MinHeight = minHeight;
        return (T)this;
    }

    public T WithMaxWidth(MinMaxLength maxWidth)
    {
        MaxWidth = maxWidth;
        return (T)this;
    }

    public T WithMaxHeight(MinMaxLength maxHeight)
    {
        MaxHeight = maxHeight;
        return (T)this;
    }

    public T WithPadding(float left, float top, float right, float bottom)
    {
        Padding = new Thickness(left, top, right, bottom);
        return (T)this;
    }

    public T WithPadding(float horizontal, float vertical)
    {
        Padding = new Thickness(horizontal, vertical);
        return (T)this;
    }
    
    public T WithPadding(float uniform)
    {
        Padding = new Thickness(uniform);
        return (T)this;
    }

    public T WithMargin(float left, float top, float right, float bottom)
    {
        Margin = new Thickness(left, top, right, bottom);
        return (T)this;
    }

    public T WithMargin(float horizontal, float vertical)
    {
        Margin = new Thickness(horizontal, vertical);
        return (T)this;
    }
    
    public T WithMargin(float uniform)
    {
        Margin = new Thickness(uniform);
        return (T)this;
    }

    public T WithBackgroundColor(string hexColor)
    {
        BackgroundColor =  Color.FromHex(hexColor);
        return (T)this;
    }

    public T WithBackgroundColor(byte a, byte r, byte g, byte b)
    {
        BackgroundColor =  Color.FromArgb(a, r, g, b);
        return (T)this;
    }

    public T WithBackgroundColor(Color color)
    {
        BackgroundColor = color;
        return (T)this;
    }

    public T WithHorizontalAlignment(Alignment alignment)
    {
        HorizontalAlignment = alignment;
        return (T)this;
    }
    

    public T WithVerticalAlignment(Alignment alignment)
    {
        VerticalAlignment = alignment;
        return (T)this;
    }
    
    public T WithBorderThickness(float uniform)
    {
        BorderThickness = new Thickness(uniform);
        return (T)this;
    }
    
    public T WithBorderThickness(float horizontal, float vertical)
    {
        BorderThickness = new Thickness(horizontal, vertical);
        return (T)this;
    }

    public T WithBorderThickness(float left, float top, float right, float bottom)
    {
        BorderThickness = new Thickness(left, top, right, bottom);
        return (T)this;
    }

    public T WithBorderColor(string hexColor)
    {
        BorderColor = Color.FromHex(hexColor);
        return (T)this;
    }

    public T WithBorderColor(byte a, byte r, byte g, byte b)
    {
        BorderColor =  Color.FromArgb(a, r, g, b);
        return (T)this;
    }

    public T WithBorderColor(Color color)
    {
        BorderColor = color;
        return (T)this;
    }

    public T WithBorderLineStyle(BorderLineStyle borderLineStyle)
    {
        BorderLineStyle = borderLineStyle;
        return (T)this;
    }

    public T WithZIndex(int index)
    {
        ZIndex = index;
        return (T)this;
    }
}
