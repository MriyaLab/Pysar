using Pysar.Elements;

namespace Pysar.Console.Sample.Reports.MasterDetails;

public partial class MasterDetailReportXaml : Report
{
    public MasterDetailReportXaml()
    {
        InitializeComponent();
        DataContext = AnnualLedger.CreateSample();
    }
}