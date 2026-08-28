using Pysar.Binding;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

/// <summary>
///     End-to-end checks that <see cref="PageRenderer"/> re-resolves the page bands per page.
///     <para>
///     The assertions read the footer's content at <i>draw</i> time through a recording drawer rather
///     than after the render call returns. Reading the element afterwards would only ever observe the
///     final resolution, which cannot tell a correct per-page loop apart from resolving once for the
///     last page, or from hoisting the resolution out of the loop entirely.
///     </para>
/// </summary>
public class PageNumberRenderTests
{
    /// <summary>
    ///     Records what each draw of <paramref name="target"/> actually saw, so the assertions observe
    ///     the per-page sequence instead of the surviving final value. Drawing is delegated to the
    ///     built-in <see cref="TextDrawer"/> so the rest of the pipeline still runs.
    /// </summary>
    private sealed class RecordingTextDrawer(Text target, List<string> content, List<float> deviceY)
        : IElementDrawer
    {
        private readonly TextDrawer _inner = new();

        public void Draw(LayoutNode node, RenderContext ctx)
        {
            if (ReferenceEquals(node.Element, target))
            {
                content.Add(((Text)node.Element).Content);
                // The canvas translation at draw time is where the band was actually placed on the page.
                deviceY.Add(ctx.Canvas.TotalMatrix.TransY);
            }

            _inner.Draw(node, ctx);
        }
    }

    /// <summary>Turns page number N into N space-separated words, so the text wraps onto more lines as N grows.</summary>
    private sealed class RepeatWordsConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter)
            => string.Join(" ", Enumerable.Repeat("page", System.Convert.ToInt32(value ?? 1)));
    }

    private static (DrawerRegistry Registry, List<string> Content, List<float> DeviceY) Recorder(Text target)
    {
        var content = new List<string>();
        var deviceY = new List<float>();
        var registry = DrawerRegistry.CreateDefault();
        registry.Register<Text>(new RecordingTextDrawer(target, content, deviceY));

        return (registry, content, deviceY);
    }

    /// <summary>
    ///     A multi-page report whose page footer holds a single fixed-height Text, either bound to
    ///     <paramref name="bindingPath"/> or carrying the literal <paramref name="content"/>.
    /// </summary>
    private static (Report Design, Text FooterText) BuildMultiPageReport(
        string? bindingPath = null, string content = "")
    {
        var footerText = new Text { Size = new Size(SizeLength.Fill, SizeLength.Fixed(10)) };
        if (bindingPath is null)
            footerText.Content = content;

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(20)).AddElement(footerText))
            .WithDetail(b => b.AddElement(
                new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) }))
            .Build();
        // Bound to the report itself — the object-model spelling of Source={x:Reference Root}.
        if (bindingPath is not null)
            footerText.SetBinding(Text.ContentProperty, new BindingInfo(bindingPath, source: design));

        return (design, footerText);
    }

    /// <summary>
    ///     The same report with an <i>auto-height</i> footer: band and Text both size to content, and the
    ///     Text is narrow enough that extra words wrap onto extra lines — so the footer genuinely measures
    ///     taller as the page number grows. Unbound (<paramref name="bindingPath"/> null) it is the
    ///     single-line baseline.
    /// </summary>
    private static (Report Design, Text FooterText) BuildGrowingFooterReport(
        string? bindingPath = null, string content = "page")
    {
        var footerText = new Text
        {
            Size = new Size(SizeLength.Fixed(40), SizeLength.Auto),
            AutoHeight = true,
            TextTrimming = TextTrimming.WordWrap
        };
        if (bindingPath is null)
            footerText.Content = content;

        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Auto).AddElement(footerText))
            .WithDetail(b => b.AddElement(
                new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(2400)) }))
            .Build();
        if (bindingPath is not null)
            footerText.SetBinding(Text.ContentProperty,
                new BindingInfo(bindingPath, new RepeatWordsConverter(), source: design));

        return (design, footerText);
    }

    [Fact]
    public async Task Render_PageNumberBinding_IsResolvedOncePerPageInOrder()
    {
        var (design, footerText) = BuildMultiPageReport("PageNumber");
        var (registry, content, _) = Recorder(footerText);

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None, registry);

        Assert.InRange(pages.Count, 3, int.MaxValue);
        // One draw per page, each seeing its own number: pins the values and the resolve→draw order.
        Assert.Equal(Enumerable.Range(1, pages.Count).Select(n => n.ToString()), content);
    }

    [Fact]
    public async Task Render_PageCountBinding_IsTheTotalOnEveryPage()
    {
        var (design, footerText) = BuildMultiPageReport("PageCount");
        var (registry, content, _) = Recorder(footerText);

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None, registry);

        Assert.InRange(pages.Count, 3, int.MaxValue);
        // Constant across pages — unlike PageNumber, so the two properties cannot be confused.
        Assert.Equal(Enumerable.Repeat(pages.Count.ToString(), pages.Count), content);
    }

    [Fact]
    public async Task RenderToPdf_ResolvesPageNumbersPerPageToo()
    {
        var (design, footerText) = BuildMultiPageReport("PageNumber");
        var (registry, content, _) = Recorder(footerText);

        using var stream = new MemoryStream();
        await PageRenderer.RenderToPdfAsync(design, stream, CancellationToken.None, registry);

        Assert.InRange(content.Count, 3, int.MaxValue);
        Assert.Equal(Enumerable.Range(1, content.Count).Select(n => n.ToString()), content);
        Assert.True(stream.Length > 0, "the PDF document should have been written");
    }

    [Fact]
    public async Task Render_FooterThatGrowsWithThePageNumber_DoesNotMoveOrRepaginate()
    {
        var (growing, footerText) = BuildGrowingFooterReport("PageNumber");
        var (registry, content, deviceY) = Recorder(footerText);
        var grownPages = await PageRenderer.RenderAsync(growing, scale: 1f, CancellationToken.None, registry);

        Assert.InRange(grownPages.Count, 3, int.MaxValue);

        // Guard the premise: the last page's content really does reserve more height than the seeding
        // measurement (page 1 of 1) saw. Without this the rest of the test could pass vacuously.
        var shortHeight = await ReservedFooterHeightAsync(BuildGrowingFooterReport().Design);
        var grownHeight = await ReservedFooterHeightAsync(BuildGrowingFooterReport(content: content[^1]).Design);
        Assert.True(grownHeight > shortHeight,
            $"the footer must actually grow for this test to bite, but height went {shortHeight} → {grownHeight}");

        // Invariant 1: positioned from the RESERVED height, so the footer sits at the same device y on
        // every page even though it measured taller on later ones.
        Assert.Single(deviceY.Distinct());

        // Invariant 2: the reserved height drove pagination once, so the grown content never repaginates.
        var baselinePages = await PageRenderer.RenderAsync(
            BuildGrowingFooterReport().Design, scale: 1f, CancellationToken.None);
        Assert.Equal(baselinePages.Count, grownPages.Count);
    }

    private static async Task<float> ReservedFooterHeightAsync(Report design)
        => (await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None))
            .PageFooterHeight;
}
