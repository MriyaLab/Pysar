using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ResourceTypeResolutionTests
{
    private const string Ns = "https://mriyalab.com/pysar";

    [Fact]
    public void Resolve_CoreValueType_And_StyleType()
    {
        var r = new XamlTypeResolver();
        Assert.Equal(typeof(Color), r.Resolve(Ns, "Color"));      // Core.Structs
        Assert.Equal(typeof(Style), r.Resolve(Ns, "Style"));      // Elements
        Assert.Equal(typeof(Setter), r.Resolve(Ns, "Setter"));
    }

    [Fact]
    public void Report_HasResources()
    {
        var d = new Report();
        Assert.NotNull(d.Resources);
        d.Resources["k"] = 1;
        Assert.Equal(1, d.Resources["k"]);
    }
}
