using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class TriggerTests
{
    private sealed record Row(decimal UnitPrice, decimal Total, string Status);

    private static Text WithTrigger(string binding, CompareType compare, string value, params (string prop, string val)[] setters)
    {
        var t = new Text { Content = "x" };
        var trigger = new DataTrigger { Binding = binding, CompareType = compare, Value = value };
        foreach (var (prop, val) in setters)
            trigger.Setters.Add(new Setter { Member = prop, Value = val });
        t.Triggers.Add(trigger);
        return t;
    }

    private static void Apply(Text t, object ctx) => TriggerEngine.Apply(new[] { (Core.Abstractions.IReportElement)t }, ctx);

    [Fact]
    public void GreaterThanOrEqual_Satisfied_AppliesSetter()
    {
        var t = WithTrigger(nameof(Row.UnitPrice), CompareType.GreaterThanOrEqual, "20", ("BackgroundColor", "#F0F1F5"));
        Apply(t, new Row(45.60m, 0, ""));
        Assert.Equal(Color.FromHex("#F0F1F5"), t.BackgroundColor);
    }

    [Fact]
    public void GreaterThanOrEqual_NotSatisfied_LeavesDefault()
    {
        var t = WithTrigger(nameof(Row.UnitPrice), CompareType.GreaterThanOrEqual, "20", ("BackgroundColor", "#F0F1F5"));
        Apply(t, new Row(12.00m, 0, ""));
        Assert.Equal(Colors.Transparent, t.BackgroundColor);
    }

    [Theory]
    [InlineData(CompareType.GreaterThan, 100, 150, true)]
    [InlineData(CompareType.GreaterThan, 100, 100, false)]
    [InlineData(CompareType.LessThan, 100, 50, true)]
    [InlineData(CompareType.LessThan, 100, 100, false)]
    [InlineData(CompareType.LessThanOrEqual, 100, 100, true)]
    [InlineData(CompareType.Equal, 100, 100, true)]
    [InlineData(CompareType.NotEqual, 100, 101, true)]
    public void NumericComparisons(CompareType compare, int threshold, int total, bool shouldApply)
    {
        var t = WithTrigger(nameof(Row.Total), compare, threshold.ToString(), ("FontColor", "#FF0000"));
        Apply(t, new Row(0, total, ""));
        Assert.Equal(shouldApply ? Color.FromHex("#FF0000") : t.Font.Color, t.FontColor);
    }

    [Fact]
    public void Equal_OnString_Matches()
    {
        var t = WithTrigger(nameof(Row.Status), CompareType.Equal, "Paid", ("FontColor", "#00FF00"));
        Apply(t, new Row(0, 0, "Paid"));
        Assert.Equal(Color.FromHex("#00FF00"), t.FontColor);
    }

    [Fact]
    public void NonColorSetter_IsCoercedAndApplied()
    {
        var t = WithTrigger(nameof(Row.Total), CompareType.GreaterThanOrEqual, "100", ("FontSize", "20"));
        Apply(t, new Row(0, 150, ""));
        Assert.Equal(20f, t.FontSize);
    }

    [Fact]
    public void TypedSetterValue_IsAppliedWithoutStringConversion()
    {
        var expected = Color.FromHex("#123456");
        var text = new Text { Content = "x" };
        var trigger = new DataTrigger
        {
            Binding = nameof(Row.Status),
            Value = "Paid"
        };
        trigger.Setters.Add(new Setter
        {
            Member = nameof(Text.FontColor),
            Value = expected
        });
        text.Triggers.Add(trigger);

        Apply(text, new Row(0, 0, "Paid"));

        Assert.Equal(expected, text.FontColor);
    }

    [Fact]
    public void MissingPath_IsNoOp()
    {
        var t = WithTrigger("DoesNotExist", CompareType.Equal, "1", ("FontColor", "#FF0000"));
        var before = t.FontColor;
        Apply(t, new Row(0, 0, ""));
        Assert.Equal(before, t.FontColor);
    }

    [Fact]
    public void Clone_PreservesTriggers_SoRowTemplatesKeepFormatting()
    {
        var template = WithTrigger(nameof(Row.Total), CompareType.GreaterThanOrEqual, "100", ("FontColor", "#FF0000"));
        var clone = (Text)template.Clone();
        Apply(clone, new Row(0, 150, ""));
        Assert.Equal(Color.FromHex("#FF0000"), clone.FontColor);
    }

    [Fact]
    public void LaterSetter_OverridesEarlier_WhenTwoTriggersMatch()
    {
        var t = new Text { Content = "x" };
        var first = new DataTrigger { Binding = nameof(Row.Total), CompareType = CompareType.GreaterThanOrEqual, Value = "0" };
        first.Setters.Add(new Setter { Member = "FontColor", Value = "#111111" });
        var second = new DataTrigger { Binding = nameof(Row.Total), CompareType = CompareType.GreaterThanOrEqual, Value = "0" };
        second.Setters.Add(new Setter { Member = "FontColor", Value = "#222222" });
        t.Triggers.Add(first);
        t.Triggers.Add(second);

        Apply(t, new Row(0, 10, ""));
        Assert.Equal(Color.FromHex("#222222"), t.FontColor);
    }

    [Fact]
    public void ImageFontSource_SatisfiedTrigger_AppliesColor()
    {
        var source = new FontImageSource { Color = Colors.Gray };
        var trigger = new DataTrigger
        {
            Binding = nameof(Row.Status),
            CompareType = CompareType.Equal,
            Value = "Delivered"
        };
        trigger.Setters.Add(new Setter { Member = "Color", Value = "#339933" });
        source.Triggers.Add(trigger);

        var image = new Image { Source = source };
        TriggerEngine.Apply([image], new Row(0, 0, "Delivered"));

        Assert.Equal(Color.FromHex("#339933"), source.Color);
    }

    [Fact]
    public void ImageClone_PreservesFontSourceTriggers()
    {
        var source = new FontImageSource { Color = Colors.Gray };
        var trigger = new DataTrigger
        {
            Binding = nameof(Row.Status),
            CompareType = CompareType.Equal,
            Value = "Delivered"
        };
        trigger.Setters.Add(new Setter { Member = "Color", Value = "#339933" });
        source.Triggers.Add(trigger);

        var clone = (Image)new Image { Source = source }.Clone();
        TriggerEngine.Apply([clone], new Row(0, 0, "Delivered"));

        Assert.Equal(Color.FromHex("#339933"), ((FontImageSource)clone.Source!).Color);
        Assert.Equal(Colors.Gray, source.Color);
    }
}
