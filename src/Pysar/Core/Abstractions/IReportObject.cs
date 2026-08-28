using Pysar.Core.Enums;
using Pysar.Core.Structs;

namespace Pysar.Core.Abstractions;

public interface IReportObject
{
    public object? DataContext { get; set; }
    public Color BackgroundColor { get; set; }
    public BorderLineStyle BorderLineStyle { get; set; }
    public Thickness BorderThickness { get; set; }
    public Color BorderColor { get; set; }
    public Thickness Padding { get; set; }
    public Thickness Margin { get; set; }
}