namespace Pysar.Skia;

/// <summary>
///     Implemented once per application to perform the registrations every report render needs:
///     the platform handler, fonts, and custom element drawers. The application calls it from its
///     own entry point; design-time tooling discovers and calls the same implementation, because
///     it must never execute the application's <c>Main</c>.
/// </summary>
public interface IReportBootstrap
{
    /// <summary>Registers platform services, fonts and custom drawers on <paramref name="renderer"/>.</summary>
    static abstract void Initialize(SkiaReportRenderer renderer);
}
