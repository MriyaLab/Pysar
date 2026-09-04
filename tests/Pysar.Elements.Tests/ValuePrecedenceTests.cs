using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Xunit;

namespace Pysar.Elements.Tests;

/// <summary>
///     Specification for value precedence: <c>Trigger &gt; Local &gt; ExplicitStyle &gt; ImplicitStyle &gt;
///     Default</c>. A style must never overwrite a value the author assigned directly, on any of the three
///     construction paths (code-first, compiled XAML, runtime-loaded XAML).
///     See docs/superpowers/plans/2026-09-04-value-precedence.md.
/// </summary>
public class ValuePrecedenceTests
{
    private static Style ImplicitTextStyle() => new()
    {
        TargetType = typeof(Text),
        Setters =
        {
            new Setter { Member = nameof(Text.FontFamily), Value = "Ubuntu" },
            new Setter { Member = nameof(Text.FontSize), Value = "14" },
        }
    };

    private static Style FieldValueStyle() => new()
    {
        TargetType = typeof(Text),
        Setters =
        {
            new Setter { Member = nameof(Text.FontSize), Value = "8" },
            new Setter { Member = nameof(Text.FontColor), Value = "#444444" },
        }
    };

    private static Report ReportWith(Text text, Style? implicitStyle = null)
    {
        var report = new Report();
        if (implicitStyle is not null)
            report.Resources[typeof(Text)] = implicitStyle;

        var header = new PageHeaderBand();
        header.AddElement(text);
        report.Bands.Add(header);
        return report;
    }

    // ---------------------------------------------------------------- code-first

    [Fact]
    public void CodeFirst_LocalValue_BeatsImplicitStyle()
    {
        var text = new Text { FontFamily = "LibreBarcode128" };
        var report = ReportWith(text, ImplicitTextStyle());

        report.Build();

        Assert.Equal("LibreBarcode128", text.FontFamily);
    }

    [Fact]
    public void CodeFirst_ImplicitStyle_StillFillsUntouchedMembers()
    {
        var text = new Text { FontFamily = "LibreBarcode128" };
        var report = ReportWith(text, ImplicitTextStyle());

        report.Build();

        Assert.Equal(14f, text.FontSize);   // untouched locally - the style supplies it
    }

    [Fact]
    public void CodeFirst_LocalValue_BeatsExplicitStyle()
    {
        var text = new Text { FontSize = 55f, Style = FieldValueStyle() };
        var report = ReportWith(text);

        report.Build();

        Assert.Equal(55f, text.FontSize);
    }

    [Fact]
    public void CodeFirst_PrecedenceIsPerMember_NotPerBackingProperty()
    {
        // FontFamily/FontSize/FontStyle/FontColor are CLR facades over one backing Font property.
        // Overriding one of them locally must not block a style from supplying the others.
        var text = new Text { FontFamily = "LibreBarcode128", Style = FieldValueStyle() };
        var report = ReportWith(text, ImplicitTextStyle());

        report.Build();

        Assert.Equal("LibreBarcode128", text.FontFamily);          // local
        Assert.Equal(8f, text.FontSize);                           // explicit style beats implicit
        Assert.Equal(new Color(0x44, 0x44, 0x44), text.FontColor); // explicit style
    }

