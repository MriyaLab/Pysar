using Pysar.Binding;
using Xunit;

namespace Pysar.Binding.Tests;

public class BindableObjectHasBindingsTests
{
    private sealed class Probe : BindableObject
    {
        public static readonly BindableProperty ValueProperty =
            BindableProperty.Create(nameof(Value), typeof(string), typeof(Probe), string.Empty);

        public string Value
        {
            get => (string)GetValue(ValueProperty)!;
            set => SetValue(ValueProperty, value);
        }
    }

    [Fact]
    public void HasBindings_IsFalse_WhenNoBindingWasSet()
    {
        Assert.False(new Probe().HasBindings);
    }

    [Fact]
    public void HasBindings_IsTrue_AfterSetBinding()
    {
        var probe = new Probe();
        probe.SetBinding(Probe.ValueProperty, "PageNumber");

        Assert.True(probe.HasBindings);
    }

    [Fact]
    public void HasBindings_StaysTrue_AfterResolution()
    {
        // Resolution reads _pendingBindings without clearing them — this is what makes the
        // per-page re-resolution in PageBandResolver possible.
        var probe = new Probe();
        probe.SetBinding(Probe.ValueProperty, "PageNumber");
        new BindingEngine().ResolveBindings(probe, new { PageNumber = 3 });

        Assert.Equal("3", probe.Value);
        Assert.True(probe.HasBindings);
    }
}
