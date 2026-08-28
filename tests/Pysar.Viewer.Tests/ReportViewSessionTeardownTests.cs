using Pysar.Viewer.Tiles;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using Xunit;

namespace Pysar.Viewer.Tests;

/// <summary>
///     Teardown for a control that is destroyed without ever loading another report. Every host's
///     detach hook also fires when a control is merely reparented, so the deciding question is not
///     "was it detached" but "is it still detached once this turn of the user-interface loop is over".
/// </summary>
public class ReportViewSessionTeardownTests
{
    private static readonly SkiaReportRenderer Renderer = new();

    private static Report AReport()
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };

        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(200)) });
        design.Build();

        return design;
    }

    private static async Task<(FakeHost Host, ReportViewSession Session)> LoadedSubject()
    {
        var host = new FakeHost { DeferPosts = true };
        var session = new ReportViewSession(new ReportViewPresenter(host), host, new TaskRunScheduler());

        await session.LoadAsync(AReport(), Renderer);
        host.RunPosted();

        Assert.NotNull(session.Tiles);

        return (host, session);
    }

    [Fact]
    public async Task DisposeWhenStillDetached_DisposesAViewThatNeverComesBack()
    {
        var (host, session) = await LoadedSubject();

        session.DisposeWhenStillDetached(() => false);
        host.RunPosted();

        Assert.Null(session.Tiles);
    }

    [Fact]
    public async Task DisposeWhenStillDetached_KeepsTheCacheOfAViewThatWasReparented()
    {
        var (host, session) = await LoadedSubject();
        var tiles = session.Tiles;

        // Reparenting detaches and reattaches inside one turn: by the time the posted check runs the
        // control is back, and tearing its cache down here would make it redraw every cell for
        // nothing.
        session.DisposeWhenStillDetached(() => true);
        host.RunPosted();

        Assert.Same(tiles, session.Tiles);
    }

    [Fact]
    public async Task Dispose_IsSafeToCallTwice()
    {
        var (_, session) = await LoadedSubject();

        session.Dispose();
        session.Dispose();

        Assert.Null(session.Tiles);
    }
}
