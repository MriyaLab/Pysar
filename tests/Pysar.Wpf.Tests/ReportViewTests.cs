using System.Windows;
using System.Windows.Media;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Viewer.Zoom;
using Xunit;
using Font = Pysar.Core.Structs.Font;
using Thickness = Pysar.Core.Structs.Thickness;

namespace Pysar.Wpf.Tests;

/// <summary>
///     What the desktop sample was the only proof of: a report shown in a real window, through the
///     real WPF dispatcher and layout.
/// </summary>
/// <remarks>
///     Input is deliberately not simulated here. Ctrl+wheel reads <see cref="System.Windows.Input.Keyboard.Modifiers"/>,
///     which is the real keyboard's state and cannot be set from a test; the arithmetic behind the
///     gesture is covered framework-neutrally in Pysar.Viewer.Tests, and the Avalonia tests
///     exercise a modified wheel end to end on a platform whose input can be injected.
/// </remarks>
[Collection(WpfCollection.Name)]
public class ReportViewTests(WpfSession session)
{
    [Fact]
    public void SettingAReport_PublishesThePageCountAndAResolvedZoom()
        => session.Run(async () =>
        {
            using var host = await ViewHost.ShowAsync(BuildReport());

            Assert.Equal(1, host.View.PageCount);
            Assert.True(host.View.EffectiveZoom > 0);
        });

    [Fact]
    public void ClearingTheReport_ResetsThePageCount()
        => session.Run(async () =>
        {
            using var host = await ViewHost.ShowAsync(BuildReport());

            host.View.Report = null;

            await host.WaitFor(() => host.View.PageCount == 0, "the page count to fall back to zero");
        });

    [Theory]
    [InlineData(1000d)]
    [InlineData(0.0001d)]
    public void TheZoomProperty_IsClampedToWhatTheZoomModelAllows(double requested)
        => session.Run(async () =>
        {
            using var host = await ViewHost.ShowAsync(BuildReport());

            host.View.ZoomMode = ReportZoomMode.Custom;
            host.View.Zoom = requested;

            Assert.InRange(host.View.Zoom, ZoomModel.MinimumZoom, ZoomModel.MaximumZoom);
        });

    [Fact]
    public void AViewIsUsableAgainAfterBeingTakenOutOfTheTreeAndPutBack()
        => session.Run(async () =>
        {
            // Reparenting looks exactly like a teardown from the control's side; the session is
            // meant to survive it, so a report set afterwards still reaches the screen.
            using var host = await ViewHost.ShowAsync(BuildReport());
            var view = host.View;

            var panel = new System.Windows.Controls.Grid();
            host.Window.Content = panel;
            await host.Settle();

            panel.Children.Add(view);
            await host.Settle();

            view.Report = BuildReport();

            await host.WaitFor(() => view.PageCount == 1, "the reparented view to load a report");
        });

    [Fact]
    public void TheRendererAndExportServiceInstalledByUseQReportAreReachable()
        => session.Run(() =>
        {
            // UseQReport ran once, in the session fixture - this is what a host reaches afterwards.
            Assert.NotNull(QReportWpf.Renderer);
            Assert.NotNull(QReportWpf.ExportService);
            Assert.Same(QReportWpf.ExportService, QReportWpf.ExportService);
        });

    private static Report BuildReport()
        => ReportBuilder.Create("Wpf")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30) })
            .WithDetail(detail => detail.AddElement(
                new Text { Content = "Hello", Font = new Font("Ubuntu", 24) }))
            .Build();

    /// <summary>A shown window holding one <see cref="ReportView"/>, closed with the test.</summary>
    private sealed class ViewHost : IDisposable
    {
        private ViewHost()
        {
            View = new ReportView { Background = Brushes.White };
            Window = new Window
            {
                Width = 400,
                Height = 300,
                // Off-screen and never activated: the tests need layout and the dispatcher, not a
                // window a CI runner has to put anywhere.
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000,
                Top = -10000,
                ShowActivated = false,
                Content = View
            };

            Window.Show();
        }

        public ReportView View { get; }

        public Window Window { get; }

        public static async Task<ViewHost> ShowAsync(Report report)
        {
            var host = new ViewHost();

            host.View.Report = report;

            await host.WaitFor(() => host.View.PageCount > 0, "the report to load");

            return host;
        }

        /// <summary>Waits until <paramref name="condition"/> holds, or fails the test.</summary>
        public async Task WaitFor(Func<bool> condition, string what)
        {
            for (var attempt = 0; attempt < 200 && !condition(); attempt++)
                await Task.Delay(10);

            Assert.True(condition(), $"Timed out waiting for {what}.");
        }

        /// <summary>Lets everything already queued run, for assertions that nothing happened.</summary>
        public Task Settle() => Task.Delay(50);

        public void Dispose() => Window.Close();
    }
}
