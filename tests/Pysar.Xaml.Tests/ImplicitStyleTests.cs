using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ImplicitStyleTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void ImplicitStyle_AppliesToType_LocalOverrides()
    {
        var xaml = $"<Report {Root}>" +
                   "<Report.Resources>" +
                   "<Style TargetType=\"Text\"><Setter Member=\"FontSize\" Value=\"14\"/></Style>" +
                   "</Report.Resources>" +
                   "<DetailBand><StackPanel Height=\"Auto\">" +
                   "  <Text x:Name=\"A\"/>" +
                   "  <Text x:Name=\"B\" FontSize=\"9\"/>" +
                   "</StackPanel></DetailBand></Report>";

        var design = ReportXaml.Load(xaml);
        // A data-less DetailBand wraps its template in a Repeater: DetailBand -> Repeater -> StackPanel.
        var repeater = (Repeater)design.Detail.Children[0];
        var panel = (StackPanel)repeater.Children[0];
        var a = (Text)panel.Children[0];
        var b = (Text)panel.Children[1];
        Assert.Equal(14f, a.FontSize);   // from implicit style
        Assert.Equal(9f, b.FontSize);    // local wins
    }

    [Fact]
    public void LegacyPropertyAttribute_IsRejected()
    {
        var xaml = $"<Report {Root}><Report.Resources>"
                   + "<Style TargetType=\"Text\"><Setter Property=\"FontSize\" Value=\"14\"/></Style>"
                   + "</Report.Resources></Report>";

        var error = Assert.Throws<XamlException>(() => ReportXaml.Load(xaml));

        Assert.Contains("<Setter> requires Member.", error.Message);
    }
}
