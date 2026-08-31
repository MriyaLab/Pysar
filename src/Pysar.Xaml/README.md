# Pysar.Xaml

Declarative report markup for [Pysar](https://github.com/MriyaLab/Pysar), a cross-platform report
engine for .NET: this package contains the runtime `.rxaml` loader and the code-behind source
generator. It depends on the core `Pysar` package, which carries the element tree, data binding and
the SkiaSharp rendering and PDF engine.

`.rxaml` is Pysar's markup dialect: an XML document that describes a report the same way the fluent
builder does, using the same elements (`Grid`, `StackPanel`, `Frame`, `Text`, `Image`, `Repeater`)
and the same bands. It is the better fit for a report with a fixed shape — an invoice, a statement, a
certificate — because the layout stays readable as a tree instead of a chain of method calls, and it
is what makes a report editable by someone who is not writing C#: a template stored in a database, or
edited through the design-time preview in Rider and VS Code.

```xml
<Report xmlns="https://mriyalab.com/pysar" x:DataType="local:Invoice">
  <PageFormat Size="A4" Margin="30" />

  <ReportHeaderBand>
    <Text Content="{Binding Company.Name}" FontSize="18" FontStyle="Bold" />
  </ReportHeaderBand>

  <DetailBand DataSource="{Binding Items}">
    <DetailBand.DetailHeader>
      <Text Content="Product" FontStyle="Bold" />
    </DetailBand.DetailHeader>

    <Grid ColumnDefinitions="*, 80, 80">
      <Text Grid.Column="0" Content="{Binding Product}" />
      <Text Grid.Column="1" Content="{Binding Quantity}" />
      <Text Grid.Column="2" Content="{Binding Total, StringFormat='{0:C}'}">
        <Text.Triggers>
          <DataTrigger Binding="{Binding Total}" CompareType="GreaterThan" Value="1000">
            <Setter Member="FontColor" Value="Chocolate" />
          </DataTrigger>
        </Text.Triggers>
      </Text>
    </Grid>
  </DetailBand>
</Report>
```

An `.rxaml` report supports the same binding system as the object model: `{Binding Path}` against the
element's data context, string formats, value converters, and `DataTrigger` for conditional
formatting. A `DataSource` on `DetailBand` (or any `Repeater`) puts its children in a per-item scope
over the bound collection, with an optional `DetailHeader` and `DetailFooter` that stay in the outer
scope.

## Loading at runtime

Reports can be loaded at runtime, which is what a template stored in a database or edited by a user
needs:

```csharp
using Pysar.Xaml;

var report = ReportXaml.Load("""
    <Report xmlns="https://mriyalab.com/pysar">
      <PageFormat Size="A4" Margin="30" />
      <DetailBand>
        <Text Content="Hello from XAML" />
      </DetailBand>
    </Report>
    """);

report.Build();
```

## Code-behind source generator

For application projects, the source generator uses the standard `x:Class` directive to provide
generated `InitializeComponent()`, strongly typed `x:Name` fields, and compiled object construction.
The generator ships inside this package as an analyzer and is wired up automatically through
`build/Pysar.Xaml.props` — no separate analyzer reference is needed. Every `.rxaml` file in the
project is picked up automatically; set `<EnableDefaultReportItems>false</EnableDefaultReportItems>`
to list them yourself. Resources, styles, and triggers currently use the runtime-loader fallback.

## Compile-time binding validation

Any element accepts the MAUI-style directive `x:DataType="local:Invoice"` to declare the
data-context type of a scope. The hint is design-time only — it is ignored when the report is
loaded — and is inherited by child elements until another element declares its own;
`x:DataType=""` clears it for that subtree. The source generator validates `{Binding ...}` paths —
and `DataTrigger.Binding` — against the hint at build time (`PQX010` error for an unknown member,
`PQX011` warning for a type it cannot resolve). Where the scope cannot be known, nothing is reported
rather than guessed: styles and resource dictionaries are reused across scopes, so their bindings are
never validated. The XAML designer idiom `d:DataContext="{d:DesignInstance Type=local:Invoice}"` is
an accepted alternative spelling of the same hint, used when no `x:DataType` is present on the
element.

## Documentation

- [Repository and full README](https://github.com/MriyaLab/Pysar)
- [Quick start](https://github.com/MriyaLab/Pysar/blob/main/docs/quick-start.md)

## License

MIT — see [LICENSE](https://github.com/MriyaLab/Pysar/blob/main/LICENSE).
