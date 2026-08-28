using Pysar.Binding;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

/// <summary>
///     Covers <see cref="Report.OnPageChangedAsync"/>: the hook a code-behind report overrides to touch
///     its named elements per page, for anything the page-number bindings cannot express.
/// </summary>
public class PageChangedHookTests
{
    /// <summary>
    ///     A report that records every page it was notified about and writes the number into a footer
    ///     Text carrying no binding at all — so the test also proves the hook survives the resolver's
    ///     no-bindings fast path.
    /// </summary>
    private sealed class RecordingReport : Report
    {
        public List<int> Pages { get; } = [];
        public List<int> CountsSeen { get; } = [];
        public Text Label { get; } = new() { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };

        protected override Task OnPageChangedAsync(int pageNumber, CancellationToken ct)
        {
            Pages.Add(pageNumber);
            CountsSeen.Add(PageCount);
            Label.Content = $"p{pageNumber}";
            return Task.CompletedTask;
        }
    }

    private static RecordingReport BuildMultiPageReport()
    {
        var design = new RecordingReport
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };
        design.Bands.Add(new PageFooterBand { Size = new Size(SizeLength.Fill, SizeLength.Fixed(20)) });
        design.PageFooter!.AddElement(design.Label);
        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) });

        design.Build();
        return design;
    }

    [Fact]
    public async Task Render_CallsTheHookOncePerPageInOrder()
    {
        var design = BuildMultiPageReport();

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.InRange(pages.Count, 3, int.MaxValue);
        // Exactly one call per page — the seeding measurement must not produce an extra one.
        Assert.Equal(Enumerable.Range(1, pages.Count), design.Pages);
    }

    [Fact]
    public async Task Render_HookSeesTheRealPageCount()
    {
        var design = BuildMultiPageReport();

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        // Never the seed value of 1 — the hook runs after pagination, so the total is known.
        Assert.Equal(Enumerable.Repeat(pages.Count, pages.Count), design.CountsSeen);
    }

    [Fact]
    public async Task Render_HookEditsAreDrawn()
    {
        var design = BuildMultiPageReport();
        var seen = new List<string>();
        var registry = DrawerRegistry.CreateDefault();
        registry.Register<Text>(new ContentRecorder(design.Label, seen));

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None, registry);

        // What the hook wrote is what reached the canvas, page by page.
        Assert.Equal(Enumerable.Range(1, pages.Count).Select(n => $"p{n}"), seen);
    }

    /// <summary>
    ///     Overwrites a Text that is <i>also</i> bound, which is the only way to pin the hook's position
    ///     in the sequence: run it before the bindings resolve and the binding wins instead.
    /// </summary>
    private sealed class OverwritingReport : Report
    {
        public Text Bound { get; } = new() { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };

        protected override Task OnPageChangedAsync(int pageNumber, CancellationToken ct)
        {
            Bound.Content = $"hook{pageNumber}";
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Render_HookRunsAfterBindingResolution_SoItsValueWins()
    {
        var design = new OverwritingReport
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };
        design.Bands.Add(new PageFooterBand { Size = new Size(SizeLength.Fill, SizeLength.Fixed(20)) });
        design.PageFooter!.AddElement(design.Bound);
        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) });
        design.Build();
        design.Bound.SetBinding(Text.ContentProperty, new BindingInfo("PageNumber", source: design));

        var seen = new List<string>();
        var registry = DrawerRegistry.CreateDefault();
        registry.Register<Text>(new ContentRecorder(design.Bound, seen));

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None, registry);

        // The binding would have written "1", "2", "3"; the hook runs later, so its value is drawn.
        Assert.Equal(Enumerable.Range(1, pages.Count).Select(n => $"hook{n}"), seen);
    }

    [Fact]
    public async Task RenderToPdf_CallsTheHookToo()
    {
        var design = BuildMultiPageReport();

        using var stream = new MemoryStream();
        await PageRenderer.RenderToPdfAsync(design, stream, CancellationToken.None);

        Assert.InRange(design.Pages.Count, 3, int.MaxValue);
        Assert.Equal(Enumerable.Range(1, design.Pages.Count), design.Pages);
    }

    [Fact]
    public void WithPageChanged_Null_Throws()
    {
        var report = new Report();
        Assert.Throws<ArgumentNullException>(() => report.WithPageChanged(null!));
    }

    [Fact]
    public async Task Render_WithoutAnOverride_StillRenders()
    {
        // The default implementation is a no-op; a plain report keeps the cheap path.
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)))
            .WithDetail(b => b.AddElement(
                new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) }))
            .Build();

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.InRange(pages.Count, 3, int.MaxValue);
    }

    private static Report BuildCallbackReport(Text label, Action<int, int> onPage)
    {
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };
        design.Bands.Add(new PageFooterBand { Size = new Size(SizeLength.Fill, SizeLength.Fixed(20)) });
        design.PageFooter!.AddElement(label);
        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) });
        design.WithPageChanged(onPage);
        return design.Build();
    }

    [Fact]
    public async Task Render_Callback_OncePerPageInOrder()
    {
        var label = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        var pagesSeen = new List<int>();
        var design = BuildCallbackReport(label, (number, _) =>
        {
            pagesSeen.Add(number);
            label.Content = $"p{number}";
        });

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.InRange(pages.Count, 3, int.MaxValue);
        Assert.Equal(Enumerable.Range(1, pages.Count), pagesSeen);
    }

    [Fact]
    public async Task Render_Callback_SeesRealPageCount()
    {
        var label = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        var countsSeen = new List<int>();
        var design = BuildCallbackReport(label, (_, count) => countsSeen.Add(count));

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.Equal(Enumerable.Repeat(pages.Count, pages.Count), countsSeen);
    }

    [Fact]
    public async Task Render_Callback_EditsAreDrawn()
    {
        var label = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        var design = BuildCallbackReport(label, (number, _) => label.Content = $"p{number}");
        var seen = new List<string>();
        var registry = DrawerRegistry.CreateDefault();
        registry.Register<Text>(new ContentRecorder(label, seen));

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None, registry);

        Assert.Equal(Enumerable.Range(1, pages.Count).Select(n => $"p{n}"), seen);
    }

    [Fact]
    public async Task Render_Callback_ReplacesPreviousHandler()
    {
        var label = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        var first = 0;
        var second = 0;
        var design = new Report
        {
            PageFormat = new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 }
        };
        design.Bands.Add(new PageFooterBand { Size = new Size(SizeLength.Fill, SizeLength.Fixed(20)) });
        design.PageFooter!.AddElement(label);
        design.Detail.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) });
        design.WithPageChanged((_, _) => first++);
        design.WithPageChanged((_, _) => second++);
        design.Build();

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None);

        Assert.Equal(0, first);
        Assert.Equal(pages.Count, second);
    }

    private sealed class ContentRecorder(Text target, List<string> sink) : IElementDrawer
    {
        private readonly TextDrawer _inner = new();

        public void Draw(Pysar.Skia.Layout.LayoutNode node, RenderContext ctx)
        {
            if (ReferenceEquals(node.Element, target))
                sink.Add(((Text)node.Element).Content);

            _inner.Draw(node, ctx);
        }
    }
}
