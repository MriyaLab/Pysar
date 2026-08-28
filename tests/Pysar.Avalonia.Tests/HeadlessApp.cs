using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Xunit;

namespace Pysar.Avalonia.Tests;

/// <summary>
///     The application the control tests run inside: a real Avalonia app with a real dispatcher and
///     input pipeline, driven by the headless platform so nothing needs a window server.
/// </summary>
/// <remarks>
///     <c>UseQReport</c> is part of the setup rather than a test of its own on purpose - it is what a
///     host application does once at startup, and everything below (asset resolution, the font the
///     reports use, the renderer <see cref="ReportView"/> measures with) only exists because of it.
///     The assembly name is passed explicitly: the default reads the entry assembly, which under a
///     test host is the runner, not the assembly carrying the report assets.
/// </remarks>
public sealed class HeadlessApp : Application
{
    /// <summary>The assembly the report assets in this project are packaged under.</summary>
    public static string AssetAssemblyName { get; } =
        typeof(HeadlessApp).Assembly.GetName().Name!;

    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessApp>()
            .UseSkia()
            .UseQReport(
                qreport => qreport.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu"),
                AssetAssemblyName)
            // Real drawing rather than the headless stub: the report view puts its tiles on screen
            // through WriteableBitmap, which the stub backend has no implementation for.
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

/// <summary>
///     One Avalonia session for the whole assembly. Avalonia is single-instance and single-threaded:
///     every test runs on that one UI thread, through <see cref="Run"/>.
/// </summary>
public sealed class HeadlessSession : IDisposable
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));

    /// <summary>Runs <paramref name="body"/> on the Avalonia UI thread and rethrows what it threw.</summary>
    public void Run(Action body) => _session.Dispatch(body, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>The asynchronous counterpart, for tests that have to wait for a load or a render.</summary>
    public void Run(Func<Task> body)
        => _session.Dispatch(async () =>
        {
            await body();

            return true;
        }, CancellationToken.None).GetAwaiter().GetResult();

    public void Dispose() => _session.Dispose();
}

[CollectionDefinition(Name)]
public sealed class HeadlessCollection : ICollectionFixture<HeadlessSession>
{
    public const string Name = "avalonia-headless";
}
