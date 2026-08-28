using Pysar.Elements;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

/// <summary>
///     Proves the page-number feature works through the real authoring surface — XAML markup — and not
///     only through the object model exercised by <see cref="PageNumberRenderTests"/>: markup binding →
///     per-page resolution → draw, including bindings nested inside a panel in a page band.
/// </summary>
public class PageNumberXamlTests
{
    private const string Xaml = """
        <Report x:Name="Root"
                xmlns="https://mriyalab.com/pysar"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <PageFormat Margin="10" Size="A4" />
            <PageFooterBand Height="20">
                <StackPanel Orientation="Horizontal">
                    <Text x:Name="Number" Content="{Binding PageNumber, Source={x:Reference Root}}" />
                    <Text Content=" of " />
                    <Text x:Name="Count" Content="{Binding PageCount, Source={x:Reference Root}}" />
                </StackPanel>
            </PageFooterBand>
            <DetailBand>
                <Frame Height="2400" />
            </DetailBand>
        </Report>
        """;

    /// <summary>
    ///     Records the content each tracked <see cref="Text"/> actually carried at <i>draw</i> time, so the
    ///     assertions observe the per-page sequence rather than the single surviving final value. Drawing is
    ///     delegated to the built-in <see cref="TextDrawer"/> so the rest of the pipeline still runs.
    /// </summary>
    private sealed class RecordingTextDrawer(IReadOnlyDictionary<Text, List<string>> tracked) : IElementDrawer
    {
        private readonly TextDrawer _inner = new();

        public void Draw(LayoutNode node, RenderContext ctx)
        {
            if (node.Element is Text text && tracked.TryGetValue(text, out var content))
                content.Add(text.Content);

            _inner.Draw(node, ctx);
        }
    }

    [Fact]
    public async Task Render_PageNumberAndCountFromXaml_ResolvePerPage()
    {
        var report = new Report();
        var result = ReportXaml.LoadInto(report, Xaml);
        var design = report.Build();

        var number = new List<string>();
        var count = new List<string>();
        var registry = DrawerRegistry.CreateDefault();
        // Reference identity: the tracked elements are matched by instance, not by value equality.
        var tracked = new Dictionary<Text, List<string>>(ReferenceEqualityComparer.Instance)
        {
            [(Text)result.Names["Number"]] = number,
            [(Text)result.Names["Count"]] = count
        };
        registry.Register<Text>(new RecordingTextDrawer(tracked));

        var pages = await PageRenderer.RenderAsync(design, scale: 1f, CancellationToken.None, registry);

        Assert.InRange(pages.Count, 3, int.MaxValue);
        Assert.Equal(Enumerable.Range(1, pages.Count).Select(n => n.ToString()), number);
        Assert.Equal(Enumerable.Repeat(pages.Count.ToString(), pages.Count), count);
    }
}
