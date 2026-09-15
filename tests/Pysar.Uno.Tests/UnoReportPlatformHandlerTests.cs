using System.Reflection;
using Pysar.Core.Abstractions;
using Xunit;

// Why this project covers no control behaviour: it links Uno.WinUI's reference assemblies, since a
// plain net10.0 test host loads no Uno head. Touching any Uno type therefore throws
// "System.NotSupportedException : Ref assembly" at runtime - verified by trying to construct a
// ReportView here, which fails on that exact message. Everything about the view that can be tested
// without a framework already lives in Pysar.Viewer.Tests; the rest needs a running host.
namespace Pysar.Uno.Tests;

public class UnoReportPlatformHandlerTests
{
    private static UnoReportPlatformHandler CreateHandler()
        => new(Assembly.GetExecutingAssembly());

    [Fact]
    public void ExposesTheTwoThingsRenderingNeeds()
    {
        var handler = CreateHandler();

        Assert.IsType<UnoAssetFileSystem>(handler.FileSystem);
        Assert.NotNull(handler.FontCollection);

        // One object behind both properties, not two file systems over the same assembly:
        // Application.UsePysarAsync preloads into Assets while rendering reads through FileSystem,
        // so a second instance would leave the preloaded content unreachable at render time.
        Assert.Same(handler.Assets, handler.FileSystem);
    }

    [Fact]
    public void RegistersAFontFromThePackagedAssets()
    {
        // The end the file system exists for: SkiaFontCollection reads through ISyncFileSystem, so
        // this throws if the file system ever stops offering one.
        var handler = CreateHandler();

        handler.FontCollection.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");
    }

    [Fact]
    public void AMissingFontIsReportedRatherThanIgnored()
        => Assert.Throws<FileNotFoundException>(
            () => CreateHandler().FontCollection.AddFont("Fonts/NoSuchFont.ttf", "Nope"));

    [Fact]
    public void RejectsNull()
        => Assert.Throws<ArgumentNullException>(() => new UnoReportPlatformHandler(null!));

    [Fact]
    public void IsAPlatformHandler()
        => Assert.IsAssignableFrom<IReportPlatformHandler>(CreateHandler());
}
