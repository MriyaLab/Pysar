using Pysar.Core.Abstractions;

namespace Pysar.Sample.Reports;

public partial class RevenueByCustomerReport
{
    public RevenueByCustomerReport(RevenueReportData data)
    {
        InitializeComponent();
        DataContext = data;
    }

    public RevenueByCustomerReport()
    {
        InitializeComponent();
    }
}
