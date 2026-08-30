using Microsoft.Extensions.Logging;
using Pysar.Maui.Sample.Services;
using Pysar.Maui.Sample.ViewModels;
using Pysar.Maui.Sample.Views;
using Pysar.Sample.Reports;
using Pysar.Sample.Reports.QRCode;

namespace Pysar.Maui.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UsePysar(pysar => pysar
				.RegisterFonts(ReportBootstrap.RegisterFonts)
				.AddDrawer<QRCode>(new QRCodeDrawer()))
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("Cookie-Regular.ttf", "CookieRegular");
				fonts.AddFont("Font Awesome 6 Free-Regular-400.otf", "FontAwesomeRegular");
				fonts.AddFont("Font Awesome 6 Free-Solid-900.otf", "FontAwesomeSolid");
			});

		builder.Services.AddTransient<AppShell>();
		builder.Services.AddTransient<Func<ReportDescriptor, ReportViewerViewModel>>(services =>
			descriptor => ActivatorUtilities.CreateInstance<ReportViewerViewModel>(services, descriptor));

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
