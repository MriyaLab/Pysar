using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Pysar.Elements;
using Pysar.Export;
using Pysar.Maui.Sample.Services;
using Pysar.Skia;
using Pysar.Viewer.Zoom;

namespace Pysar.Maui.Sample.ViewModels;

/// <summary>Drives the viewer toolbar and the PDF export for one report.</summary>
public sealed class ReportViewerViewModel : INotifyPropertyChanged
{
    /// <summary>The zoom levels the minus and plus buttons step through, as a browser does.</summary>
    private static readonly double[] ZoomSteps =
        [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5];

    private readonly IReportExportService _exporter;
    private readonly IReportSharer _sharer;
    private readonly IReportPrinter _printer;

    private readonly ReportDescriptor _reportDescriptor;
    private Report? _report;
    private ReportZoomMode _zoomMode = ReportZoomMode.FitWidth;
    private double _effectiveZoom = 1;
    private double _zoom = 1;
    private int _currentPage = 1;
    private int _pageCount;
    private bool _isBusy;
    private string? _errorMessage;

    public ReportViewerViewModel(
        ReportDescriptor reportDescriptor,
        IReportExportService exporter,
        IReportSharer sharer,
        IReportPrinter printer)
    {
        _reportDescriptor = reportDescriptor;
        _exporter = exporter;
        _sharer = sharer;
        _printer = printer;

        ExportPdfCommand = new Command(async () => await ExportPdfAsync(), () => !IsBusy);
        PrintCommand = new Command(async () => await PrintAsync(), () => !IsBusy);
        ZoomInCommand = new Command(ZoomIn);
        ZoomOutCommand = new Command(ZoomOut);
        ActualSizeCommand = new Command(() => SetZoom(1));
        ToggleFitCommand = new Command(ToggleFit);

        LoadReport();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ExportPdfCommand { get; }

    public ICommand PrintCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand ActualSizeCommand { get; }

    public ICommand ToggleFitCommand { get; }

    /// <summary>The built report the viewer shows.</summary>
    public Report? Report
    {
        get => _report;
        private set => SetField(ref _report, value);
    }

    public ReportZoomMode ZoomMode
    {
        get => _zoomMode;
        set
        {
            if (!SetField(ref _zoomMode, value))
                return;

            OnPropertyChanged(nameof(FitButtonText));
        }
    }

    /// <summary>The zoom asked for; meaningful only while the mode is Custom.</summary>
    public double Zoom
    {
        get => _zoom;
        set => SetField(ref _zoom, value);
    }

    /// <summary>
    ///     The zoom the viewer actually settled on, reported back by the control. A fit mode resolves
    ///     to a factor only the control knows, so the percentage has to come from there.
    /// </summary>
    public double EffectiveZoom
    {
        get => _effectiveZoom;
        set
        {
            if (!SetField(ref _effectiveZoom, value))
                return;

            OnPropertyChanged(nameof(ZoomText));
        }
    }

    public string ZoomText => $"{Math.Round(EffectiveZoom * 100)}%";

    public string FitButtonText => ZoomMode == ReportZoomMode.FitWidth ? "Fit Page" : "Fit Width";

    public int CurrentPage
    {
        get => _currentPage;
        set
        {
            if (!SetField(ref _currentPage, value))
                return;

            OnPropertyChanged(nameof(CurrentPageText));
        }
    }

    /// <summary>The page number for the toolbar's entry; text that is not a number is ignored.</summary>
    public string CurrentPageText
    {
        get => _currentPage.ToString(CultureInfo.CurrentCulture);
        set
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out var page))
                CurrentPage = Math.Clamp(page, 1, Math.Max(1, PageCount));
        }
    }

    public int PageCount
    {
        get => _pageCount;
        set => SetField(ref _pageCount, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetField(ref _isBusy, value))
                return;

            OnPropertyChanged(nameof(IsNotBusy));
            ((Command)ExportPdfCommand).ChangeCanExecute();
            ((Command)PrintCommand).ChangeCanExecute();
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetField(ref _errorMessage, value);
    }

    private void LoadReport()
    {
        try
        {
            var report = _reportDescriptor.Create();
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

    private void ToggleFit()
        => ZoomMode = ZoomMode == ReportZoomMode.FitWidth ? ReportZoomMode.FitPage : ReportZoomMode.FitWidth;

    private void ZoomIn()
    {
        var next = ZoomSteps.FirstOrDefault(step => step > EffectiveZoom + 0.001);
        if (next > 0)
            SetZoom(next);
    }

    private void ZoomOut()
    {
        var previous = ZoomSteps.LastOrDefault(step => step < EffectiveZoom - 0.001);
        if (previous > 0)
            SetZoom(previous);
    }

    private void SetZoom(double zoom)
    {
        Zoom = zoom;
        ZoomMode = ReportZoomMode.Custom;
    }

    private async Task ExportPdfAsync()
    {
        if (IsBusy || Report is null)
            return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var bytes = await _exporter.ExportAsync(Report, ExportFormat.Pdf);
            await _sharer.ShareAsync(bytes, _reportDescriptor.FileName, $"{_reportDescriptor.Title} report");
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;

            Console.WriteLine($"Pysar: export failed - {exception}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PrintAsync()
    {
        if (IsBusy || Report is null)
            return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await _printer.PrintAsync(Report);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            Console.WriteLine($"Pysar: print failed - {exception}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
