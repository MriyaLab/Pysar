namespace Pysar.Core.Abstractions;

/// <summary>
///     Implemented by a data model that can produce a sample instance for design-time preview.
///     Referenced from markup through <c>d:DesignInstance</c> with <c>IsDesignTimeCreatable=True</c>.
/// </summary>
/// <typeparam name="T">The implementing type.</typeparam>
public interface IDesignTimeCreatable<out T>
{
    /// <summary>Creates the sample instance shown in the designer.</summary>
    static abstract T CreateDesignInstance();
}
