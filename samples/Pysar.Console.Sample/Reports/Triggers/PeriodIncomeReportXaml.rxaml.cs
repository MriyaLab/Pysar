using Pysar.Elements;

namespace Pysar.Console.Sample.Reports.Triggers;

public partial class PeriodIncomeReportXaml : Report
{
    public PeriodIncomeReportXaml()
    {
        InitializeComponent();
        DataContext = PeriodIncome.CreateSample();
    }
}
