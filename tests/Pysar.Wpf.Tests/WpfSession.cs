using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace Pysar.Wpf.Tests;

/// <summary>
///     One WPF application for the whole assembly, on the single STA thread WPF requires. Every test
///     runs on that thread through <see cref="Run(Action)"/>.
/// </summary>
/// <remarks>
///     <c>UsePysar</c> is part of the setup rather than a test of its own on purpose - it is what a
///     host application does once at startup, and everything below (asset resolution, the font the
///     reports use, the renderer the report view measures with) only exists because of it. The
///     assembly name is passed explicitly: the default reads the entry assembly, which under a test
///     host is the runner, not the assembly carrying the report assets.
/// </remarks>
public sealed class WpfSession : IDisposable
{
    private readonly Dispatcher _dispatcher;

    public WpfSession()
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            // OnExplicitShutdown: closing the last test window must not tear the application down
            // while later tests still need it.
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

            application.UsePysar(
                pysar => pysar.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu"),
                AssetAssemblyName);

            ready.SetResult(Dispatcher.CurrentDispatcher);

            Dispatcher.Run();
        })
        {
            IsBackground = true
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        _dispatcher = ready.Task.GetAwaiter().GetResult();
    }

    /// <summary>The assembly the report assets in this project are packaged under.</summary>
    public static string AssetAssemblyName { get; } = typeof(WpfSession).Assembly.GetName().Name!;

    /// <summary>Runs <paramref name="body"/> on the WPF thread and rethrows what it threw.</summary>
    public void Run(Action body) => _dispatcher.Invoke(body);

    /// <summary>
    ///     The asynchronous counterpart, for tests that have to wait for a load or a render. The body
    ///     runs on the WPF thread, which keeps pumping inside <see cref="Dispatcher.Run"/> while it
    ///     awaits, so its continuations are not waiting on the caller.
    /// </summary>
    public void Run(Func<Task> body)
        => _dispatcher.InvokeAsync(body).Task.Unwrap().GetAwaiter().GetResult();

    public void Dispose() => _dispatcher.InvokeShutdown();
}

[CollectionDefinition(Name)]
public sealed class WpfCollection : ICollectionFixture<WpfSession>
{
    public const string Name = "wpf";
}
