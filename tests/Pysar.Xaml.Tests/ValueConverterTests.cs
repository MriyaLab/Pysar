using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ValueConverterTests
{
    [Theory]
    [InlineData("30")]
    public void Convert_Thickness_Uniform(string s)
        => Assert.Equal(new Thickness(30), (Thickness)XamlValueConverter.Convert(s, typeof(Thickness))!);

    [Fact]
    public void Convert_Thickness_FourValues()
        => Assert.Equal(new Thickness(10, 20, 30, 40), (Thickness)XamlValueConverter.Convert("10,20,30,40", typeof(Thickness))!);

    [Fact]
    public void Convert_SizeLength_KeywordsAndFixed()
    {
        Assert.True(((SizeLength)XamlValueConverter.Convert("Fill", typeof(SizeLength))!).IsFill);
        Assert.True(((SizeLength)XamlValueConverter.Convert("Auto", typeof(SizeLength))!).IsAuto);
        var fixed60 = (SizeLength)XamlValueConverter.Convert("60", typeof(SizeLength))!;
        Assert.True(fixed60.IsFixed);
        Assert.Equal(60f, fixed60.Value);
    }

    [Fact]
    public void Convert_Size_WidthHeight()
    {
        var size = (Size)XamlValueConverter.Convert("Fill,60", typeof(Size))!;
        Assert.True(size.Width.IsFill);
        Assert.Equal(60f, size.Height.Value);
    }

    [Fact]
    public void Convert_Color_Hex() =>
        Assert.Equal(Color.FromHex("#ECECEC"), (Color)XamlValueConverter.Convert("#ECECEC", typeof(Color))!);

    [Theory]
    [InlineData("Azure")]
    [InlineData("azure")]
    public void Convert_Color_Name(string value) =>
        Assert.Equal(Colors.Azure, (Color)XamlValueConverter.Convert(value, typeof(Color))!);

    [Fact]
    public void Convert_Enum_ByName() =>
        Assert.Equal(PageSize.A4, (PageSize)XamlValueConverter.Convert("A4", typeof(PageSize))!);

    [Fact]
    public void Convert_Primitive_Int() =>
        Assert.Equal(4, (int)XamlValueConverter.Convert("4", typeof(int))!);

    [Fact]
    public void Convert_Position_XY()
    {
        var pos = (Position)XamlValueConverter.Convert("70,39", typeof(Position))!;
        Assert.Equal(70f, pos.X);
        Assert.Equal(39f, pos.Y);
    }

    [Fact]
    public void Convert_ImageSource_RelativePath()
    {
        var source = Assert.IsType<FileImageSource>(
            XamlValueConverter.Convert("Images/logo.svg", typeof(ImageSource)));

        Assert.Equal("Images/logo.svg", source.FilePath);
    }

    [Fact]
    public void Convert_ImageSource_HttpUri()
    {
        var source = Assert.IsType<UriImageSource>(
            XamlValueConverter.Convert("https://example.com/logo.svg", typeof(ImageSource)));

        Assert.Equal("https://example.com/logo.svg", source.Uri!.OriginalString);
    }
}
