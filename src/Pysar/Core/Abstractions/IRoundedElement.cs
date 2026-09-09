using Pysar.Core.Structs;

namespace Pysar.Core.Abstractions;

/// <summary>
///     An element whose box is drawn with rounded corners: its background, its border and — when it
///     is a container that clips — its children all follow the radii. Implemented by
///     <c>Pysar.Elements.Frame</c>; the renderer depends on this abstraction so it never has to know
///     the concrete element type.
/// </summary>
public interface IRoundedElement
{
    public CornerRadius CornerRadius { get; set; }
}
