using Pysar.Core.Enums;
using Pysar.Core.Structs;

namespace Pysar.Core.Abstractions;

public interface IReportElement : IReportObject
{
    public Size Size { get; set; }
    public SizeConstraint MinSize { get; set; }
    public SizeConstraint MaxSize { get; set; }
    public string? Name { get; set; }
    public Position Position { get; set; }
    public bool IsVisible { get; set; }
    public Alignment HorizontalAlignment { get; set; }
    public Alignment VerticalAlignment { get; set; }
    public int ZIndex { get; set; }
    public IReportObject ParentElement { get; set; }
    public Rect Bounds { get; set; }

    /// <summary>Deep-copies this element (values, pending bindings, and children).</summary>
    public IReportElement Clone();
}