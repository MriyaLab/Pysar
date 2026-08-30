using Pysar.Elements;

namespace Pysar.Console.Tests.Reports.MasterDitails;

public partial class MasterDetailReportXaml : Report
{
    public MasterDetailReportXaml()
    {
        InitializeComponent();
        DataContext = AnnualLedger.CreateSample();
    }
}