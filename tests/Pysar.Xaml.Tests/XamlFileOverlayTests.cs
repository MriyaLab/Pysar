using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public sealed class XamlFileOverlayTests : IDisposable
{
    private const string Root =
        "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public XamlFileOverlayTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void TryOpen_WithoutPush_ReturnsFalse()
    {
        Assert.False(XamlFileOverlay.TryOpen(Path.Combine(_directory, "missing.rxaml"), out var stream));
        Assert.Null(stream);
    }

    [Fact]
    public void TryOpen_UsesOverlayInsteadOfDisk()
    {
        var path = Path.Combine(_directory, "Colors.rxaml");
        File.WriteAllText(path, "from-disk");

        using (XamlFileOverlay.Push(new Dictionary<string, string>
        {
            [path] = "from-buffer",
        }))
        {
            Assert.True(XamlFileOverlay.TryOpen(path, out var stream));
            using (stream)
            using (var reader = new StreamReader(stream!))
                Assert.Equal("from-buffer", reader.ReadToEnd());
        }
    }

    [Fact]
    public void Push_IsClearedOnDispose()
    {
        var path = Path.Combine(_directory, "Colors.rxaml");
        using (XamlFileOverlay.Push(new Dictionary<string, string> { [path] = "x" }))
        {
        }

        Assert.False(XamlFileOverlay.TryOpen(path, out _));
    }

    [Fact]
    public void TryOpen_MatchesFullPathIgnoreCase()
    {
        var path = Path.Combine(_directory, "Colors.rxaml");
        using (XamlFileOverlay.Push(new Dictionary<string, string> { [path] = "x" }))
        {
            var alt = Path.Combine(_directory, "colors.rxaml");
            Assert.True(XamlFileOverlay.TryOpen(alt, out var stream));
            stream!.Dispose();
        }
    }

    [Fact]
    public void LoadInto_PrefersOverlayOverDiskForMergedDictionary()
    {
        var diskPath = Path.Combine(_directory, "Colors.rxaml");
        File.WriteAllText(
            diskPath,
            $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#111111</Color></ResourceDictionary>");

        var xaml = $"<Report {Root}>"
                   + "<Report.Resources><ResourceDictionary Source=\"Colors.rxaml\" /></Report.Resources>"
                   + "<PageHeaderBand BackgroundColor=\"{StaticResource Brand}\"/>"
                   + "</Report>";

        var overlay = $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#C0392B</Color></ResourceDictionary>";
        var report = new Report();

        using (XamlFileOverlay.Push(new Dictionary<string, string> { [diskPath] = overlay }))
            ReportXaml.LoadInto(report, xaml, _directory);

        Assert.Equal(Color.FromHex("#C0392B"), report.PageHeader!.BackgroundColor);
    }

    [Fact]
    public void LoadInto_OverlayWorksWhenDiskFileIsMissing()
    {
        var missing = Path.Combine(_directory, "Colors.rxaml");
        var overlay = $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#C0392B</Color></ResourceDictionary>";
        var xaml = $"<Report {Root}>"
                   + "<Report.Resources><ResourceDictionary Source=\"Colors.rxaml\" /></Report.Resources>"
                   + "<PageHeaderBand BackgroundColor=\"{StaticResource Brand}\"/>"
                   + "</Report>";
        var report = new Report();

        using (XamlFileOverlay.Push(new Dictionary<string, string> { [missing] = overlay }))
            ReportXaml.LoadInto(report, xaml, _directory);

        Assert.Equal(Color.FromHex("#C0392B"), report.PageHeader!.BackgroundColor);
    }

    [Fact]
    public void LoadFile_PrefersOverlay()
    {
        var path = Path.Combine(_directory, "Colors.rxaml");
        File.WriteAllText(
            path,
            $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#111111</Color></ResourceDictionary>");
        var overlay = $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">#C0392B</Color></ResourceDictionary>";

        using (XamlFileOverlay.Push(new Dictionary<string, string> { [path] = overlay }))
        {
            var resources = ReportResources.LoadFile(path);
            Assert.Equal(Color.FromHex("#C0392B"), resources["Brand"]);
        }
    }
}
