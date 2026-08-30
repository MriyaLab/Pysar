using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Pysar.Avalonia.Sample.ViewModels;
using Pysar.Avalonia.Sample.Views;

namespace Pysar.Avalonia.Sample;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new ReportViewerViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
