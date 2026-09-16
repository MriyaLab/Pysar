using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements.Base;

namespace Pysar.Elements;

public class Grid : ReportContainer<Grid>
{
    public static BindableProperty ColumnDefinitionsProperty { get; } =
        BindableProperty.Create(nameof(ColumnDefinitions), typeof(List<ColumnDefinition>), typeof(Grid), null);

    public static BindableProperty RowDefinitionsProperty { get; } =
        BindableProperty.Create(nameof(RowDefinitions), typeof(List<RowDefinition>), typeof(Grid), null);

    public static BindableProperty ColumnSpacingProperty { get; } =
        BindableProperty.Create(nameof(ColumnSpacing), typeof(float), typeof(Grid), 0f);

    public static BindableProperty RowSpacingProperty { get; } =
        BindableProperty.Create(nameof(RowSpacing), typeof(float), typeof(Grid), 0f);

    public Grid()
    {
        // At Default precedence: a type's own defaults must not shadow a later style (see Text).
        using var defaults = PushWritePrecedence(ValuePrecedence.Default);
        RowDefinitions = new List<RowDefinition>();
        ColumnDefinitions = new List<ColumnDefinition>();
    }

    public List<ColumnDefinition> ColumnDefinitions
    {
        get => GetDefinitionList(ColumnDefinitionsProperty, static () => new List<ColumnDefinition>());
        set => SetValue(ColumnDefinitionsProperty, value);
    }

    public List<RowDefinition> RowDefinitions
    {
        get => GetDefinitionList(RowDefinitionsProperty, static () => new List<RowDefinition>());
        set => SetValue(RowDefinitionsProperty, value);
    }

    private List<T> GetDefinitionList<T>(BindableProperty property, Func<List<T>> create)
    {
        if (GetValueInternal(property) is List<T> list)
            return list;

        list = create();
        SetValue(property, list);
        return list;
    }

    public float ColumnSpacing
    {
        get => (float)GetValue(ColumnSpacingProperty)!;
        set => SetValue(ColumnSpacingProperty, value);
    }

    public float RowSpacing
    {
        get => (float)GetValue(RowSpacingProperty)!;
        set => SetValue(RowSpacingProperty, value);
    }

    public Grid AddElement(IReportElement element, int row, int column,
        int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentNullException.ThrowIfNull(element);
        GridAttached.SetRow(element, row);
        GridAttached.SetColumn(element, column);
        GridAttached.SetRowSpan(element, rowSpan);
        GridAttached.SetColumnSpan(element, columnSpan);

        AddElement(element);
        return this;
    }

    public Grid WithColumnDefinitions(params ColumnDefinition[] definitions)
    {
        ColumnDefinitions = [..definitions];
        return this;
    }

    public Grid WithColumnDefinitions(string definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ColumnDefinitions = definitions
            .Split(',')
            .Select(s => new ColumnDefinition(GridLength.Parse(s)))
            .ToList();
        return this;
    }

    public Grid WithRowDefinitions(params RowDefinition[] definitions)
    {
        RowDefinitions = [..definitions];
        return this;
    }

    public Grid WithRowDefinitions(string definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        RowDefinitions = definitions
            .Split(',')
            .Select(s => new RowDefinition(GridLength.Parse(s)))
            .ToList();
        return this;
    }

    public Grid WithColumnSpacing(float spacing)
    {
        ColumnSpacing = spacing;
        return this;
    }

    public Grid WithRowSpacing(float spacing)
    {
        RowSpacing = spacing;
        return this;
    }

    public override IReportElement Clone()
    {
        var clone = (Grid)base.Clone();
        clone.ColumnDefinitions = new List<ColumnDefinition>(ColumnDefinitions);
        clone.RowDefinitions = new List<RowDefinition>(RowDefinitions);
        for (int i = 0; i < Children.Count; i++)
        {
            var original = Children[i];
            var copied = clone.Children[i];
            GridAttached.SetRow(copied, GridAttached.GetRow(original));
            GridAttached.SetColumn(copied, GridAttached.GetColumn(original));
            GridAttached.SetRowSpan(copied, GridAttached.GetRowSpan(original));
            GridAttached.SetColumnSpan(copied, GridAttached.GetColumnSpan(original));
        }
        return clone;
    }
}
