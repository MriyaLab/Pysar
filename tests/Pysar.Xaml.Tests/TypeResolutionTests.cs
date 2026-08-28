using System.ComponentModel;
using System.Reflection;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Elements.Base;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class TypeResolutionTests
{
    private const string Ns = "https://mriyalab.com/pysar";

    [Fact]
    public void Resolve_DefaultNamespace_FindsElementType()
    {
        var resolver = new XamlTypeResolver();
        Assert.Equal(typeof(Text), resolver.Resolve(Ns, "Text"));
        Assert.Equal(typeof(StackPanel), resolver.Resolve(Ns, "StackPanel"));
        Assert.Equal(typeof(PageHeaderBand), resolver.Resolve(Ns, "PageHeaderBand"));
    }

    [Fact]
    public void Resolve_DefaultNamespace_DoesNotCreateBandAliases()
    {
        var resolver = new XamlTypeResolver();

        var exception = Assert.Throws<XamlException>(() => resolver.Resolve(Ns, "PageHeader"));

        Assert.Contains("Type 'PageHeader' not found", exception.Message);
    }

    [Fact]
    public void StandardXamlMetadata_DescribesContentAndColorConversion()
    {
        var content = typeof(StackPanel)
            .GetCustomAttribute<System.Windows.Markup.ContentPropertyAttribute>();
        var converter = TypeDescriptor.GetConverter(typeof(Color));

        Assert.Equal(nameof(ReportContainer<StackPanel>.Children), content?.Name);
        Assert.Equal(
            Color.FromHex("#2C3E50"),
            Assert.IsType<Color>(converter.ConvertFromInvariantString("#2C3E50")));
    }

    [Fact]
    public void StandardXamlMetadata_DescribesBindingAndStyle()
    {
        var binding = new BindingExtension("Customer.Name")
        {
            StringFormat = "{0}"
        };

        Assert.IsAssignableFrom<System.Windows.Markup.MarkupExtension>(binding);
        Assert.Equal("Customer.Name", binding.Path);
        Assert.Equal(typeof(Style), typeof(Text).GetProperty(nameof(Text.Style))?.PropertyType);
    }

    [Fact]
    public void Resolve_ClrNamespace_FindsCustomType()
    {
        var resolver = new XamlTypeResolver();
        var clrNs = $"clr-namespace:Pysar.Xaml.Tests;assembly={typeof(TypeResolutionTests).Assembly.GetName().Name}";
        Assert.Equal(typeof(SampleWidget), resolver.Resolve(clrNs, "SampleWidget"));
    }

    [Fact]
    public void Resolve_DefaultNamespace_FindsCoreStructType()
    {
        var resolver = new XamlTypeResolver();
        Assert.Equal(typeof(Color), resolver.Resolve(Ns, "Color"));
        Assert.Equal(typeof(Thickness), resolver.Resolve(Ns, "Thickness"));
    }
}

public sealed class SampleWidget { }
