namespace Pysar.Core;

/// <summary>
///     Process-wide switch set by design-time tooling (the preview host). When enabled, the
///     generated <c>InitializeComponent</c> skips loading the compiled markup so the tooling
///     can load the current markup from disk instead.
/// </summary>
public static class ReportDesignMode
{
    /// <summary>True while the process renders reports for a designer rather than for an application.</summary>
    public static bool IsEnabled { get; private set; }

    public static void Enable() => IsEnabled = true;

    public static void Disable() => IsEnabled = false;
}
