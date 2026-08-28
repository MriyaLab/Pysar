using System.Collections.Concurrent;
using Pysar.Binding;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests;

public class ReportRenderSessionTests
{
    [Fact]
    public async Task TwoRegions_CanRenderConcurrently()
    {
        // A slow Text drawer proves two RenderRegionAsync calls overlap in DrawPage rather than
        // queueing on a session-wide gate.
        var probe = new ConcurrentProbeDrawer(hold: TimeSpan.FromMilliseconds(80));
        var drawers = DrawerRegistry.CreateDefault();
        drawers.Register<Text>(probe);

        var session = await ReportRenderSession.CreateAsync(BuildReport("One"), drawers);

        // Real viewers pump tiles from a worker pool; start both regions on their own dedicated
        // threads so a still-serial session cannot complete one draw before the other begins.
        // Task.Run would route through the shared ThreadPool, whose ramp-up throttles how fast a
        // second thread is injected - on a CI runner with few cores that can leave both regions
        // queued onto the one thread already warm, serializing them and failing this assert for a
        // reason that has nothing to do with the session's actual concurrency. LongRunning avoids
        // that: it always creates a fresh thread immediately.
        // Both regions overlap the top-left text (margin 30) so culling still invokes the Text
        // drawer on each thread; a tile that misses the text would correctly skip the probe.
        var bitmaps = await Task.WhenAll(
            Task.Factory.StartNew(
                () => session.RenderRegionAsync(0, new SKRect(0, 0, 120, 120), scale: 1f),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap(),
            Task.Factory.StartNew(
                () => session.RenderRegionAsync(0, new SKRect(20, 20, 140, 140), scale: 1f),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap());

        try
        {
            Assert.All(bitmaps, b =>
            {
                Assert.True(b.Width > 0);
                Assert.True(b.Height > 0);
            });
            Assert.True(probe.MaxInFlight >= 2,
                $"Expected concurrent region draws; max in-flight drawers was {probe.MaxInFlight}");
        }
        finally
        {
            foreach (var bitmap in bitmaps)
                bitmap.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentPages_KeepTheirOwnPageNumbers()
    {
        // Freeze must snapshot resolved footer content; otherwise the second Resolve overwrites the
        // live Text before the first page finishes drawing.
        var seen = new ConcurrentBag<string>();
        var drawers = DrawerRegistry.CreateDefault();
        drawers.Register<Text>(new ContentRecordingDrawer(seen));

        var footerText = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        var design = ReportBuilder.Create("pages")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).AddElement(footerText))
            .WithDetail(b => b.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) }))
            .Build();
        footerText.SetBinding(Text.ContentProperty, new BindingInfo("PageNumber", source: design));

        var session = await ReportRenderSession.CreateAsync(design, drawers);
        Assert.True(session.PageCount >= 2);

        var page = session.PageSizePt;
        var region = new SKRect(0, 0, page.Width, page.Height);
        var bitmaps = await Task.WhenAll(
            Task.Run(() => session.RenderRegionAsync(0, region, scale: 1f)),
            Task.Run(() => session.RenderRegionAsync(1, region, scale: 1f)));

        foreach (var bitmap in bitmaps)
            bitmap.Dispose();

