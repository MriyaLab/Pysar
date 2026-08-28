using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ExplicitStyleTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void ExplicitStyle_AppliesByKey_LocalOverrides()
    {
        var xaml = $"<Report {Root}>" +
                   "<Report.Resources>" +
                   "<Color x:Key=\"Brand\">#2C3E50</Color>" +
                   "<Style x:Key=\"H1\" TargetType=\"Text\">" +
                   "  <Setter Member=\"FontSize\" Value=\"14\"/>" +
                   "  <Setter Member=\"FontColor\" Value=\"{StaticResource Brand}\"/>" +
                   "</Style>" +
                   "</Report.Resources>" +
                   "<DetailBand><StackPanel Height=\"Auto\">" +
                   "  <Text x:Name=\"T\" Style=\"{StaticResource H1}\" FontSize=\"20\"/>" +
                   "</StackPanel></DetailBand></Report>";

        var design = ReportXaml.Load(xaml);
        // data-less DetailBand wraps its template in a Repeater: Detail → Repeater → StackPanel → Text
        var panel = (StackPanel)((Repeater)design.Detail.Children[0]).Children[0];
        var t = (Text)panel.Children[0];
        Assert.Equal(20f, t.FontSize);                          // local overrides explicit style
        Assert.Equal(Color.FromHex("#2C3E50"), t.Font.Color);   // from style setter via {StaticResource}
    }

    [Fact]
    public void ExplicitStyle_SurvivesBuild_NotOverwrittenByImplicit()
    {
        var xaml = $"<Report {Root}>" +
                   "<Report.Resources>" +
                   "<Style TargetType=\"Text\"><Setter Member=\"FontSize\" Value=\"14\"/></Style>" +
                   "<Style x:Key=\"H1\" TargetType=\"Text\"><Setter Member=\"FontSize\" Value=\"38\"/></Style>" +
                   "</Report.Resources>" +
                   "<PageHeaderBand>" +
                   "  <Text x:Name=\"Heading\" Style=\"{StaticResource H1}\"/>" +
                   "</PageHeaderBand></Report>";

        var report = new Report();
        var result = ReportXaml.LoadInto(report, xaml);
        report.Build();

        var heading = Assert.IsType<Text>(result.Names["Heading"]);
        Assert.Equal(38f, heading.FontSize);
        Assert.Same(report.Resources["H1"], heading.Style);
    }
}
