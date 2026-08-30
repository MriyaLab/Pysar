using System.Windows;
using Pysar.Sample.Reports;
using Pysar.Sample.Reports.QRCode;
using Pysar.Wpf;

namespace Pysar.Wpf.Sample;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        this.UsePysar(pysar => pysar
            .RegisterFonts(ReportBootstrap.RegisterFonts)
            .AddDrawer<QRCode>(new QRCodeDrawer()));

        var window = new MainWindow
        {
            DataContext = new ViewModels.ReportViewerViewModel()
        };
        window.Show();
    }
}
