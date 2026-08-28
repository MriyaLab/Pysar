using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia.Layout;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class RepeatDetailHeaderTests
{
    private static object[] Records(int n) => Enumerable.Range(0, n).Select(i => (object)new { N = $"r{i}" }).ToArray();

    [Fact]
    public async Task Measure_RepeatFlagOn_ExtractsHeaderNodeAndHeight()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(20), Size = PageSize.A4 })
            .WithDetail(d =>
            {
                d.WithDataSource(Records(3));
                d.WithDetailHeader(h => h.WithSize(SizeLength.Fill, SizeLength.Fixed(30)));
                d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(40)) });
                d.WithRepeatDetailHeader();
            })
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        Assert.NotNull(layout.RepeatDetailHeader);
        Assert.Equal(30, layout.RepeatDetailHeaderHeight);
    }

    [Fact]
    public async Task Measure_RepeatFlagOff_NoRepeatHeader()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(20), Size = PageSize.A4 })
            .WithDetail(d =>
            {
                d.WithDataSource(Records(3));
                d.WithDetailHeader(h => h.WithSize(SizeLength.Fill, SizeLength.Fixed(30)));
                d.AddElement(new Frame { Size = new Size(SizeLength.Fill, SizeLength.Fixed(40)) });
            })
            .Build();

        var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);

        Assert.Null(layout.RepeatDetailHeader);
        Assert.Equal(0, layout.RepeatDetailHeaderHeight);
    }
}
