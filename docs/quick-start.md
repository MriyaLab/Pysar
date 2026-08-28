# Pysar quick start

This guide shows how to reference and use Pysar. Pysar is pre-1.0; the API and XAML
language may still receive documented breaking changes before the first stable release.

## 1. Reference the packages

A consumer names at most three packages: `Pysar` always, `Pysar.Xaml` for reports
written in `.rxaml` markup, and one platform package for the target UI framework.

```xml
<ItemGroup>
  <PackageReference Include="Pysar" />
  <!-- Only if reports are written in .rxaml markup rather than the fluent API. -->
  <PackageReference Include="Pysar.Xaml" />
</ItemGroup>
```

`Pysar.Xaml` ships its code-behind source generator as an analyzer inside the package
(`analyzers/dotnet/cs`) and wires it up automatically via `build/Pysar.Xaml.props` — no
separate analyzer reference is needed.

Every `.rxaml` file in the project is picked up automatically. To exclude one, use
`<QReport Remove="Draft.rxaml" />`; to turn the automatic pickup off entirely, set
`<EnableDefaultReportItems>false</EnableDefaultReportItems>` and list the files yourself.

## 2. Configure the host platform

Rendering can require platform-specific filesystem and font services:

```csharp
ReportPlatformHandler.Create(new MyReportPlatformHandler());
ReportPlatformHandler.FontCollection.AddFont(
    "Fonts/Inter-Regular.ttf",
    alias: "Inter");
```

Implement `IReportPlatformHandler`, `IFileSystem`, and `IFontCollection` for the host environment. The
platform packages ship one each - `AvaloniaReportPlatformHandler`, `WpfReportPlatformHandler`,
`MauiReportPlatformHandler`, `WasmPlatformHandler` - and their tests show what one has to do.

## 3. Create a report with the fluent API

```csharp
using Pysar.Core.Structs;
using Pysar.Elements;

var rows = new[]
{
    new Product("Keyboard", 79.00m),
    new Product("Mouse", 39.00m)
};

var name = new Text();
name.SetBinding(Text.ContentProperty, nameof(Product.Name));

var price = new Text();
price.SetBinding(Text.ContentProperty, nameof(Product.Price), "{0:C}");

var report = ReportBuilder.Create("Product list")
    .WithPageFormat(new PageFormat { Margin = new Thickness(30) })
    .WithDetail(detail => detail
        .WithDataSource(rows)
        .AddElement(new StackPanel()
            .AddElement(name)
            .AddElement(price)))
    .Build();

public sealed record Product(string Name, decimal Price);
```

`DetailBand` clones its row template for every data record and resolves each clone against that record.
Nested `Repeater` elements and `AddGroup` support master-detail data.

## 4. Load runtime XAML

```csharp
using Pysar.Xaml;

var xaml = """
    <Report xmlns="https://mriyalab.com/pysar"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <PageFormat Size="A4" Margin="30" />
      <DetailBand DataSource="{Binding Products}">
        <StackPanel>
          <Text Content="{Binding Name}" />
          <Text Content="{Binding Price, StringFormat='{0:C}'}" />
        </StackPanel>
      </DetailBand>
    </Report>
    """;

var report = ReportXaml.Load(xaml);
report.DataContext = new ProductReportModel(rows);
report.Build();

public sealed record ProductReportModel(IReadOnlyList<Product> Products);
```

Runtime XAML supports bindings, resources, explicit and implicit styles, data triggers, property
elements, Grid attached properties, and custom CLR namespaces.

## 5. Use XAML code-behind

`ProductReport.rxaml`:

```xml
<Report x:Class="MyApp.ProductReport"
        xmlns="https://mriyalab.com/pysar"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <PageFormat Size="A4" Margin="30" />
  <DetailBand DataSource="{Binding Products}">
    <Text x:Name="ProductName" Content="{Binding Name}" />
  </DetailBand>
</Report>
```

`ProductReport.rxaml.cs`:

```csharp
namespace MyApp;

public partial class ProductReport
{
    public ProductReport(ProductReportModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}
```

