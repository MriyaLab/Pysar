using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Viewer.Zoom;
using Xunit;
// Report structs and Avalonia primitives share several names; the report side is aliased because
// this file is an Avalonia control test first.
using Font = Pysar.Core.Structs.Font;
using Thickness = Pysar.Core.Structs.Thickness;

namespace Pysar.Avalonia.Tests;

/// <summary>
///     What the desktop sample was the only proof of: a report shown in a real window, zoomed with
///     the input a reader actually uses, through the real Avalonia dispatcher and input pipeline.
/// </summary>
[Collection(HeadlessCollection.Name)]
public class ReportViewTests(HeadlessSession session)
{
    /// <summary>The point every gesture below is aimed at - the middle of the 400x300 window.</summary>
    private static readonly Point Centre = new(200, 150);

    [Fact]
    public void SettingAReport_PublishesThePageCountAndAResolvedZoom()
        => session.Run(async () =>
        {
            using var host = await ShowAsync(BuildReport());

            Assert.Equal(1, host.View.PageCount);
            Assert.True(host.View.EffectiveZoom > 0);
        });

    [Fact]
    public void ClearingTheReport_ResetsThePageCount()
        => session.Run(async () =>
        {
            using var host = await ShowAsync(BuildReport());

            host.View.Report = null;

            await host.WaitFor(() => host.View.PageCount == 0, "the page count to fall back to zero");
        });

    [Fact]
    public void AModifiedWheelNotch_ZoomsInsteadOfScrolling()
        => session.Run(async () =>
        {
            using var host = await ShowAsync(BuildReport());

            var before = host.View.EffectiveZoom;

            host.Window.MouseWheel(Centre, new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();

            await host.WaitFor(() => host.View.EffectiveZoom > before, "the wheel notch to zoom in");

            // A wheel notch always lands on a factor of its own, never back on a fit mode.
            Assert.Equal(ReportZoomMode.Custom, host.View.ZoomMode);
        });

    [Fact]
    public void APlainWheelNotch_IsLeftToTheScrollViewer()
        => session.Run(async () =>
        {
            using var host = await ShowAsync(BuildReport());

            var mode = host.View.ZoomMode;
            var before = host.View.EffectiveZoom;

            host.Window.MouseWheel(Centre, new Vector(0, 1), RawInputModifiers.None);
            await host.Settle();

            Assert.Equal(before, host.View.EffectiveZoom);
            Assert.Equal(mode, host.View.ZoomMode);
        });

    [Theory]
    [InlineData(1000d)]
    [InlineData(0.0001d)]
    public void TheZoomProperty_IsClampedToWhatTheZoomModelAllows(double requested)
        => session.Run(async () =>
        {
            using var host = await ShowAsync(BuildReport());

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
            using var host = await ShowAsync(BuildReport());
            var view = host.View;

            var panel = new Panel();
            host.Window.Content = panel;
            await host.Settle();

            panel.Children.Add(view);
            await host.Settle();

            view.Report = BuildReport();

            await host.WaitFor(() => view.PageCount == 1, "the reparented view to load a report");
        });

    private static Report BuildReport()
        => ReportBuilder.Create("Headless")
            .WithPageFormat(new PageFormat { Margin = new Thickness(30) })
            .WithDetail(detail => detail.AddElement(
                new Text { Content = "Hello", Font = new Font("Ubuntu", 24) }))
            .Build();

    private static async Task<ViewHost> ShowAsync(Report report)
    {
        var host = new ViewHost();

        host.View.Report = report;

        await host.WaitFor(() => host.View.PageCount > 0, "the report to load");

        return host;
    }

    /// <summary>A shown window holding one <see cref="ReportView"/>, closed with the test.</summary>
    private sealed class ViewHost : IDisposable
    {
        public ViewHost()
        {
            // A background is what makes the view hit-testable, so a wheel notch aimed at the middle
            // of the window reaches the scroll viewer rather than falling through to the window.
            View = new ReportView { Background = Brushes.White };
            Window = new Window { Width = 400, Height = 300, Content = View };

            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        public ReportView View { get; }

        public Window Window { get; }

        /// <summary>Pumps the dispatcher until <paramref name="condition"/> holds, or fails the test.</summary>
        public async Task WaitFor(Func<bool> condition, string what)
        {
            for (var attempt = 0; attempt < 200 && !condition(); attempt++)
            {
                Dispatcher.UIThread.RunJobs();

                await Task.Delay(10);
            }

            Assert.True(condition(), $"Timed out waiting for {what}.");
        }

        /// <summary>Lets everything already queued run, for assertions that nothing happened.</summary>
        public async Task Settle()
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                Dispatcher.UIThread.RunJobs();

                await Task.Delay(10);
            }

            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => Window.Close();
    }
}
