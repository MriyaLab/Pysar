using Pysar.Viewer.Tiles;
using Xunit;

namespace Pysar.Uno.Tests;

public class ReportViewSchedulerTests
{
    [Fact]
    public void CreateRenderScheduler_WhenNotBrowser_UsesTaskRunScheduler()
        => Assert.IsType<TaskRunScheduler>(ReportView.CreateRenderScheduler(isBrowser: false));

    [Fact]
    public void CreateRenderScheduler_WhenBrowser_UsesYieldingScheduler()
        => Assert.IsType<YieldingRenderScheduler>(ReportView.CreateRenderScheduler(isBrowser: true));
}
