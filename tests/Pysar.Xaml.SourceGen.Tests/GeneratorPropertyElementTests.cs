using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class GeneratorPropertyElementTests
{
    private const string Head = "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void PropertyElement_Font_Emitted()
    {
        var src = GeneratorTestHarness.Run("namespace MyApp { public partial class R {} }",
            ("R.rxaml", $"<Report x:Class=\"MyApp.R\" {Head}>" +
                       "<DetailBand><Text x:Name=\"T\"><Text.Font Family=\"Ubuntu\" Size=\"9\"/></Text></DetailBand></Report>")).GeneratedSource!;
        // Font's members are Family/Size/Style/Color (NOT FontFamily/FontSize — those flat facades live on Text).
        // NOTE: a Style="..." attribute is intentionally omitted — the generator's usesResources heuristic
        // treats any "Style" attribute as a resource reference and would switch to the runtime fallback,
        // which never emits construction C#. Family/Size keep this on the construction path.
        Assert.Contains("new global::Pysar.Core.Structs.Font()", src);
        Assert.Contains(".Family = ", src);
        Assert.Contains(".Size = ", src);
        Assert.Contains(".Font = ", src);
    }
}