The generator reads `x:Class` and supplies the `Report` base type, `InitializeComponent()`, and
strongly typed `x:Name` fields. The legacy `Report.CodeBehind` attribute remains supported for
compatibility. XAML using resources, styles, or triggers automatically uses the runtime-loader
fallback.

## 6. Render a vector PDF

```csharp
using Pysar.Skia;

var renderer = new SkiaReportRenderer();
await renderer.SavePdfAsync(report, "products.pdf");
```

`SavePdfAsync` writes vector text and shapes directly to PDF. For bitmap pages:

```csharp
var pages = await renderer.RenderPageAsync(report, scale: 2f);
```

## Printing

Inject or construct `IReportPrinter` from the host package and call:

```csharp
await printer.PrintAsync(builtReport);
```

- MAUI: registered by `UseQReport` as `IReportPrinter` (`MauiReportPrinter`)
- Avalonia: `new AvaloniaReportPrinter(renderer)` after `UseQReport` (or `QReportAvalonia.Renderer`)
- WPF (Windows only): `new WpfReportPrinter(renderer)` after `UseQReport` (or `QReportWpf.Renderer`)
- Blazor: `BlazorReportPrinter` + `reportPrint.js` (browser print dialog)
- Console sample: `--print` opens OS print/preview for generated PDFs

The report must already have `Build()` called. Printing uses the same vector PDF pipeline as export.

## 7. Register a custom element drawer

```csharp
var renderer = new SkiaReportRenderer()
    .WithDrawer<MyElement>(new MyElementDrawer());
```

Custom elements implement `IReportElement`; custom Skia drawing implements `IElementDrawer`.

## Page numbers

`PageHeaderBand` and `PageFooterBand` are re-resolved for every page. The page position lives on the
report itself — `PageNumber`, `PageCount`, `IsFirstPage`, `IsLastPage` — so name the root and bind to it
with an explicit source:

```xml
<Report x:Class="MyApp.InvoiceReport" x:Name="Root" ...>
  <PageFooterBand Height="25">
    <StackPanel Orientation="Horizontal">
      <Text Content="Page " TextTrimming="None" />
      <Text Content="{Binding PageNumber, Source={x:Reference Root}}" />
      <Text Content=" of " TextTrimming="None" />
      <Text Content="{Binding PageCount, Source={x:Reference Root}}" />
    </StackPanel>
  </PageFooterBand>
</Report>
```

There is no composite format string: "Page 3 of 12" is assembled from the elements above.

`TextTrimming="None"` on the literal separators matters: word wrap splits on spaces and rejoins them,
which would swallow the padding and print "Page 3of12". Both sample reports show the finished footer.

The explicit source is what makes this work in a compiled report (`x:Class`): the build-time binding
validator resolves `Source={x:Reference …}` against the component root's type, so it can check
`PageNumber`. It also leaves the bands' data context alone — an unsourced `{Binding CompanyName}` in a
page band still reads the report's data, exactly as anywhere else.

To use the page number inside a `ReportView` component, pass it down as a component property, the same
way `CompanyHeader` takes its values:

```xml
<PageFooterBand Height="25">
  <views:PageFooter PageNumber="{Binding PageNumber, Source={x:Reference Root}}" />
</PageFooterBand>
```

### Driving page bands from code

For anything a binding cannot express, override `OnPageChangedAsync` in the report's code-behind. It
runs once per page, in order, after the page bands' bindings resolve and before the page is measured —
so what it writes wins over a binding on the same property, and still reaches the layout. The generated
`x:Name` fields are strongly typed, so elements are reached directly rather than looked up:

```csharp
public partial class InvoiceReport
{
    protected override Task OnPageChangedAsync(int pageNumber, CancellationToken ct)
    {
        Stamp.Content = pageNumber == 1 ? "Original" : "Copy";
        Watermark.BackgroundColor = IsLastPage ? Colors.Red : Colors.Transparent;
        return Task.CompletedTask;
    }
}
```

`PageNumber` and `PageCount` are already set when it is called, and `PageCount` is the real total — the
hook never runs during the initial measurement.

**Only page-band elements can be driven this way.** The flow bands (`ReportHeader`, `Detail`,
`ReportFooter`) are measured once and then sliced across pages, so editing an element there from the
hook has no per-page effect.

