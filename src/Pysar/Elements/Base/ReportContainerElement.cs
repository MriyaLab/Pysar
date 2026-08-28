using Pysar.Binding;
using Pysar.Core.Abstractions;

namespace Pysar.Elements.Base;

[ContentProperty(nameof(Children))]
[System.Windows.Markup.ContentProperty(nameof(Children))]
public abstract class ReportContainer<T> : ReportElement<T>, IReportContainer, IContainerMutator
    where T : ReportContainer<T>
{
    private readonly List<IReportElement> _children = new();

    public static BindableProperty IsClippedToBoundsProperty { get; } =
        BindableProperty.Create(nameof(IsClippedToBounds), typeof(bool), typeof(ReportContainer<T>), true);

    public bool IsClippedToBounds
    {
        get => (bool)GetValue(IsClippedToBoundsProperty)!;
        set => SetValue(IsClippedToBoundsProperty, value);
    }

    public IReadOnlyList<IReportElement> Children => _children.AsReadOnly();
    
    public virtual T AddElement(IReportElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        
        element.ParentElement = this;
        _children.Add(element);
        
        return (T)this;
    }
    
    public virtual T RemoveElement(IReportElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        _children.Remove(element);

        return (T)this;
    }

    public T AddElements(IEnumerable<IReportElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        foreach (var element in elements) 
            AddElement(element);

        return (T)this;
    }

    public T AddElements(Func<IEnumerable<IReportElement>> elementsFunc)
    {
        ArgumentNullException.ThrowIfNull(elementsFunc);
        return AddElements(elementsFunc());
    }

    public T WithIsClippedToBounds(bool value)
    {
        IsClippedToBounds = value;
        return (T)this;
    }

    public override IReportElement Clone()
    {
        var clone = (ReportContainer<T>)base.Clone();
        clone.ClearElements();
        foreach (var child in _children)
            clone.AddElement(child.Clone());
        return clone;
    }

    public T ClearElements()
    {
        _children.Clear();
        return (T)this;
    }

    void IContainerMutator.ReplaceChildren(IEnumerable<IReportElement> children)
    {
        _children.Clear();
        foreach (var child in children)
            AddElement(child);
    }

    void IContainerMutator.AddChild(IReportElement element) => AddElement(element);
}
