using Pysar.Elements;

namespace Pysar.Console.Tests.Reports.Triggers;

public partial class PeriodIncomeReportXaml : Report
{
    public PeriodIncomeReportXaml()
    {
        InitializeComponent();
        DataContext = PeriodIncome.CreateSample();
    }
}
