namespace Pysar.Export;

/// <summary>
///     The output format an export produces, identified by a short lowercase id that is also the
///     conventional file extension.
/// </summary>
/// <remarks>
///     Deliberately not an enum: a format project that ships separately - docx, xlsx, png - has to be
///     able to name its own format without an edit to this package, and adding an enum member would
///     be a breaking change for anyone switching over one.
/// </remarks>
public readonly record struct ExportFormat
{
    private readonly string? _id;

    /// <param name="id">
    ///     A short format id, compared case-insensitively so one format cannot be registered twice
    ///     under two spellings.
    /// </param>
    public ExportFormat(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _id = id.ToLowerInvariant();
    }

    /// <summary>The lowercase format id. Empty for a default-constructed value, which matches no format.</summary>
    public string Id => _id ?? "";

    public static readonly ExportFormat Pdf = new("pdf");

    public override string ToString() => Id;
}
