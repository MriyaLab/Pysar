using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public sealed class ReportResourcesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public ReportResourcesTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "Colors.rxaml"),
            """
            <ResourceDictionary xmlns="https://mriyalab.com/pysar" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Color x:Key="Accent">#C0392B</Color>
              <Color x:Key="DarkGray">#3E4351</Color>
            </ResourceDictionary>
            """);
        File.WriteAllText(Path.Combine(_directory, "Styles.rxaml"),
            """
            <ResourceDictionary xmlns="https://mriyalab.com/pysar" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Colors.rxaml" />
              </ResourceDictionary.MergedDictionaries>
              <Style TargetType="Text">
                <Setter Member="FontSize" Value="14" />
                <Setter Member="FontColor" Value="{StaticResource DarkGray}" />
              </Style>
              <Style x:Key="H1" TargetType="Text">
                <Setter Member="FontSize" Value="38" />
                <Setter Member="FontStyle" Value="Bold" />
              </Style>
            </ResourceDictionary>
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void LoadFile_MergesAccentAndH1()
    {
        var resources = ReportResources.LoadFile(Path.Combine(_directory, "Styles.rxaml"));

        Assert.Equal(Color.FromHex("#C0392B"), resources["Accent"]);
        Assert.IsType<Style>(resources["H1"]);
    }

    [Fact]
    public void LoadFile_StoresImplicitStyleUnderTargetType()
    {
        var resources = ReportResources.LoadFile(Path.Combine(_directory, "Styles.rxaml"));

        Assert.True(resources.ContainsKey(typeof(Text)));
        var style = Assert.IsType<Style>(resources[typeof(Text)]);
        Assert.Equal(typeof(Text), style.TargetType);
    }

    [Fact]
    public void LoadFile_ResolvesSetterStaticResourceToColor()
    {
        var resources = ReportResources.LoadFile(Path.Combine(_directory, "Styles.rxaml"));
        var style = Assert.IsType<Style>(resources[typeof(Text)]);
        var fontColor = style.Setters.Single(s => s.Member == nameof(Text.FontColor)).Value;

        Assert.Equal(Color.FromHex("#3E4351"), fontColor);
    }

    [Fact]
    public void LoadFile_NonDictionaryRoot_ThrowsXamlException()
    {
        var path = Path.Combine(_directory, "Report.rxaml");
        File.WriteAllText(path,
            """
            <Report xmlns="https://mriyalab.com/pysar" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """);

        var exception = Assert.Throws<XamlException>(() => ReportResources.LoadFile(path));

        Assert.Contains("ResourceDictionary", exception.Message);
    }

    [Fact]
    public void LoadFile_KeylessStyleWithoutTargetType_ThrowsXamlException()
    {
        var path = Path.Combine(_directory, "Bad.rxaml");
        File.WriteAllText(path,
            """
            <ResourceDictionary xmlns="https://mriyalab.com/pysar" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Style>
                <Setter Member="FontSize" Value="14" />
              </Style>
            </ResourceDictionary>
            """);

        Assert.Throws<XamlException>(() => ReportResources.LoadFile(path));
    }

    [Fact]
    public void LoadFile_MissingFile_ThrowsXamlException()
    {
        var path = Path.Combine(_directory, "Missing.rxaml");

        var exception = Assert.Throws<XamlException>(() => ReportResources.LoadFile(path));

        Assert.Contains(path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
