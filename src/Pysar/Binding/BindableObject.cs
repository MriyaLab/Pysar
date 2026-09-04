using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Pysar.Binding;

public abstract class BindableObject : IBindableObject, IBindingStore
{
    private readonly Dictionary<BindableProperty, object?> _values = new();
    private readonly Dictionary<string, ValuePrecedence> _memberPrecedence = new(StringComparer.Ordinal);
    protected readonly Dictionary<BindableProperty, BindingInfo> _pendingBindings = new();

    /// <summary>
    ///     The precedence recorded for writes happening right now. <see cref="ValuePrecedence.Local"/> -
    ///     an assignment by the report author - unless a style, trigger or constructor pass has changed it
    ///     via <see cref="PushWritePrecedence"/>.
    /// </summary>
    public ValuePrecedence WritePrecedence { get; private set; } = ValuePrecedence.Local;

    /// <summary>
    ///     True when a write at <paramref name="precedence"/> may set <paramref name="member"/> - that is,
    ///     when nothing of higher precedence has written it. Styles must consult this before assigning;
    ///     local assignments and triggers write unconditionally.
    /// </summary>
    public bool CanApplyValue(string member, ValuePrecedence precedence)
        => precedence >= (_memberPrecedence.TryGetValue(member, out var current)
            ? current
            : ValuePrecedence.Default);

    /// <summary>
    ///     Records <paramref name="member"/> as written at the current <see cref="WritePrecedence"/>.
    ///     <see cref="SetValue"/> does this for every <see cref="BindableProperty"/> under its own name; a
    ///     CLR facade over a differently named backing property (e.g. <c>FontFamily</c> over <c>Font</c>)
    ///     must call this itself, or a style would keep overwriting it.
    /// </summary>
    /// <remarks>
    ///     Only ever raises. Facades that share a backing property write it on each other's behalf -
    ///     setting <c>Height</c> also writes <c>Size</c> - so a style legitimately setting one facade
    ///     would otherwise re-record the shared backing member at style precedence, and a later style
    ///     could then set that member wholesale and drop the value the author had set through the other
    ///     facade. A value's source only moves up; <see cref="ClearValue"/> is what takes it back down.
    /// </remarks>
    protected void RecordValue(string member)
    {
        if (!_memberPrecedence.TryGetValue(member, out var current) || current < WritePrecedence)
            _memberPrecedence[member] = WritePrecedence;
    }

    /// <summary>
    ///     Makes every write until the returned scope is disposed record <paramref name="precedence"/>
    ///     instead of <see cref="ValuePrecedence.Local"/>. Scopes nest and restore the previous value.
    /// </summary>
    public IDisposable PushWritePrecedence(ValuePrecedence precedence)
        => new PrecedenceScope(this, precedence);

    private sealed class PrecedenceScope : IDisposable
    {
        private readonly BindableObject _owner;
        private readonly ValuePrecedence _previous;

        public PrecedenceScope(BindableObject owner, ValuePrecedence precedence)
        {
            _owner = owner;
            _previous = owner.WritePrecedence;
            owner.WritePrecedence = precedence;
        }

        public void Dispose() => _owner.WritePrecedence = _previous;
    }

    public void SetValue(BindableProperty property, object? value)
    {
        var oldValue = GetValueInternal(property);
        SetValueInternal(property, value);
        RecordValue(property.Name);
        property.OnPropertyChanged(this, oldValue, value);
    }

    public object? GetValue(BindableProperty property)
    {
        return GetValueInternal(property) ?? property.DefaultValue;
    }

    public T GetValue<T>(BindableProperty property)
    {
        var value = GetValueInternal(property) ?? property.DefaultValue;
        if (value != null)
            return (T)value;
        
        if (typeof(T).IsValueType)
            return default!;
            
        return default!;
    }

    public object? GetValueInternal(BindableProperty property)
    {
        return _values.TryGetValue(property, out var value) ? value : null;
    }

    private void SetValueInternal(BindableProperty property, object? value)
    {
        _values[property] = value;
    }

