namespace Pysar.Core.Abstractions;

public interface IReportContainer : IReportElement
{
    public bool IsClippedToBounds { get; set; }
    public IReadOnlyList<IReportElement> Children { get; }
}