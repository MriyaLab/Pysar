using Pysar.Viewer.Geometry;

namespace Pysar.Viewer.Zoom;

/// <summary>
///     What a finished pinch has to be paid for with: the factor it reached, and the point it
///     was anchored on.
/// </summary>
/// <remarks>
///     Carried out of <see cref="PinchSession.End"/> together rather than left on the session for
///     the caller to read first. The relayout that ends a gesture needs the anchor and the held
///     point the gesture was shown around, and a session that cleared them on ending made reading
///     them beforehand a rule no signature expressed.
/// </remarks>
public readonly record struct PinchCommit(double Factor, ViewPoint Anchor, DocumentPoint? Held);

/// <summary>
///     One pinch, from its first frame to its last: what it holds, how far it has scaled, and
///     whether it moved far enough to be worth paying a relayout for.
/// </summary>
/// <remarks>
///     A pinch relays nothing out while it runs. Each frame is shown by scaling what is already
///     drawn - <see cref="ReportViewPresenter.PreviewZoom"/> works out the transform - and one
///     relayout at the end pays for the zoom it reached. Relaying out per frame instead left the
///     view showing pixels of the previous zoom wherever the geometry had moved away from: blank
///     bands at the top of a page, a header ghosted into the gap below it, surviving until a scroll
///     disturbed them.
///     <para>
///     What this type deliberately does not do is apply or drop the transform. When the transform is
///     dropped is the one part that is genuinely different on every host - MAUI must drop it before
///     the relayout, because its anchor is a fraction of the view size and resizing the content
///     under a non-1 scale moves that anchor in pixels; Avalonia must drop it after, because
///     dropping it before the scroll lands paints one frame of pages at the new zoom under the old
///     offset; Blazor drops it after the next render, without awaiting. <see cref="End"/> therefore
///     reports what to commit and leaves the sequence to the caller.
///     </para>
/// </remarks>
public sealed class PinchSession(ReportViewPresenter presenter)
{
    /// <summary>The zoom the gesture began at, which <see cref="Scale"/> is measured against.</summary>
    private double _startZoom = 1;

    /// <summary>Set between the gesture's first frame and its last.</summary>
    public bool Running { get; private set; }

    /// <summary>How far the gesture has scaled the document, and 1 when none is running.</summary>
    public double Scale { get; private set; } = 1;

    /// <summary>
    ///     The point of the viewport the gesture is anchored at, fixed for the whole gesture.
    /// </summary>
    /// <remarks>
    ///     Not taken per frame. What a platform reports with a magnify is the centroid of the
    ///     fingers, and that wanders as they move, so scaling each frame about the point that frame
    ///     reported adds a little translation every time and they accumulate into a visible drift.
    /// </remarks>
    public ViewPoint Anchor { get; private set; }

    /// <summary>
    ///     The point of the report the gesture holds, resolved once when it began, or <c>null</c>
    ///     when there was no document under the anchor.
    /// </summary>
    public DocumentPoint? Held { get; private set; }

    /// <summary>Starts a gesture anchored at <paramref name="anchor"/>, a point of the viewport.</summary>
    public void Begin(ViewPoint anchor)
    {
        Scale = 1;
        _startZoom = presenter.EffectiveZoom;
        Anchor = anchor;
        Held = presenter.PointAt(anchor);
        Running = true;
    }

    /// <summary>
    ///     A frame reported as a step against the previous frame of the same gesture, which is what
    ///     MAUI's cross-platform recogniser, Android's <c>ScaleGestureDetector</c> and AppKit's
    ///     magnify give.
    /// </summary>
    /// <returns>How to draw the frame, or <c>null</c> when there is nothing to show.</returns>
    public ZoomPreview? MoveByStep(double step) => Move(Scale * step);

    /// <summary>
    ///     A frame reported as a scale against the zoom the gesture began at, which is what
    ///     <c>UIPinchGestureRecognizer</c> and the browser's accumulated wheel pinch give.
    /// </summary>
    /// <returns>How to draw the frame, or <c>null</c> when there is nothing to show.</returns>
    public ZoomPreview? MoveByScale(double scale) => Move(scale);

    /// <summary>
    ///     Ends the gesture.
    /// </summary>
    /// <returns>
    ///     What to commit, or <c>null</c> when the gesture changed too little to be worth a relayout
    ///     - in which case the caller only has to drop its preview. <see cref="PinchCommit.Factor"/>
    ///     is measured against the zoom the gesture began at, so it belongs through
    ///     <see cref="GestureModel.PinchByScale"/> and requires the caller to have called
    ///     <see cref="GestureModel.BeginPinch"/> first, the same as this session's own
    ///     <see cref="Begin"/>.
    /// </returns>
    public PinchCommit? End()
    {
        var factor = Scale;
        var anchor = Anchor;
        var held = Held;

        Scale = 1;
        Held = null;
        Running = false;

        if (Math.Abs(factor - 1) < ReportViewDefaults.ZoomStepThreshold)
            return null;

        return new PinchCommit(factor, anchor, held);
    }

    private ZoomPreview? Move(double factor)
    {
        // A NaN passes every comparison against it as false, so a `factor <= 0` guard alone would
        // let a NaN step through undetected; checking finiteness first closes that.
        if (!Running || !double.IsFinite(factor) || factor <= 0)
            return null;

        if (presenter.PreviewZoom(factor, Anchor, Held) is not { } preview)
            return null;

        // Read back from the zoom the presenter settled on rather than clamped here: past the
        // limits the drawing would keep growing while the zoom behind it had stopped, and the
        // release would give that back in one jump. Two copies of the same limits would only have
        // to disagree once for that to come back unnoticed.
        Scale = preview.Zoom / _startZoom;

        return preview;
    }
}
