using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class PropertyElementTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\"";

    [Fact]
    public void PropertyElement_Shorthand_BuildsAndSetsValueType()
    {
        // <Text.Font Family="Ubuntu" Size="9" Style="Bold"/> builds a Font and assigns Text.Font.
        var text = XamlTestHost.BuildElement<Text>(
            $"<Text {Root}><Text.Font Family=\"Ubuntu\" Size=\"9\" Style=\"Bold\"/></Text>");
        Assert.Equal("Ubuntu", text.Font.Family);
        Assert.Equal(9, text.Font.Size);
    }

    [Fact]
    public void PropertyElement_ContainerSlot_WithContent()
    {
        // DetailHeader is a polymorphic slot (any element), so the container is authored explicitly:
        // <DetailBand.DetailHeader><Frame BackgroundColor="#2C3E50"><Text Content="H"/></Frame></DetailBand.DetailHeader>
        var detail = XamlTestHost.BuildElement<DetailBand>(
            $"<DetailBand {Root}><DetailBand.DetailHeader><Frame BackgroundColor=\"#2C3E50\"><Text Content=\"H\"/></Frame></DetailBand.DetailHeader></DetailBand>");
        var header = (Frame)detail.DetailHeader!;
        Assert.Equal(Color.FromHex("#2C3E50"), header.BackgroundColor);
        Assert.Equal("H", ((Text)header.Children[0]).Content);
    }

    [Fact]
    public void PropertyElement_ContainerSlot_AcceptsNonFrame()
    {
        // The slot is polymorphic: a Grid header is valid, not only a Frame.
        var detail = XamlTestHost.BuildElement<DetailBand>(
            $"<DetailBand {Root}><DetailBand.DetailHeader><Grid ColumnDefinitions=\"*,*\"/></DetailBand.DetailHeader></DetailBand>");
        Assert.IsType<Grid>(detail.DetailHeader);
    }
}
