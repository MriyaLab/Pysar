namespace Pysar.Viewer.Tiles;

/// <summary>
///     Waits for the view to stop moving, then asks for the cells that belong to where it stopped.
/// </summary>
/// <remarks>
///     A zoom or a resize invalidates every cell at once, and a gesture raises an event per frame,
///     so following each one would render several screens' worth of pixels that are stale before
///     they arrive. This notes the time and lets one already-running loop do the waiting -
///     cancelling and allocating a token per event instead put that churn on the user-interface
///     thread during the very gesture it was meant to keep smooth.
///     <para>
///     Call it from the thread the view belongs to. <c>Task.Delay</c> resumes on the context it was
///     awaited from, so the request runs where the host can act on it, and nothing here needs a
///     lock: it is only ever touched from that one thread.
///     </para>
/// </remarks>
public sealed class TileSettleTimer(Action request, TimeSpan? delay = null)
{
    private readonly TimeSpan _delay = delay ?? ReportViewDefaults.SettleDelay;

    private DateTime _lastChange;
    private bool _settling;

    /// <summary>Raised when the request threw.</summary>
    public event Action<Exception>? Failed;

    /// <summary>Notes that the view moved, and asks for cells once it has stopped.</summary>
    public async Task ScheduleAsync()
    {
        _lastChange = DateTime.UtcNow;

        if (_settling)
            return;

        _settling = true;
        try
        {
            while (DateTime.UtcNow - _lastChange < _delay)
                await Task.Delay(_delay);

            request();
        }
        catch (Exception exception)
        {
            Failed?.Invoke(exception);
        }
        finally
        {
            _settling = false;
        }
    }
}
