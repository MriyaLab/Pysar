namespace Pysar.Viewer;

/// <summary>Whether writing one axis of the content's size to the host is worth doing.</summary>
public static class ExtentWrite
{
    /// <summary>Below this, a difference is smaller than anything a layout pass can act on.</summary>
    private const double Tolerance = 0.5;

    /// <summary>
    ///     Whether this axis of the content's size has to be written.
    /// </summary>
    /// <remarks>
    ///     Both arms are load-bearing. Writing an unchanged value invalidates the layout, which
    ///     raises a size change, which lands back in the caller: that loop never finishes a pass and
    ///     the window stays blank, with no exception to show for it. And an unset size is
    ///     <see cref="double.NaN"/> in WPF and Avalonia, where every comparison against it is false
    ///     - so a guard written as a difference alone never fires at all, the content is never given
    ///     a size, and the scroll viewer concludes there is nothing to scroll. MAUI escaped that one
    ///     only because an unset <c>WidthRequest</c> there is -1.
    ///
    ///     The NaN check is deliberately one-directional: only <paramref name="current"/> is tested,
    ///     not <paramref name="wanted"/>. <paramref name="current"/> is whatever the platform holds
    ///     and can legitimately be unset; <paramref name="wanted"/> comes from the presenter and is a
    ///     real number by contract - <see cref="ReportViewPresenter.Update"/> always passes a
    ///     <see cref="Math.Max"/> of measured values. If that contract ever broke and
    ///     <paramref name="wanted"/> arrived as NaN, this would write the size once and then never
    ///     again, since every later comparison against a NaN <paramref name="current"/> would again
    ///     be true - not the same failure as the one this guard exists for, but a related one, and
    ///     one that is not worth guarding against a case that cannot occur today.
    /// </remarks>
    public static bool Needed(double current, double wanted)
        => double.IsNaN(current) || Math.Abs(current - wanted) >= Tolerance;
}
