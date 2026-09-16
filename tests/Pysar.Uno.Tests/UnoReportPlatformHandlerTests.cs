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

        // FileSystem is the fallback chain - the application's own packaged assets first, then
        // whatever a referenced report library embedded - not Assets alone, so an embedded asset
        // stays reachable at render time. Proving that behaviourally: a path Assets already
        // resolves must come back identical through the chain, not merely asserting the chain's
        // type. Assets itself stays the concrete UnoAssetFileSystem, which is a real API claim (not
        // an identity proxy) - Application.UsePysarAsync preloads into it by that exact type.
        var expected = handler.Assets.ReadFile("Fonts/Ubuntu-Regular.ttf");

        Assert.NotNull(expected);
        Assert.Equal(expected, ((ISyncFileSystem)handler.FileSystem).ReadFile("Fonts/Ubuntu-Regular.ttf"));
        Assert.IsType<UnoAssetFileSystem>(handler.Assets);
        Assert.NotNull(handler.FontCollection);
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
