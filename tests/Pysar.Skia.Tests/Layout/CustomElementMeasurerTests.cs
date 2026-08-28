using Pysar.Core.Enums;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Skia.Layout;
using Pysar.Skia.Rendering;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

/// <summary>
///     A custom element could register how it is drawn but not how it is measured, so anything with
///     an intrinsic content size - a chart, a barcode, a QR code - resolved to zero under
///     <c>Auto</c> and the author had no way to correct it.
/// </summary>
public class CustomElementMeasurerTests
{
    private sealed class Badge : ReportElement<Badge>;

    /// <summary>A badge is as tall as it is wide, whatever width it is given.</summary>
    private sealed class SquareMeasurer : IElementMeasurer
    {
        public (float Width, float Height) Measure(
            IReportElement element, (float Width, float Height) available, MeasureContext ctx)
            => (available.Width, available.Width);
    }

    private static readonly Rect Available = new(0, 0, 120, 400);

    private static async Task<LayoutNode> MeasureAsync(IReportElement element, MeasurerRegistry? measurers = null)
        => await LayoutEngine.MeasureAsync(
            element,
            new MeasureConstraint(Available),
            new MeasureContext(1f) { Measurers = measurers ?? new MeasurerRegistry() },
            CancellationToken.None);

    [Fact]
    public async Task AutoSizedCustomElement_WithoutAMeasurer_StillResolvesToZero()
    {
        var badge = new Badge { Size = new Size(SizeLength.Fill, SizeLength.Auto) };

        var node = await MeasureAsync(badge);

        // Unchanged behaviour for an element that registered nothing: there is no content size to
        // ask about, so the leaf-box rule still applies.
        Assert.Equal(0, node.Bounds.Height);
    }

    [Fact]
    public async Task AutoSizedCustomElement_WithAMeasurer_GetsItsContentSize()
    {
        var badge = new Badge { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        var measurers = new MeasurerRegistry();
        measurers.Register<Badge>(new SquareMeasurer());

        var node = await MeasureAsync(badge, measurers);

        Assert.Equal(120, node.Bounds.Width);
        Assert.Equal(120, node.Bounds.Height);
    }

    [Fact]
    public async Task FixedSize_WinsOverTheMeasurer()
    {
        var badge = new Badge { Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(20)) };
        var measurers = new MeasurerRegistry();
        measurers.Register<Badge>(new SquareMeasurer());

        var node = await MeasureAsync(badge, measurers);

        // An explicit size is the author's instruction; only Auto asks the element what it needs.
        Assert.Equal(50, node.Bounds.Width);
        Assert.Equal(20, node.Bounds.Height);
    }

    [Fact]
    public async Task WithMeasurer_ReachesTheMeasurePhaseThroughTheRendererItself()
    {
        var badge = new Badge { Size = new Size(SizeLength.Fixed(80), SizeLength.Auto) };

        var design = ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(0), Size = PageSize.A4 })
            .WithDetail(b => b.AddElement(badge))
            .Build();

        var renderer = new SkiaReportRenderer().WithMeasurer<Badge>(new SquareMeasurer());
        var session = await renderer.CreateSessionAsync(design);

        // A square badge 80pt wide is 80pt tall, so it fits on one page rather than collapsing to
        // nothing - which is what an unregistered Auto element would have done.
        Assert.Equal(1, session.PageCount);
    }

    [Fact]
    public async Task AMeasurerRegisteredForAnotherType_IsNotUsed()
    {
        var badge = new Badge { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        var measurers = new MeasurerRegistry();
        measurers.Register<Frame>(new SquareMeasurer());

        var node = await MeasureAsync(badge, measurers);

        Assert.Equal(0, node.Bounds.Height);
    }
}
