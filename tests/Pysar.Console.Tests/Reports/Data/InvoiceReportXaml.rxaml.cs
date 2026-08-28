using Pysar.Elements;

namespace Pysar.Console.Tests.Reports.Data;

public partial class InvoiceReportXaml : Report
{
    public InvoiceReportXaml()
    {
        InitializeComponent();
        DataContext = Invoice.CreateSample();
    }
}
