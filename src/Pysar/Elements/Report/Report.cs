using Pysar.Binding;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Pysar.Elements.Base;

namespace Pysar.Elements;

public class Report : ReportObject, IResourceHost
{
    private bool _buildStarted;
    private Action<int, int>? _pageChanged;

    public Report()
    {
        // Page surface defaults to paper white; element BackgroundColor stays Transparent.
        // At Default precedence: a type's own default must not shadow a later style (see Text).
        using (PushWritePrecedence(ValuePrecedence.Default))
            BackgroundColor = Colors.White;
        Bands = new BandCollection(this);
        Bands.Add(new DetailBand());
    }

    public Metadata Metadata { get; set; } = new();

    public PageFormat PageFormat { get; set; } = new();

    [System.Windows.Markup.Ambient]
    public ResourceDictionary Resources { get; } = new();

    /// <summary>
    /// Fully qualified partial class generated for this report by
    /// Pysar.Xaml.SourceGen.
    /// </summary>
    public string? CodeBehind { get; set; }

    /// <summary>Flow order is type-driven (the paginator derives ReportHeader → Detail → ReportFooter from band types); insertion order carries no semantics.</summary>
    public BandCollection Bands { get; }

    public ReportHeaderBand? ReportHeader => Bands.GetBand<ReportHeaderBand>();
    public PageHeaderBand?   PageHeader   => Bands.GetBand<PageHeaderBand>();
    public DetailBand        Detail       => Bands.GetBand<DetailBand>()!;
    public PageFooterBand?   PageFooter   => Bands.GetBand<PageFooterBand>();
    public ReportFooterBand? ReportFooter => Bands.GetBand<ReportFooterBand>();

    /// <summary>
    ///     1-based index of the page being rendered. The renderer rewrites it before each page, so page-band
    ///     markup can print it with <c>{Binding PageNumber, Source={x:Reference Root}}</c>.
    ///     <para>
    ///     The value lives on the report rather than in a separate per-page context so that it resolves
    ///     against the report's own type: that is what lets the compile-time binding validator check the
    ///     path, and it leaves the page bands' data context free to carry the report's data as it always
    ///     has. Outside a render pass the value is not meaningful.
    ///     </para>
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>Total number of pages. See <see cref="PageNumber"/> for the lifetime caveat.</summary>
    public int PageCount { get; set; }

    public bool IsFirstPage => PageNumber == 1;

    public bool IsLastPage => PageNumber == PageCount;

    /// <summary>
    ///     Called once per page, in page order, after the page bands' bindings have been resolved and
    ///     before the page is measured and drawn. Override it in a report's code-behind to drive named
    ///     elements programmatically, for anything a binding cannot express.
    ///     <para>
    ///     <see cref="PageNumber"/> and <see cref="PageCount"/> are already set, and
    ///     <see cref="PageCount"/> is the real total — the hook runs after pagination, never during the
    ///     seeding measurement.
    ///     </para>
    ///     <para>
    ///     Only elements inside <see cref="PageHeaderBand"/> and <see cref="PageFooterBand"/> can be
    ///     driven this way. The flow bands are measured once and then sliced across pages, so editing an
    ///     element there has no per-page effect.
    ///     </para>
    /// </summary>
    protected virtual Task OnPageChangedAsync(int pageNumber, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    ///     Fluent per-page callback. <paramref name="onPage"/> receives the 1-based page number and the
    ///     real page count after pagination. Only page header/footer elements are re-measured. A second
    ///     call replaces the previous handler.
    /// </summary>
    public Report WithPageChanged(Action<int, int> onPage)
    {
        ArgumentNullException.ThrowIfNull(onPage);
        _pageChanged = onPage;
        return this;
    }

    /// <summary>
    ///     Invokes <see cref="OnPageChangedAsync"/> and the <see cref="WithPageChanged"/> handler.
    ///     The renderer cannot reach either directly.
    /// </summary>
    internal async Task RaisePageChangedAsync(int pageNumber, CancellationToken ct)
    {
        await OnPageChangedAsync(pageNumber, ct);
        _pageChanged?.Invoke(pageNumber, PageCount);
    }

    /// <summary>
    ///     True when a subclass overrides <see cref="OnPageChangedAsync"/> or a
    ///     <see cref="WithPageChanged"/> handler is set. The renderer's fast path skips all per-page work
    ///     for a report with no page-band bindings, which would otherwise swallow the hook entirely.
    /// </summary>
    internal bool HandlesPageChanged =>
        _pageChanged is not null
        || GetType()
            .GetMethod(nameof(OnPageChangedAsync),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.DeclaringType != typeof(Report);

    public Dictionary<string, object> Variables { get; set; } = new();

    public Rect Bounds => CalculateBounds();

    /// <summary>
    ///     Runs the build pipeline on this design: resolve bindings (so a bound root
    ///     <see cref="DetailBand.DataSource"/> is available to expansion), expand repeaters/detail rows, then
    ///     resolve the per-row bindings on the generated rows. Suitable for a design built externally (e.g. from XAML).
    ///     A report instance can be built only once because the pipeline mutates its element tree.
    /// </summary>
    /// <exception cref="InvalidOperationException">The build pipeline has already been started for this report.</exception>
    public Report Build()
    {
        if (_buildStarted)
            throw new InvalidOperationException(
                "This report has already been built. Create a new report instance for each build.");

        _buildStarted = true;

        StyleEngine.Apply(this);

        var engine = new Pysar.Binding.BindingEngine();
        engine.ResolveBindings(Bands, DataContext);
        RepeaterExpander.Expand(this);
        engine.ResolveBindings(Bands, DataContext);
        TriggerEngine.Apply(Bands, DataContext);   // conditional formatting, after per-row contexts/bindings
        return this;
    }

    private Rect CalculateBounds()
    {
        var size = PageFormat.GetPageSizePt();
        return new Rect(0,0,size.Width,size.Height);
    }
}
