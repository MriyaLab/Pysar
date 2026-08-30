using Pysar.Maui.Sample.Services;
using Pysar.Maui.Sample.ViewModels;
using Pysar.Maui.Sample.Views;

namespace Pysar.Maui.Sample;

public partial class AppShell : Shell
{
    public AppShell(Func<ReportDescriptor, ReportViewerViewModel> viewModelFactory)
    {
        InitializeComponent();

        for (var index = 0; index < ReportDescriptor.All.Count; index++)
        {
            var descriptor = ReportDescriptor.All[index];
            Items.Add(new ShellContent
            {
                Title = descriptor.Title,
                Route = $"report-{index}",
                // FlyoutGlyph holds a Font Awesome solid ligature name; the icon only
                // renders because the matching FA font is registered in MauiProgram.
                Icon = new FontImageSource
                {
                    FontFamily = "FontAwesomeSolid",
                    Glyph = descriptor.FlyoutGlyph,
                    Size = 20
                },
                ContentTemplate = new DataTemplate(
                    () => new ReportViewerPage(viewModelFactory(descriptor)))
            });
        }
    }
}
