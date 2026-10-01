namespace Pysar.Export;

/// <summary>Settings for <see cref="ExportFormat.Pdf"/>.</summary>
public sealed record PdfExportOptions : ExportOptions
{
    /// <summary>
    ///     When <see langword="true"/>, Skia writes the PDF/A-2b document setup: XMP
    ///     <c>pdfaid</c> identification, a document UUID, and an sRGB output intent.
    ///     This does not validate the file. A font Skia cannot embed still fails a checker,
    ///     and the UUID makes the bytes non-reproducible.
    /// </summary>
    public bool PdfA { get; init; }
}