        Assert.Contains("1", seen);
        Assert.Contains("2", seen);
    }

    /// <summary>Holds the drawing thread so overlapping RenderRegionAsync calls are observable.</summary>
    private sealed class ConcurrentProbeDrawer(TimeSpan hold) : IElementDrawer
    {
        private int _inFlight;

        public int MaxInFlight;

        public void Draw(LayoutNode node, RenderContext ctx)
        {
            var current = Interlocked.Increment(ref _inFlight);
            int observed;
            while (current > (observed = MaxInFlight)
                   && Interlocked.CompareExchange(ref MaxInFlight, current, observed) != observed)
            {
            }

            try
            {
                Thread.Sleep(hold);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private sealed class ContentRecordingDrawer(ConcurrentBag<string> seen) : IElementDrawer
    {
        private readonly TextDrawer _inner = new();

        public void Draw(LayoutNode node, RenderContext ctx)
        {
            if (node.Element is Text text && !string.IsNullOrEmpty(text.Content))
                seen.Add(text.Content);
            _inner.Draw(node, ctx);
        }
    }

    [Fact]
    public async Task PageSize_IsThePageFormatInPoints()
    {
        var session = await ReportRenderSession.CreateAsync(BuildReport("One"));

        Assert.Equal(595.5f, session.PageSizePt.Width, 3);
        Assert.Equal(842f, session.PageSizePt.Height, 3);
    }

    [Fact]
    public async Task PageCount_MatchesAFullRender()
    {
        var report = BuildReport("One");
        var renderer = new SkiaReportRenderer();
        var pages = await renderer.RenderPageAsync(report, 1f);

        var session = await ReportRenderSession.CreateAsync(BuildReport("One"));

        Assert.Equal(pages.Count(), session.PageCount);

        foreach (var page in pages)
            page.Dispose();
    }

    [Fact]
    public async Task RenderRegion_ProducesABitmapOfTheRegionTimesScale()
    {
        var session = await ReportRenderSession.CreateAsync(BuildReport("One"));

        using var bitmap = await session.RenderRegionAsync(0, new SKRect(0, 0, 100, 50), scale: 2f);

        Assert.Equal(200, bitmap.Width);
        Assert.Equal(100, bitmap.Height);
    }

    [Fact]
    public async Task RenderRegion_DrawsTheContentThatFallsInTheRegion()
    {
        var session = await ReportRenderSession.CreateAsync(BuildReport("One"));

        // The text sits at the top left of the content zone; a region around it must contain ink,
        // and a region far below it must be blank paper.
        using var withText = await session.RenderRegionAsync(0, new SKRect(0, 0, 300, 120), scale: 1f);
        using var empty = await session.RenderRegionAsync(0, new SKRect(0, 600, 300, 720), scale: 1f);

        Assert.True(HasInk(withText));
        Assert.False(HasInk(empty));
    }

    [Fact]
    public async Task RenderRegion_MatchesTheSameCropOfAFullPage()
    {
        var report = BuildReport("One");
        var renderer = new SkiaReportRenderer();
        var full = (await renderer.RenderPageAsync(report, 1f)).First();

        var session = await ReportRenderSession.CreateAsync(BuildReport("One"));
        using var region = await session.RenderRegionAsync(0, new SKRect(0, 0, 200, 100), scale: 1f);

        var differences = 0;
        for (var x = 0; x < region.Width; x++)
        for (var y = 0; y < region.Height; y++)
            if (region.GetPixel(x, y) != full.GetPixel(x, y))
                differences++;

        full.Dispose();

        // Anti-aliasing at the clip edge can differ by a pixel or two; the bulk must be identical.
        Assert.True(differences < region.Width * region.Height / 100, $"{differences} pixels differ");
    }

    [Fact]
    public async Task RenderRegion_RejectsAPageOutsideTheReport()
    {
        var session = await ReportRenderSession.CreateAsync(BuildReport("One"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => session.RenderRegionAsync(session.PageCount, new SKRect(0, 0, 10, 10), 1f));
    }

    [Fact]
    public async Task RenderRegion_RejectsAnEmptyRegion()
    {
        var session = await ReportRenderSession.CreateAsync(BuildReport("One"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => session.RenderRegionAsync(0, new SKRect(0, 0, 0, 10), 1f));
    }

    [Fact]
    public async Task SessionFromRenderer_UsesTheRegisteredDrawers()
    {
        var renderer = new SkiaReportRenderer();
        renderer.WithDrawer<Text>(new SpyDrawer());

        var session = await renderer.CreateSessionAsync(BuildReport("One"));
        using var bitmap = await session.RenderRegionAsync(0, new SKRect(0, 0, 300, 120), 1f);

        Assert.True(SpyDrawer.Drew);
    }

    private sealed class SpyDrawer : Pysar.Skia.Rendering.IElementDrawer
    {
        public static bool Drew;

        public void Draw(Pysar.Skia.Layout.LayoutNode node, RenderContext ctx) => Drew = true;
    }

    private static Report BuildReport(string text)
    {
        // ReportBuilder.Build() already calls Report.Build() internally; calling it again throws
        // "This report has already been built."
        return ReportBuilder.Create("Session")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30) })
            .WithDetail(detail => detail.AddElement(new Text { Content = text, Font = new Font { Size = 24 } }))
            .Build();
    }

    private static bool HasInk(SKBitmap bitmap)
    {
        for (var x = 0; x < bitmap.Width; x++)
        for (var y = 0; y < bitmap.Height; y++)
            if (bitmap.GetPixel(x, y) != SKColors.White)
                return true;

        return false;
    }
}
