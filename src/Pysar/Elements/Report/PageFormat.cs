using Pysar.Core.Enums;

namespace Pysar.Elements;

public class PageFormat
{
    public PageSize Size { get; set; } = PageSize.A4;
    public Orientation Orientation { get; set; } = Orientation.Portrait;
    public Core.Structs.Thickness Margin { get; set; } = new Core.Structs.Thickness(40, 30, 40, 30);

    public (float Width, float Height) GetPageSizePt() => (Size, Orientation) switch
    {
        (PageSize.A4, Orientation.Portrait) => (595.5f, 842f),
        (PageSize.A4, Orientation.Landscape) => (842f, 595.5f),
        _ => (595.5f, 842f)
    };
}