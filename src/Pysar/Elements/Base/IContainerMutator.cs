using Pysar.Core.Abstractions;

namespace Pysar.Elements.Base;

/// <summary>
///     Internal mutation surface used by build-time tree transforms (e.g. <c>RepeaterExpander</c>) to
///     replace a container's children without knowing its concrete generic type. Implemented by
///     <see cref="ReportContainer{T}"/>.
/// </summary>
internal interface IContainerMutator
{
    void ReplaceChildren(IEnumerable<IReportElement> children);
    void AddChild(IReportElement element);
}
