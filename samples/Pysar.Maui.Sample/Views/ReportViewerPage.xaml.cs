using Pysar.Maui.Sample.ViewModels;

namespace Pysar.Maui.Sample.Views;

public partial class ReportViewerPage : ContentPage
{
    private readonly ReportViewerViewModel _viewModel;

    public ReportViewerPage(ReportViewerViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    /// <summary>Surfaces a viewer failure in the same label the export uses.</summary>
    private void OnRenderFailed(object? sender, Exception exception)
    {
        _viewModel.ErrorMessage = exception.Message;

        Console.WriteLine($"Pysar: render failed - {exception}");
    }
}
