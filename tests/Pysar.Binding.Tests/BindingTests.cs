using Pysar.Binding.Converters;
using Pysar.Core.Structs;
using Pysar.Elements;
using Xunit;

namespace Pysar.Binding.Tests;

public class BindingEngineTests
{
    [Fact]
    public void GetValue_SimplePath_ReturnsValue()
    {
        var engine = new BindingEngine();
        var expression = new BindingExpression { Path = "Name" };
        var data = new { Name = "John" };

        var result = engine.GetValue(expression, data);

        Assert.Equal("John", result);
    }

    [Fact]
    public void GetValue_NestedPath_ReturnsValue()
    {
        var engine = new BindingEngine();
        var expression = new BindingExpression { Path = "Customer.Name" };
        var data = new { Customer = new { Name = "John" } };

        var result = engine.GetValue(expression, data);

        Assert.Equal("John", result);
    }

    [Fact]
    public void GetValue_NullDataContext_ReturnsNull()
    {
        var engine = new BindingEngine();
        var expression = new BindingExpression { Path = "Name" };

        var result = engine.GetValue(expression, null);

        Assert.Null(result);
    }

    [Fact]
    public void GetValue_EmptyPath_ReturnsNull()
    {
        var engine = new BindingEngine();
        var expression = new BindingExpression { Path = "" };
        var data = new { Name = "John" };

        var result = engine.GetValue(expression, data);

        Assert.Null(result);
    }

    [Fact]
    public void GetValue_StringFormat_FormatsValue()
    {
        var engine = new BindingEngine();
        var expression = new BindingExpression { Path = "Name", StringFormat = "Hello {0}" };
        var data = new { Name = "World" };

        var result = engine.GetValue(expression, data);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void GetValue_Converter_AppliesConverter()
    {
        var engine = new BindingEngine();
        var converter = new DateTimeFormatConverter();
        var expression = new BindingExpression
        {
            Path = "Date",
            Converter = converter,
            ConverterParameter = "dd.MM.yyyy"
        };
        var data = new { Date = new DateTime(2024, 12, 25) };

        var result = engine.GetValue(expression, data);

        Assert.Equal("25.12.2024", result);
    }

    [Fact]
    public void GetValue_InvalidPath_ReturnsNull()
    {
        var engine = new BindingEngine();
        var expression = new BindingExpression { Path = "NonExistent" };
        var data = new { Name = "John" };

        var result = engine.GetValue(expression, data);

        Assert.Null(result);
    }
}

public class TextBindingTests
{
    private sealed class CustomBindable : BindableObject
    {
        public static readonly BindableProperty ValueProperty =
            BindableProperty.Create(nameof(Value), typeof(string), typeof(CustomBindable), string.Empty);

        public string Value
        {
            get => GetValue<string>(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }

    [Fact]
    public void ResolveBindings_DerivedBindableObject_ResolvesThroughBindingStore()
    {
        var bindable = new CustomBindable();
        bindable.SetBinding(CustomBindable.ValueProperty, "Name");

        new BindingEngine().ResolveBindings(bindable, new { Name = "Resolved" });

        Assert.Equal("Resolved", bindable.Value);
    }

    [Fact]
    public void ResolveBindings_WithSetBinding_ResolvesContent()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "CustomerName");

        var engine = new BindingEngine();
        engine.ResolveBindings(text, new { CustomerName = "John Doe" });

        Assert.Equal("John Doe", text.Content);
    }

    [Fact]
    public void ResolveBindings_WithoutBinding_KeepsOriginalContent()
    {
        var text = new Text { Content = "Original" };

        var engine = new BindingEngine();
        engine.ResolveBindings(text, new { CustomerName = "John" });

        Assert.Equal("Original", text.Content);
    }

    [Fact]
    public void ResolveBindings_WithStringFormat_AppliesFormat()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "Name", "Hello {0}!");

        var engine = new BindingEngine();
        engine.ResolveBindings(text, new { Name = "World" });

        Assert.Equal("Hello World!", text.Content);
    }

    [Fact]
    public void ResolveBindings_WithConverter_AppliesConverter()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "Date", new DateTimeFormatConverter(), "dd.MM.yyyy");

        var engine = new BindingEngine();
        engine.ResolveBindings(text, new { Date = new DateTime(2024, 12, 25) });

        Assert.Equal("25.12.2024", text.Content);
    }

    [Fact]
    public void ResolveBindings_NullValue_SetsEmptyString()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "Name");

        var engine = new BindingEngine();
        engine.ResolveBindings(text, new { Name = (string?)null });

        Assert.Equal(string.Empty, text.Content);
    }
}

public class NonStringBindingTests
{
    private record Style(Color Accent, bool Visible, int Layer, string Hex);

