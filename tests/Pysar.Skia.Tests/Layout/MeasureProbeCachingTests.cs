using Pysar.Core.Abstractions;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

/// <summary>
///     A container sizes its tracks by probing each child through a full recursive measure, then
///     measures the child again to place it. Nothing remembered a probe's answer, so the probes
///     multiplied with nesting depth: a leaf under four containers was measured on the order of a
///     hundred times, and an ordinary four-page report took seven seconds to lay out.
/// </summary>
/// <remarks>
///     The counts here are deliberately expressed as ratios rather than as absolute numbers. What is
///     being pinned down is that probing costs the same per level whatever the depth - the absolute
///     figure is an implementation detail of how many passes a grid makes, and locking it in would
///     turn every future change to those passes into a failing test for no reason.
/// </remarks>
public class MeasureProbeCachingTests
{
    private sealed class Badge : ReportElement<Badge>;

    /// <summary>Counts how often it is asked, and answers a constant so nesting adds no geometry.</summary>
    private sealed class CountingMeasurer : IElementMeasurer
    {
        public int Calls { get; private set; }

        public (float Width, float Height) Measure(
            IReportElement element, (float Width, float Height) available, MeasureContext ctx)
        {
            Calls++;

            return (40f, 20f);
        }
    }

    /// <summary>
    ///     <paramref name="depth"/> auto-sized grids nested one inside the next, with a badge at the
    ///     bottom. Auto on both axes is what makes a grid probe: a fixed track needs no question
    ///     asked of its content.
    /// </summary>
    private static IReportElement NestGrids(int depth, IReportElement leaf)
    {
        var current = leaf;

        for (var level = 0; level < depth; level++)
        {
            var grid = new Grid { Size = new Size(SizeLength.Auto, SizeLength.Auto) };
            grid.AddElement(current, 0, 0);
            current = grid;
        }

        return current;
    }

    private static async Task<int> CountProbesAsync(int depth)
    {
        var counter = new CountingMeasurer();
        var measurers = new MeasurerRegistry();
        measurers.Register<Badge>(counter);

        var badge = new Badge { Size = new Size(SizeLength.Auto, SizeLength.Auto) };

        await LayoutEngine.MeasureAsync(
            NestGrids(depth, badge),
            new MeasureConstraint(new Rect(0, 0, 500, 500)),
            new MeasureContext(1f) { Measurers = measurers },
            CancellationToken.None);

        return counter.Calls;
    }

    [Fact]
    public async Task NestingOneMoreContainer_DoesNotMultiplyHowOftenTheLeafIsMeasured()
    {
        var shallow = await CountProbesAsync(depth: 2);
        var deeper = await CountProbesAsync(depth: 3);

        // Additive, not a factor. A factor of two was the first version of this bound and it was
        // useless: the vertical stack doubles per level, so "no more than twice" passed while the
        // blow-up it was written to catch was fully present. A level may add a constant number of
        // passes; it may not multiply.
        Assert.True(
            deeper <= shallow + 3,
            $"nesting one more container took the leaf from {shallow} measurements to {deeper}, "
            + "which is the multiplicative blow-up this cache exists to prevent");
    }

    [Fact]
    public async Task DeepNesting_MeasuresTheLeafAFewTimes_NotHundreds()
    {
        var calls = await CountProbesAsync(depth: 4);

        // Four containers is an ordinary report row - a band holding a grid holding a row grid
        // holding a cell. Before the cache this cost around a hundred measurements of a single
        // leaf; the bound is loose on purpose, because the point is the order of magnitude.
        Assert.True(calls < 25, $"the leaf was measured {calls} times under four containers");
    }

    /// <summary>
    ///     The shape a repeater actually produces: <c>RepeaterExpander</c> wraps every data item in a
    ///     row and stacks the rows vertically, and a nested repeater does the same inside each row.
    ///     So a report's real nesting is vertical stacks, not grids.
    /// </summary>
    private static IReportElement NestVerticalStacks(int depth, IReportElement leaf)
    {
        var current = leaf;

        for (var level = 0; level < depth; level++)
        {
            var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
            stack.AddElement(current);
            current = stack;
        }

        return current;
    }

