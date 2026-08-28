namespace Pysar.Elements.Base;

/// <summary>An object that owns a keyed resource scope (<see cref="Report"/>, <see cref="ReportView"/>).</summary>
public interface IResourceHost
{
    ResourceDictionary Resources { get; }
}
