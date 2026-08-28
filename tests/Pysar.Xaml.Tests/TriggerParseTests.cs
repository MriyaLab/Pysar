using System.Linq;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class TriggerParseTests
{
    private const string Ns = "xmlns=\"https://mriyalab.com/pysar\"";

    private static string ReportWith(string binding, string compare, string value) => $@"
<Report {Ns}>
  <PageHeaderBand Height=""20"">
    <Text Content=""x"" FontColor=""#000000"">
      <Text.Triggers>
        <DataTrigger Binding=""{{Binding {binding}}}"" CompareType=""{compare}"" Value=""{value}"">
          <Setter Member=""FontColor"" Value=""#FF0000"" />
        </DataTrigger>
      </Text.Triggers>
    </Text>
  </PageHeaderBand>
</Report>";

    private static Text Run(string xaml, object dataContext)
    {
        var report = ReportXaml.Load(xaml);
        report.DataContext = dataContext;
        report.Build();
        return report.PageHeader!.Children.OfType<Text>().First();
    }

    [Fact]
    public void Trigger_Satisfied_AppliesSetter()
    {
        var text = Run(ReportWith("Amount", "GreaterThanOrEqual", "100"), new { Amount = 150 });
        Assert.Equal(Color.FromHex("#FF0000"), text.FontColor);
    }

    [Fact]
    public void Trigger_NotSatisfied_LeavesLiteral()
    {
        var text = Run(ReportWith("Amount", "GreaterThanOrEqual", "100"), new { Amount = 50 });
        Assert.Equal(Color.FromHex("#000000"), text.FontColor);
    }
}