    private static async Task<int> CountStackProbesAsync(int depth)
    {
        var counter = new CountingMeasurer();
        var measurers = new MeasurerRegistry();
        measurers.Register<Badge>(counter);

        var badge = new Badge { Size = new Size(SizeLength.Auto, SizeLength.Auto) };

        await LayoutEngine.MeasureAsync(
            NestVerticalStacks(depth, badge),
            new MeasureConstraint(new Rect(0, 0, 500, 500)),
            new MeasureContext(1f) { Measurers = measurers },
            CancellationToken.None);

        return counter.Calls;
    }

    [Fact]
    public async Task NestingOneMoreVerticalStack_DoesNotMultiplyHowOftenTheLeafIsMeasured()
    {
        var shallow = await CountStackProbesAsync(depth: 2);
        var deeper = await CountStackProbesAsync(depth: 3);

        Assert.True(
            deeper <= shallow + 3,
            $"nesting one more vertical stack took the leaf from {shallow} measurements to {deeper}");
    }

    private sealed record Entry(string Label);

    private sealed record Month(string Name, IReadOnlyList<Entry> Entries);

    /// <summary>
    ///     The report the synthetic cases above stand in for, built the way an author writes one: a
    ///     detail band over groups, each group repeating its own rows. This is the shape that was
    ///     actually slow, and the one the hand-nested containers missed - a fix that only addressed
    ///     grids passed every test above while a report of exactly this shape was unchanged.
    /// </summary>
    [Fact]
    public async Task AGroupedReport_MeasuresEachLeafAFewTimes_NotHundreds()
    {
        const int groups = 6;
        const int perGroup = 4;

        var months = Enumerable.Range(0, groups)
            .Select(m => new Month(
                $"Month {m}",
                Enumerable.Range(0, perGroup).Select(e => new Entry($"Entry {e}")).ToArray()))
            .ToArray();

        var counter = new CountingMeasurer();

        var design = ReportBuilder.Create("Grouped")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithDetail(d => d.WithData(months, (month, monthRow) => monthRow
                .AddElement(new Text { Content = month.Name })
                .AddGroup(month.Entries, (entry, entryRow) => entryRow
                    .AddElement(new Text { Content = entry.Label })
                    .AddElement(new Badge { Size = new Size(SizeLength.Auto, SizeLength.Auto) }))))
            .Build();

        var renderer = new SkiaReportRenderer().WithMeasurer<Badge>(counter);

        await renderer.CreateSessionAsync(design);

        var leaves = groups * perGroup;

        // Per leaf, not in total, so the bound says something about the shape rather than about
        // this particular row count. Every container between the band and a badge legitimately
        // measures it a bounded number of times; what must not happen is that number growing with
        // how deeply the repeater nested.
        //
        // Measured both ways when this was written: 2.0 per leaf as it stands, and 128.0 with the
        // vertical stack's phase-4 reuse removed and nothing else changed. Twenty sits an order of
        // magnitude above the real figure, so an honest change to how many passes a container makes
        // will not trip it, and far enough below 128 to catch that regression outright.
        var perLeaf = (double)counter.Calls / leaves;

        Assert.True(
            perLeaf < 20,
            $"each of the {leaves} leaves was measured {perLeaf:F1} times on average "
            + $"({counter.Calls} in total)");
    }

    [Fact]
    public async Task AProbeAnsweredForOneMeasure_IsNotReusedByTheNext()
    {
        // The report's page bands are re-stamped with each page number and then measured again
        // through the same MeasureContext - see PageBandResolver. A cache that outlived one
        // measure would hand page two the width of page one's footer.
        var counter = new CountingMeasurer();
        var measurers = new MeasurerRegistry();
        measurers.Register<Badge>(counter);

        var badge = new Badge { Size = new Size(SizeLength.Auto, SizeLength.Auto) };
        var tree = NestGrids(2, badge);
        var context = new MeasureContext(1f) { Measurers = measurers };
        var constraint = new MeasureConstraint(new Rect(0, 0, 500, 500));

        await LayoutEngine.MeasureAsync(tree, constraint, context, CancellationToken.None);
        var afterFirst = counter.Calls;

        await LayoutEngine.MeasureAsync(tree, constraint, context, CancellationToken.None);

        Assert.True(
            counter.Calls > afterFirst,
            "the second measure answered entirely from the first one's cache, so a re-stamped "
            + "band would keep the previous page's geometry");
    }
}