**Give page bands an explicit `Height`.** The height a page band reserves in the content zone is
measured once, before the page count is known, and never recomputed — that is what stops pagination
from depending on its own result. A band sized by auto-height content that wraps to an extra line at
`"127 of 340"` but not at `"1 of 1"` will have that extra line clipped.

Two limits worth knowing: page numbers are available in page bands only, not in `ReportHeader`,
`Detail`, or `ReportFooter`; and data triggers on page bands are evaluated once at build time against
the report data, not per page.

## Report lifetime

`Build()` resolves bindings, expands detail rows and repeaters, and applies data triggers by mutating the
report tree. It is intentionally single-use:

```csharp
var first = templateFactory().Build();
var second = templateFactory().Build();
```

Do not call `Build()` twice on the same `Report`. Do not build one report instance concurrently.

## Design-time preview

Both IDE plugins render `.rxaml` reports with the same `SkiaReportRenderer` pipeline that produces
the PDF: Rider shows them in a split editor when the Pysar Designer plugin is installed,
and VS Code in a panel opened with **QReport: Open Preview** when the Pysar Designer
extension is installed (also an icon in the editor title bar). Both drive the same renderer, so the contract
below is shared — set it up once and the report previews in either editor.

To get a preview for a report, provide three things.

**1. An application bootstrap.** Implement `IReportBootstrap` (`Pysar.Skia`) once per
application and call it from your own entry point. It performs the registrations every render
needs — platform handler, fonts, custom drawers:

```csharp
public sealed class ReportBootstrap : IReportBootstrap
{
    public static void Initialize(SkiaReportRenderer renderer)
    {
        ReportPlatformHandler.Create(new ConsolePlatformHandler());
        ReportPlatformHandler.FontCollection.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu");
        renderer.WithDrawer<QRCode>(new QRCodeDrawer());
    }
}
```

The preview host discovers this implementation by reflection instead of running your `Main`, so it
never triggers your application's own startup side effects. Zero implementations renders with
defaults; more than one is reported as an error in the preview panel. A report class may also
declare its own optional `public static void Initialize(SkiaReportRenderer renderer)`, which runs
after the application bootstrap.

**2. A design-time instance of the data model.** Implement `IDesignTimeCreatable<T>`
(`Pysar.Core.Abstractions`) on the model bound to the report:

```csharp
public sealed record Invoice(...) : IDesignTimeCreatable<Invoice>
{
    public static Invoice CreateDesignInstance() => new(/* sample data */);
}
```

**3. A design-time data context in the markup root.** Bind the `d` prefix to the Blend design-time
namespace and reference the model type:

```xml
<Report x:Class="MyApp.Reports.InvoiceReport"
        xmlns="https://mriyalab.com/pysar"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:reports="clr-namespace:MyApp.Reports;assembly=MyApp"
        d:DataContext="{d:DesignInstance Type=reports:Invoice, IsDesignTimeCreatable=True}">
```

`IsDesignTimeCreatable=False`, or no `d:DataContext` at all, renders with a null data context.

The host builds the report the same way the application would: it prefers a constructor whose
single parameter accepts the design instance (`InvoiceReport(Invoice)`), otherwise it uses the
parameterless constructor and assigns `DataContext` directly.

The preview renders the **last built** assemblies, not the current source. A C# change (including
to `CreateDesignInstance` or `IReportBootstrap`) requires a rebuild before it appears; a markup-only
change refreshes on save, no build needed. If the project has never been built, the panel shows
"Build the project to enable preview"; if the build is older than the `.cs` sources, the last render
stays visible under a "Rebuild to update" banner.

The arrangement this describes — a platform-neutral library holding the `.rxaml` reports, their
code-behind and one `IReportBootstrap`, referenced by each host application — is what
`tests/Pysar.Xaml.CodeBehind.Tests` and `tests/Pysar.Skia.Tests` exercise end to end.

## IDE completion

The generated QReport XSD provides element, attribute, and enum completion in XML-aware editors.
Rider and VS Code installation instructions, along with the Visual Studio roadmap, are available in
the `Pysar.Plugins` repository (sibling project, not part of this repo).
