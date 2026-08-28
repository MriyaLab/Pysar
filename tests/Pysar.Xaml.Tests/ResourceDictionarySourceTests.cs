using System.Diagnostics.CodeAnalysis;
using System.Text;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Xaml.Tests;

/// <summary>
///     A compiled report carries the absolute directory it was built from, which a packaged
///     application cannot read - so a merged dictionary resolves through the platform file system
///     first, under the path exactly as authored, and falls back to that directory afterwards.
/// </summary>
public sealed class ResourceDictionarySourceTests : IDisposable
{
    private const string Root =
        "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    public void Dispose() => ReportPlatformHandler.Create(new FakePlatformHandler());

    [Fact]
    public void Source_IsReadFromThePackage_WhenTheDocumentHasNoBaseDirectory()
    {
        InstallPackage(("Styles/PackageStyles.xaml", Dictionary("#2C3E50")));

        var design = ReportXaml.Load(ReportUsing("Styles/PackageStyles.xaml"));

        Assert.Equal(Color.FromHex("#2C3E50"), design.Resources["Brand"]);
    }

    [Fact]
    public void NestedSource_ResolvesAgainstTheContainingDictionaryDirectory()
    {
        InstallPackage(
            ("Styles/PackageStyles.xaml",
                $"<ResourceDictionary {Root}><ResourceDictionary.MergedDictionaries>"
                + "<ResourceDictionary Source=\"PackageColors.xaml\" />"
                + "</ResourceDictionary.MergedDictionaries></ResourceDictionary>"),
            ("Styles/PackageColors.xaml", Dictionary("#1A2B3C")));

        var design = ReportXaml.Load(ReportUsing("Styles/PackageStyles.xaml"));

        Assert.Equal(Color.FromHex("#1A2B3C"), design.Resources["Brand"]);
    }

    [Fact]
    public void SourceClimbingAboveTheDocument_IsResolvedFromThePackageRoot()
    {
        // A component in Views/ merges "../Styles/…". The package has no parent of its own root,
        // so the leading segment is dropped and the asset resolves at "Styles/…".
        InstallPackage(("Styles/PackageStyles.xaml", Dictionary("#0A0B0C")));

        var design = ReportXaml.Load(ReportUsing("../Styles/PackageStyles.xaml"));

        Assert.Equal(Color.FromHex("#0A0B0C"), design.Resources["Brand"]);
    }

    [Fact]
    public void Package_WinsOverTheDirectoryTheReportWasLoadedFrom()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(directory, "Styles"));
        File.WriteAllText(Path.Combine(directory, "Styles", "PackageStyles.xaml"), Dictionary("#FF0000"));
        var reportPath = Path.Combine(directory, "Report.xaml");
        File.WriteAllText(reportPath, ReportUsing("Styles/PackageStyles.xaml"));

        InstallPackage(("Styles/PackageStyles.xaml", Dictionary("#00FF00")));

        var design = ReportXaml.LoadFile(reportPath);

        Assert.Equal(Color.FromHex("#00FF00"), design.Resources["Brand"]);
    }

    [Fact]
    public void Disk_IsUsed_WhenThePackageDoesNotCarryTheDictionary()
    {
        var directory = CreateTempDirectory();
        Directory.CreateDirectory(Path.Combine(directory, "Styles"));
        File.WriteAllText(Path.Combine(directory, "Styles", "PackageStyles.xaml"), Dictionary("#FF0000"));
        var reportPath = Path.Combine(directory, "Report.xaml");
        File.WriteAllText(reportPath, ReportUsing("Styles/PackageStyles.xaml"));

        InstallPackage();

        var design = ReportXaml.LoadFile(reportPath);

        Assert.Equal(Color.FromHex("#FF0000"), design.Resources["Brand"]);
    }

    [Fact]
    public void MissingFromBothRoutes_Throws()
    {
        InstallPackage();

        var exception = Assert.Throws<XamlException>(
            () => ReportXaml.Load(ReportUsing("Styles/Absent.xaml")));

        Assert.Contains("Styles/Absent.xaml", exception.Message);
    }

    [Fact]
    public void Source_AsyncOnlyFileSystem_ThrowsXamlException()
    {
        InstallAsyncOnlyPackage(("Styles/PackageStyles.xaml", Dictionary("#2C3E50")));

        var ex = Assert.Throws<XamlException>(
            () => ReportXaml.Load(ReportUsing("Styles/PackageStyles.xaml")));

        Assert.Contains("ISyncFileSystem", ex.Message, StringComparison.Ordinal);
    }

    private static string Dictionary(string color)
        => $"<ResourceDictionary {Root}><Color x:Key=\"Brand\">{color}</Color></ResourceDictionary>";

    private static string ReportUsing(string source)
        => $"<Report {Root}>"
           + $"<Report.Resources><ResourceDictionary Source=\"{source}\" /></Report.Resources>"
           + "<PageHeaderBand />"
           + "</Report>";

    private static void InstallPackage(params (string Path, string Content)[] files)
        => ReportPlatformHandler.Create(new FakePlatformHandler(files));

    private static void InstallAsyncOnlyPackage(params (string Path, string Content)[] files)
        => ReportPlatformHandler.Create(new FakeAsyncOnlyPlatformHandler(files));

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakePlatformHandler : IReportPlatformHandler
    {
        public FakePlatformHandler(params (string Path, string Content)[] files)
            => FileSystem = new FakePackageFileSystem(files);

        public IFileSystem FileSystem { get; }

        public IFontCollection FontCollection { get; } = new FakeFontCollection();
    }

    private sealed class FakePackageFileSystem : IFileSystem, ISyncFileSystem
    {
        private readonly Dictionary<string, string> _files;

        public FakePackageFileSystem((string Path, string Content)[] files)
            => _files = files.ToDictionary(file => file.Path, file => file.Content);

        public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(ReadFile(filePath));

        public byte[]? ReadFile(string filePath)
            => _files.TryGetValue(filePath, out var content) ? Encoding.UTF8.GetBytes(content) : null;

        public bool Exists([NotNullWhen(true)] string? filePath)
            => filePath is not null && _files.ContainsKey(filePath);
    }

    private sealed class FakeFontCollection : Dictionary<string, object>, IFontCollection
    {
        public IFontCollection AddFont(string filename, string? alias = null, FontStyle fontStyle = FontStyle.Normal)
            => this;
    }

    private sealed class FakeAsyncOnlyPlatformHandler : IReportPlatformHandler
    {
        public FakeAsyncOnlyPlatformHandler(params (string Path, string Content)[] files)
            => FileSystem = new FakeAsyncOnlyFileSystem(files);

        public IFileSystem FileSystem { get; }

        public IFontCollection FontCollection { get; } = new FakeFontCollection();
    }

    private sealed class FakeAsyncOnlyFileSystem : IFileSystem
    {
        private readonly Dictionary<string, string> _files;

        public FakeAsyncOnlyFileSystem((string Path, string Content)[] files)
            => _files = files.ToDictionary(file => file.Path, file => file.Content);

        public Task<byte[]?> ReadFileAsync(string filePath) => Task.FromResult(Read(filePath));

        public bool Exists([NotNullWhen(true)] string? filePath)
            => filePath is not null && _files.ContainsKey(filePath);

        private byte[]? Read(string filePath)
            => _files.TryGetValue(filePath, out var content) ? Encoding.UTF8.GetBytes(content) : null;
    }
}