    /// <remarks>
    ///     Also forgets the property's recorded precedence, so the member is open to a style again -
    ///     this is the only way back down the precedence order. A facade over this property
    ///     (e.g. <c>Height</c> over <c>Size</c>) keeps its own recording and must be cleared by name.
    /// </remarks>
    public void ClearValue(BindableProperty property)
    {
        var oldValue = GetValueInternal(property);
        _values.Remove(property);
        _memberPrecedence.Remove(property.Name);
        property.OnPropertyChanged(this, oldValue, property.DefaultValue);
    }

    public void CoerceValue(BindableProperty property)
    {
    }

    // IBindableObject implementation:

    /// <summary>
    ///     True when at least one binding has been set on this object. Lets a consumer outside this
    ///     assembly skip work for objects that carry no bindings — <see cref="IBindingStore"/> is
    ///     internal, so it cannot be used for that check.
    /// </summary>
    public bool HasBindings => _pendingBindings.Count > 0;

    /// <summary>Returns the pending binding for a property, if one was set (before it is resolved).</summary>
    public bool TryGetBinding(BindableProperty property, out BindingInfo? binding)
        => _pendingBindings.TryGetValue(property, out binding);

    IEnumerable<KeyValuePair<BindableProperty, BindingInfo>> IBindingStore.EnumerateBindings()
        => _pendingBindings;

    public virtual void SetBinding(BindableProperty property, string path)
    {
        _pendingBindings[property] = new BindingInfo(path);
    }

    public virtual void SetBinding(BindableProperty property, string path, IValueConverter? converter)
    {
        _pendingBindings[property] = new BindingInfo(path, converter);
    }

    public virtual void SetBinding(BindableProperty property, string path, string? stringFormat)
    {
        _pendingBindings[property] = new BindingInfo(path, null, stringFormat);
    }

    public virtual void SetBinding(BindableProperty property, string path, IValueConverter? converter, string? stringFormat)
    {
        _pendingBindings[property] = new BindingInfo(path, converter, stringFormat);
    }

    public virtual void SetBinding(BindableProperty property, string path, IValueConverter? converter, string? stringFormat, object? converterParameter)
    {
        _pendingBindings[property] = new BindingInfo(path, converter, stringFormat, converterParameter: converterParameter);
    }

    public virtual void SetBinding<T>(BindableProperty property, Expression<Func<T, object>> lambda)
    {
        var path = ExtractPathFromLambda(lambda);
        _pendingBindings[property] = new BindingInfo(path, isLambda: true, lambdaPath: path);
    }

    /// <summary>Sets a fully-formed binding — the only overload that can carry a <see cref="BindingInfo.Source"/>.</summary>
    public virtual void SetBinding(BindableProperty property, BindingInfo binding)
    {
        _pendingBindings[property] = binding;
    }

    /// <summary>
    /// Repoints bindings whose Source is <paramref name="from"/> at <paramref name="to"/>. Cloning a
    /// component (e.g. one repeated row per data item) would otherwise leave every clone's children
    /// reading the original instance's property values.
    /// </summary>
    public void RemapBindingSource(object from, object to)
    {
        foreach (var property in _pendingBindings.Keys.ToList())
        {
            var binding = _pendingBindings[property];
            if (ReferenceEquals(binding.Source, from))
                _pendingBindings[property] = binding.WithSource(to);
        }
    }

    /// <summary>
    ///     Copies the backing values and pending bindings into <paramref name="target"/>. Values are
    ///     copied by reference; a reference-typed <see cref="BindableProperty"/> holding mutable state
    ///     (e.g. a list) would then be shared with the source, so such a property must be deep-copied in
    ///     a type-specific clone override (as <c>Grid</c> does for its definitions).
    /// </summary>
    protected void CopyStateTo(BindableObject target)
    {
        foreach (var kvp in _values)
            target._values[kvp.Key] = kvp.Value;
        foreach (var kvp in _memberPrecedence)
            target._memberPrecedence[kvp.Key] = kvp.Value;
        foreach (var kvp in _pendingBindings)
            target._pendingBindings[kvp.Key] = kvp.Value;
    }

    private string ExtractPathFromLambda<T>(Expression<Func<T, object>> lambda)
    {
        if (lambda.Body is MemberExpression member)
            return member.Member.Name;
        if (lambda.Body is UnaryExpression unary && unary.Operand is MemberExpression m)
            return m.Member.Name;
        throw new ArgumentException("Lambda must be a simple property access");
    }
}
