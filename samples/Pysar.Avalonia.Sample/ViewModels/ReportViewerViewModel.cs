using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pysar.Avalonia;
using Pysar.Elements;
using Pysar.Export;
using Pysar.Sample.Reports;
using Pysar.Skia;
using Pysar.Viewer.Zoom;

namespace Pysar.Avalonia.Sample.ViewModels;

/// <summary>One entry of the report picker: a display name and how to build the report.</summary>
public sealed record ReportDescriptor(string Title, Func<Report> Create)
{
    public static IReadOnlyList<ReportDescriptor> All { get; } =
    [
        new("Invoice", () => new InvoiceReport(Invoice.CreateDesignInstance())),
        new("Annual", () => new AnnualReport(AnnualLedger.CreateDesignInstance())),
        new("Revenue By Customer", () => new RevenueByCustomerReport(RevenueReportData.CreateDesignInstance()))
    ];

    public override string ToString() => Title;
}

/// <summary>
///     Drives the report picker and the viewer's toolbar. Ported from the MAUI sample's view model
///     of the same name; PDF export is dropped here because sharing is a platform feature and this
///     sample is about the viewer.
/// </summary>
public sealed partial class ReportViewerViewModel : ViewModelBase
{
    /// <summary>The zoom levels the minus and plus buttons step through, as a browser does.</summary>
    private static readonly double[] ZoomSteps =
        [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5];

    private readonly IReportPrinter _printer;
    private bool _isBusy;

    [ObservableProperty]
    private ReportDescriptor _selectedReport = ReportDescriptor.All[0];

    [ObservableProperty]
    private Report? _report;

    [ObservableProperty]
    private ReportZoomMode _zoomMode = ReportZoomMode.FitWidth;

    /// <summary>The zoom asked for; meaningful only while the mode is Custom.</summary>
    [ObservableProperty]
    private double _zoom = 1;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageCount;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    ///     The zoom the viewer actually settled on, reported back by the control. A fit mode resolves
    ///     to a factor only the control knows, so the percentage has to come from there.
    /// </summary>
    private double _effectiveZoom = 1;

    public ReportViewerViewModel(IReportPrinter? printer = null)
    {
        _printer = printer ?? new AvaloniaReportPrinter(PysarAvalonia.Renderer);
        LoadReport();
    }

    public IReadOnlyList<ReportDescriptor> Reports { get; } = ReportDescriptor.All;

    public double EffectiveZoom
    {
        get => _effectiveZoom;
        set
        {
            if (!SetProperty(ref _effectiveZoom, value))
                return;

            OnPropertyChanged(nameof(ZoomText));
        }
    }

    public string ZoomText => $"{Math.Round(EffectiveZoom * 100)}%";

    public string FitButtonText => ZoomMode == ReportZoomMode.FitWidth ? "Fit Page" : "Fit Width";

    /// <summary>The page number for the toolbar's entry; text that is not a number is ignored.</summary>
    public string CurrentPageText
    {
        get => CurrentPage.ToString(CultureInfo.CurrentCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var page))
                CurrentPage = Math.Clamp(page, 1, Math.Max(1, PageCount));
        }
    }

    partial void OnSelectedReportChanged(ReportDescriptor value) => LoadReport();

    partial void OnReportChanged(Report? value) => PrintCommand.NotifyCanExecuteChanged();

    partial void OnZoomModeChanged(ReportZoomMode value) => OnPropertyChanged(nameof(FitButtonText));

    partial void OnCurrentPageChanged(int value) => OnPropertyChanged(nameof(CurrentPageText));

    partial void OnPageCountChanged(int value) => OnPropertyChanged(nameof(CurrentPageText));

    private void LoadReport()
    {
        try
        {
            var report = SelectedReport.Create();
            report.Build();

            Report = report;
            CurrentPage = 1;
            ErrorMessage = null;
        }
        catch (Exception exception)
        {
            Report = null;
            ErrorMessage = exception.Message;

            Console.WriteLine($"Pysar: load failed - {exception}");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private async Task PrintAsync()
    {
        if (Report is null || _isBusy)
            return;

        _isBusy = true;
        PrintCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;

        try
        {
            await _printer.PrintAsync(Report);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            PrintCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanPrint() => !_isBusy && Report is not null;

    [RelayCommand]
    private void ToggleFit()
        => ZoomMode = ZoomMode == ReportZoomMode.FitWidth ? ReportZoomMode.FitPage : ReportZoomMode.FitWidth;

    [RelayCommand]
    private void ZoomIn()
    {
        var next = ZoomSteps.FirstOrDefault(step => step > EffectiveZoom + 0.001);
        if (next > 0)
            SetZoom(next);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        var previous = ZoomSteps.LastOrDefault(step => step < EffectiveZoom - 0.001);
        if (previous > 0)
            SetZoom(previous);
    }

    [RelayCommand]
    private void ActualSize() => SetZoom(1);

    private void SetZoom(double zoom)
    {
        Zoom = zoom;
        ZoomMode = ReportZoomMode.Custom;
    }
}
