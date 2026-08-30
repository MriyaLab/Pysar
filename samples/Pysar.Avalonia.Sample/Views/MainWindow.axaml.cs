using Avalonia.Controls;
using Pysar.Avalonia.Sample.ViewModels;

namespace Pysar.Avalonia.Sample.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>Surfaces a viewer failure in the same label the load path uses.</summary>
    private void OnRenderFailed(object? sender, Exception exception)
    {
        if (DataContext is ReportViewerViewModel viewModel)
            viewModel.ErrorMessage = exception.Message;

        Console.WriteLine($"Pysar: render failed - {exception}");
    }
}