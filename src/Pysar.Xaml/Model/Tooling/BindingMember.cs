namespace Pysar.Xaml.Model.Tooling;

/// <summary>A member (property/field) discovered on a data type: its name and an
/// opaque handle for the member's own type, usable for the next path segment.</summary>
public readonly struct BindingMember
{
    public BindingMember(string name, object? typeHandle)
    {
        Name = name;
        TypeHandle = typeHandle;
    }

    /// <summary>The member's name, as written in a binding path.</summary>
    public string Name { get; }

    /// <summary>Opaque handle for the member's declared type. Host-specific
    /// (e.g. <c>System.Type</c> or a PSI type element). Null if unresolved.</summary>
    public object? TypeHandle { get; }
}
