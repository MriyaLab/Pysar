namespace Pysar.Viewer.Geometry;

/// <summary>
///     A rectangle in page points - the units a report is laid out in, independent of zoom or of the
///     units a host measures its own views in.
/// </summary>
/// <remarks>
///     Edges rather than origin-and-size, because the callers that use it work in edges, and because
///     the renderer it is handed to does too. It is deliberately not a framework rectangle: this type
///     travels through <see cref="Pysar.Viewer.IReportViewHost"/>, so a host implementing
///     that interface for a new UI framework should not have to reference the rendering backend to
///     read one.
/// </remarks>
public readonly record struct RectPt(double Left, double Top, double Right, double Bottom)
{
    public double Width => Right - Left;
    public double Height => Bottom - Top;
}