    [Fact]
    public void CodeFirst_LocalValue_EqualToTypeDefault_StillBeatsStyle()
    {
        // Font.Style defaults to Normal. Assigning Normal explicitly is a local value like any other
        // and must not be mistaken for "never set".
        var style = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.FontStyle), Value = "Bold" } }
        };
        var text = new Text { FontStyle = FontStyle.Normal };
        var report = ReportWith(text, style);

        report.Build();

        Assert.Equal(FontStyle.Normal, text.FontStyle);
    }

    [Fact]
    public void CodeFirst_ExplicitStyle_BeatsImplicitStyle()
    {
        var text = new Text { Style = FieldValueStyle() };
        var report = ReportWith(text, ImplicitTextStyle());

        report.Build();

        Assert.Equal("Ubuntu", text.FontFamily);   // only the implicit style sets it
        Assert.Equal(8f, text.FontSize);           // explicit style wins
    }

    // ------------------------------------------------ constructor-assigned defaults

    // Several elements assign their own defaults through public setters in their constructor
    // (Text.Size, PageBreak.Size, Report.BackgroundColor, Grid.Row/ColumnDefinitions). Those writes
    // must not be recorded as author-local values, or a style could never set those members again.

    [Fact]
    public void ImplicitStyle_CanSet_MemberAssignedInConstructor()
    {
        var style = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.Size), Value = new Size(SizeLength.Fill, SizeLength.Fixed(20)) } }
        };
        var text = new Text();
        var report = ReportWith(text, style);

        report.Build();

        Assert.Equal(new Size(SizeLength.Fill, SizeLength.Fixed(20)), text.Size);
    }

    [Fact]
    public void ImplicitStyle_CanSet_FacadeOverMemberAssignedInConstructor()
    {
        var style = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.Height), Value = "20" } }
        };
        var text = new Text();
        var report = ReportWith(text, style);

        report.Build();

        Assert.Equal(SizeLength.Fixed(20), text.Height);
    }

    // ------------------------------------------------ facades sharing a backing property

    [Fact]
    public void StyleWritingOneFacade_DoesNotDowngrade_TheSharedBackingMember()
    {
        // Width and Height are both facades over Size. A local Width records "Size" as Local; a style
        // then legitimately setting Height also writes Size under the hood, and must not re-record
        // "Size" at style precedence - that would open the door for a later style to set Size
        // wholesale and silently drop the author's Width.
        var report = new Report();
        report.Resources[typeof(Text)] = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.Height), Value = "20" } }
        };
        var wholesale = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.Size), Value = new Size(SizeLength.Fill, SizeLength.Fill) } }
        };

        var text = new Text { Width = SizeLength.Fixed(60), Style = wholesale };
        var header = new PageHeaderBand();
        header.AddElement(text);
        report.Bands.Add(header);

        report.Build();

        Assert.Equal(SizeLength.Fixed(60), text.Width);
    }

    [Fact]
    public void StyleAndLocal_CanOwnDifferentPartsOfTheSameBackingProperty()
    {
        // MinWidth and MaxWidth both flow through SetMinMax. Precedence is per member name, so the
        // author owns MinWidth and the style still supplies MaxWidth.
        var report = new Report();
        report.Resources[typeof(Text)] = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.MaxWidth), Value = "300" } }
        };

        var text = new Text { MinWidth = MinMaxLength.Fixed(60) };
        var header = new PageHeaderBand();
        header.AddElement(text);
        report.Bands.Add(header);

        report.Build();

        Assert.Equal(MinMaxLength.Fixed(60), text.MinWidth);
        Assert.Equal(MinMaxLength.Fixed(300), text.MaxWidth);
    }

    // ---------------------------------------------------------------- triggers

    [Fact]
    public void Trigger_BeatsLocalValue()
    {
        var text = new Text { FontFamily = "LibreBarcode128" };
        text.Triggers.Add(new DataTrigger
        {
            Binding = nameof(Row.Flag),
            CompareType = CompareType.Equal,
            Value = "yes",
            Setters = { new Setter { Member = nameof(Text.FontFamily), Value = "TriggerFont" } }
        });

        var report = ReportWith(text, ImplicitTextStyle());
        report.DataContext = new Row("yes");

        report.Build();

        Assert.Equal("TriggerFont", text.FontFamily);
    }

    // ---------------------------------------------------------------- WithStyle extension

    [Fact]
    public void WithStyle_DoesNotOverwrite_LocalValueSetBeforehand()
    {
        var resources = new ResourceDictionary
        {
            [typeof(Text)] = ImplicitTextStyle(),
            ["FieldValue"] = FieldValueStyle()
        };

        var text = new Text { FontFamily = "LibreBarcode128", FontSize = 55f };
        text.WithStyle(resources, "FieldValue");

        Assert.Equal("LibreBarcode128", text.FontFamily);
        Assert.Equal(55f, text.FontSize);
    }

    // ---------------------------------------------------------------- repeated rows

    [Fact]
    public void RepeatedRows_KeepLocalOverride_AfterCloning()
    {
        var report = new Report();
        report.Resources[typeof(Text)] = ImplicitTextStyle();

        var template = new Text { FontFamily = "LibreBarcode128" };
        template.SetBinding(Text.ContentProperty, nameof(Row.Flag));

        var detail = new DetailBand();
        detail.WithDataSource(new[] { new Row("a"), new Row("b") });
        detail.AddElement(template);
        report.Bands.Set(detail);   // a Report always has a Detail band; replace it

        report.Build();

        var rows = CollectTexts(report.Detail).ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("LibreBarcode128", row.FontFamily));
    }

    private static IEnumerable<Text> CollectTexts(Core.Abstractions.IReportElement element)
    {
        if (element is Text text)
            yield return text;

        if (element is Core.Abstractions.IReportContainer container)
            foreach (var child in container.Children)
                foreach (var found in CollectTexts(child))
                    yield return found;
    }

    private sealed record Row(string Flag);
}
