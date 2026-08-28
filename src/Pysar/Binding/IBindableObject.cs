using System.Linq.Expressions;

namespace Pysar.Binding;

public interface IBindableObject
{
    void SetBinding(BindableProperty property, string path);
    void SetBinding(BindableProperty property, string path, IValueConverter? converter);
    void SetBinding(BindableProperty property, string path, string? stringFormat);
    void SetBinding(BindableProperty property, string path, IValueConverter? converter, string? stringFormat);
    void SetBinding(BindableProperty property, string path, IValueConverter? converter, string? stringFormat, object? converterParameter);
    void SetBinding(BindableProperty property, BindingInfo binding);
    void SetBinding<T>(BindableProperty property, Expression<Func<T, object>> lambda);
}