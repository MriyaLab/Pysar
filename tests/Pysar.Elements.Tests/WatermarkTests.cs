using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class WatermarkTests
{
    [Fact]
    public void Watermark_IsFrame_SupportsChildren()
    {
        var watermark = new Watermark();
        watermark.AddElement(new Text { Content = "DRAFT" });
        Assert.Single(watermark.Children);
        Assert.IsAssignableFrom<Frame>(watermark);
    }

    [Fact]
    public void Watermark_Layer_DefaultsToBehind()
    {
        Assert.Equal(WatermarkLayer.Behind, new Watermark().Layer);
    }

    [Fact]
    public void Report_Watermark_DefaultIsNull()
    {
        Assert.Null(new Report().Watermark);
    }

    [Fact]
    public void Report_Watermark_IsNotInBands()
    {
        var report = new Report();
        var watermark = new Watermark();
        report.Watermark = watermark;
        Assert.Same(watermark, report.Watermark);
        Assert.DoesNotContain<object>(watermark, report.Bands);
    }

    [Fact]
    public void Report_Watermark_SetsParent()
    {
        var report = new Report();
        var watermark = new Watermark();
        report.Watermark = watermark;
        Assert.Same(report, watermark.ParentElement);
    }

    [Fact]
    public void Report_Watermark_Replace_DoesNotThrow()
    {
        var report = new Report();
        report.Watermark = new Watermark();
        var second = new Watermark();
        report.Watermark = second;
        Assert.Same(second, report.Watermark);
        Assert.Same(report, second.ParentElement);
    }

    [Fact]
    public void Builder_WithWatermark_ConfiguresWatermark()
    {
        var design = ReportBuilder.Create("t")
            .WithWatermark(w => w.AddElement(new Text { Content = "DRAFT" }))
            .Build();

        Assert.NotNull(design.Watermark);
        Assert.Single(design.Watermark!.Children);
    }

    [Fact]
    public void Builder_WithWatermark_SecondCallMutatesSameInstance()
    {
        var design = ReportBuilder.Create("t")
            .WithWatermark(w => w.BackgroundColor = Colors.Red)
            .WithWatermark(w => w.AddElement(new Text { Content = "DRAFT" }))
            .Build();

        Assert.Equal(Colors.Red, design.Watermark!.BackgroundColor);
        Assert.Single(design.Watermark.Children);
    }

    [Fact]
    public void Build_ResolvesWatermarkBindings()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "Title");

        var report = new Report { DataContext = new WatermarkVm("DRAFT") };
        report.Watermark = new Watermark();
        report.Watermark.AddElement(text);
        report.Build();

        Assert.Equal("DRAFT", text.Content);
    }

    [Fact]
    public void Build_AppliesImplicitStyle_ToWatermarkChild()
    {
        var report = new Report();
        report.Resources[typeof(Text)] = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.FontSize), Value = "14" } }
        };

        var text = new Text();
        report.Watermark = new Watermark();
        report.Watermark.AddElement(text);
        report.Build();

        Assert.Equal(14f, text.FontSize);
    }

    private sealed record WatermarkVm(string Title);
}
