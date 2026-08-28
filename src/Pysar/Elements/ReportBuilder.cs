using Pysar.Core.Abstractions;
using Pysar.Core.Structs;

namespace Pysar.Elements;

public class ReportBuilder
{
    private readonly Report _reportDesign = new();

    private ReportBuilder(string title)
    {
        _reportDesign.Metadata.Title = title;
    }

    public static ReportBuilder Create(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        return new(title);
    }

    public ReportBuilder WithAuthor(string author)
    {
        ArgumentNullException.ThrowIfNull(author);
        _reportDesign.Metadata.Author = author;
        return this;
    }

    public ReportBuilder WithPageFormat(PageFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        _reportDesign.PageFormat = format;
        return this;
    }

    /// <summary>
    ///     Sets the report-level data context: the object top-level bindings resolve against, including a
    ///     bound root <see cref="DetailBand.DataSource"/> (e.g. a report view model exposing the data).
    /// </summary>
    public ReportBuilder WithDataContext(object dataContext)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        _reportDesign.DataContext = dataContext;
        return this;
    }

    public ReportBuilder WithResources(ResourceDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        foreach (var entry in dictionary)
            _reportDesign.Resources[entry.Key] = entry.Value;
        return this;
    }

    public ReportBuilder Configure(Action<Report> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(_reportDesign);
        return this;
    }

    public ReportBuilder WithReportHeader(Action<ReportHeaderBand> configure) => WithBand(configure);
    public ReportBuilder WithPageHeader(Action<PageHeaderBand> configure) => WithBand(configure);
    public ReportBuilder WithDetail(Action<DetailBand> configure) => WithBand(configure);
    public ReportBuilder WithPageFooter(Action<PageFooterBand> configure) => WithBand(configure);
    public ReportBuilder WithReportFooter(Action<ReportFooterBand> configure) => WithBand(configure);

    private ReportBuilder WithBand<TBand>(Action<TBand> configure) where TBand : Band, new()
    {
        ArgumentNullException.ThrowIfNull(configure);
        var band = _reportDesign.Bands.GetBand<TBand>();
        if (band is null)
        {
            band = new TBand();
            _reportDesign.Bands.Add(band);
        }
        configure(band);
        return this;
    }

    public Report Build() => _reportDesign.Build();
}
