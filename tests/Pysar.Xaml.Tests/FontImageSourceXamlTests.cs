using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class FontImageSourceXamlTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\"";

    [Fact]
    public void PropertyElement_SetsAllFourProperties()
    {
        var image = XamlTestHost.BuildElement<Image>(
            $"<Image {Root}><Image.Source><FontImageSource Glyph=\"A\" FontFamily=\"FontAwesomeSolid\" Size=\"44\" Color=\"#339933\"/></Image.Source></Image>");

        var source = Assert.IsType<FontImageSource>(image.Source);
        Assert.Equal("A", source.Glyph);
        Assert.Equal("FontAwesomeSolid", source.FontFamily);
        Assert.Equal(44f, source.Size);
        Assert.Equal(Color.FromHex("#339933"), source.Color);
    }

    [Fact]
    public void PropertyElement_GlyphAndColorBindings_ResolveFromImageContext()
    {
        var image = XamlTestHost.BuildElement<Image>(
            $"<Image {Root}><Image.Source><FontImageSource Glyph=\"{{Binding StatusIcon}}\" Color=\"{{Binding StatusColor}}\" FontFamily=\"FontAwesomeSolid\" Size=\"12\"/></Image.Source></Image>");

        image.DataContext = new { StatusIcon = "\uf00c", StatusColor = "#339933" };
        new Pysar.Binding.BindingEngine().ResolveBindings([image]);

        var source = Assert.IsType<FontImageSource>(image.Source);
        Assert.Equal("\uf00c", source.Glyph);
        Assert.Equal(Color.FromHex("#339933"), source.Color);
    }

    [Fact]
    public void PropertyElement_Triggers_ApplyColorFromDeliveryStatus()
    {
        var report = ReportXaml.Load(
            $"<Report {Root}><PageHeaderBand Height=\"40\"><Image>" +
            "<Image.Source><FontImageSource FontFamily=\"FontAwesomeSolid\" Glyph=\"A\" Size=\"10\" Color=\"Gray\">" +
            "<FontImageSource.Triggers>" +
            "<DataTrigger Binding=\"{Binding DeliveryStatus}\" CompareType=\"Equal\" Value=\"Delivered\">" +
            "<Setter Member=\"Color\" Value=\"#339933\" />" +
            "</DataTrigger>" +
            "</FontImageSource.Triggers>" +
            "</FontImageSource></Image.Source></Image></PageHeaderBand></Report>");

        report.DataContext = new { DeliveryStatus = "Delivered" };
        report.Build();

        var source = Assert.IsType<FontImageSource>(
            report.PageHeader!.Children.OfType<Image>().Single().Source);
        Assert.Equal(Color.FromHex("#339933"), source.Color);
    }
}