    [Fact]
    public void ResolveBindings_ColorProperty_ResolvesColorValue()
    {
        var frame = new Frame();
        frame.SetBinding(Frame.BackgroundColorProperty, "Accent");

        var engine = new BindingEngine();
        engine.ResolveBindings(frame, new Style(Colors.Red, true, 0, "#FF00FF00"));

        Assert.Equal(Colors.Red, frame.BackgroundColor);
    }

    [Fact]
    public void ResolveBindings_ColorProperty_ParsesHexString()
    {
        var frame = new Frame();
        frame.SetBinding(Frame.BackgroundColorProperty, "Hex");

        var engine = new BindingEngine();
        engine.ResolveBindings(frame, new Style(Colors.Red, true, 0, "#FF00FF00"));

        Assert.Equal(Color.FromHex("#FF00FF00"), frame.BackgroundColor);
    }

    [Fact]
    public void ResolveBindings_BoolProperty_ResolvesBoolValue()
    {
        var frame = new Frame();
        frame.SetBinding(Frame.IsVisibleProperty, "Visible");

        var engine = new BindingEngine();
        engine.ResolveBindings(frame, new Style(Colors.Red, false, 0, "#FF00FF00"));

        Assert.False(frame.IsVisible);
    }

    [Fact]
    public void ResolveBindings_IntProperty_ResolvesIntValue()
    {
        var frame = new Frame();
        frame.SetBinding(Frame.ZIndexProperty, "Layer");

        var engine = new BindingEngine();
        engine.ResolveBindings(frame, new Style(Colors.Red, true, 7, "#FF00FF00"));

        Assert.Equal(7, frame.ZIndex);
    }

    [Fact]
    public void ResolveBindings_StringContent_StillWorks()
    {
        var text = new Text();
        text.SetBinding(Text.ContentProperty, "Hex");

        var engine = new BindingEngine();
        engine.ResolveBindings(text, new Style(Colors.Red, true, 0, "#FF00FF00"));

        Assert.Equal("#FF00FF00", text.Content);
    }
}

public class ConvertersTests
{
    [Fact]
    public void StringFormatConverter_FormatsString()
    {
        var converter = new StringFormatConverter();
        var result = converter.Convert("test", typeof(string), "Hello {0}");
        Assert.Equal("Hello test", result);
    }

    [Fact]
    public void StringFormatConverter_NullValue_ReturnsNull()
    {
        var converter = new StringFormatConverter();
        var result = converter.Convert(null, typeof(string), "Hello {0}");
        Assert.Null(result);
    }

    [Fact]
    public void StringFormatConverter_NoFormat_ReturnsToString()
    {
        var converter = new StringFormatConverter();
        var result = converter.Convert(42, typeof(string), null);
        Assert.Equal("42", result);
    }

    [Fact]
    public void DateTimeFormatConverter_FormatsDateTime()
    {
        var converter = new DateTimeFormatConverter();
        var date = new DateTime(2024, 12, 25);
        var result = converter.Convert(date, typeof(string), "dd.MM.yyyy");
        Assert.Equal("25.12.2024", result);
    }

    [Fact]
    public void DateTimeFormatConverter_NonDateTime_ReturnsToString()
    {
        var converter = new DateTimeFormatConverter();
        var result = converter.Convert("not a date", typeof(string), "dd.MM.yyyy");
        Assert.Equal("not a date", result);
    }
}

public class BindingSourceTests
{
    private sealed class Holder : Pysar.Elements.Base.ReportElement<Holder>
    {
        public static BindableProperty TitleProperty { get; } =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(Holder), string.Empty);

        public string Title
        {
            get => (string)GetValue(TitleProperty)!;
            set => SetValue(TitleProperty, value);
        }
    }

    [Fact]
    public void ResolveBindings_ExplicitSource_ReadsFromSourceNotDataContext()
    {
        var source = new Holder { Title = "From component" };
        var target = new Pysar.Elements.Text();
        target.SetBinding(
            Pysar.Elements.Text.ContentProperty,
            new BindingInfo("Title", source: source));

        new BindingEngine().ResolveBindings(target, new { Title = "From model" });

        Assert.Equal("From component", target.Content);
    }

    [Fact]
    public void ResolveBindings_ExplicitSource_ResolvesWithoutDataContext()
    {
        var source = new Holder { Title = "From component" };
        var target = new Pysar.Elements.Text();
        target.SetBinding(
            Pysar.Elements.Text.ContentProperty,
            new BindingInfo("Title", source: source));

        new BindingEngine().ResolveBindings(new[] { (Pysar.Core.Abstractions.IReportElement)target }, null);

        Assert.Equal("From component", target.Content);
    }
}
