using Pysar.Core;
using Pysar.Skia;
using Xunit;

namespace Pysar.Wpf.Tests;

/// <summary>
///     UsePysar has to install the fallback chain, not WpfAssetFileSystem on its own - otherwise an
///     asset embedded by a referenced report library is unreachable on WPF, which is the case the
///     ReportAsset item type exists for.
/// </summary>
[Collection(WpfCollection.Name)]
public class WpfAssetChainTests(WpfSession session)
{
    [Fact]
    public void UsePysar_InstallsTheEmbeddedFallbackBehindTheApplicationsOwnResources()
        => session.Run(() => Assert.IsType<FallbackFileSystem>(ReportPlatformHandler.FileSystem));
}
