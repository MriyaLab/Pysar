namespace Pysar.Binding;

internal interface IBindingStore
{
    IEnumerable<KeyValuePair<BindableProperty, BindingInfo>> EnumerateBindings();
}
