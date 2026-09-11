# Report.Watermark — Design

Date: 2026-09-11
Status: Approved by user (interactive design review)

## Goal

Give a report a page watermark: an overlay `Frame` that paints on every page **under** the bands,
covering the physical page, without taking layout space or changing pagination.

```xml
<Report>
  <Watermark>
    <Text Content="DRAFT"
          HorizontalAlignment="Center"
          VerticalAlignment="Center" />
  </Watermark>
  <PageHeaderBand Height="30"/>
  <DetailBand/>
</Report>
```

## Approved decisions

1. **Dedicated type** `Watermark : Frame`, exposed as `Report.Watermark` — not a `Band`, not a bare `Frame`.
2. **Underlay.** Paint after the page surface, before PageHeader / flow / PageFooter. Page border stays last.
3. **Full page.** Measure and draw against the physical page (including margins), origin (0,0).
4. **Static.** One measure, same `LayoutNode` on every page. No per-page binding pass, no `OnPageChanged` effect.
5. **v1 primitives.** Existing Frame children only (`Text`, `Image`, layout). No `Opacity`, no `Rotation`.
6. **Does not reserve height.** `ContentWindowHeight` and the header+footer oversubscribe check ignore it.

## Out of scope

- Rotation / opacity / blend modes.
- Overlay-on-top paint order.
- Per-page watermark content (`PageNumber`, `OnPageChanged`).
- Repeater expansion inside the watermark (same as page bands: `RepeaterExpander` only walks `Detail`).
- Multiple watermarks.

## Components

### 1. `Watermark` — `src/Pysar/Elements/Watermark/Watermark.cs` (new)

```csharp
public sealed class Watermark : Frame { }
```

Empty subclass. All layout, clipping, background, children, `ZIndex`, `IsVisible`, `CornerRadius`
come from `Frame`. A distinct type is the extension point for later watermark-only properties
without polluting `Frame`.

Default `Size` is already `Fill` on `ReportElement`, so an unconfigured watermark fills the
page and children can center inside it.

### 2. `Report.Watermark` — `src/Pysar/Elements/Report/Report.cs`

```csharp
public Watermark? Watermark { get; set; }
```

Not in `BandCollection`. Setter assigns `ParentElement = this` (clear on `null`). A second
assignment replaces; it does not throw.

`Build()` must visit the watermark in the same passes as bands. Today those walks take `Bands`
only, so each one grows an extra `if (Watermark is { } w)` (or equivalent):

- `StyleEngine.Apply` — `Walk(w, report.Resources)`
- both `BindingEngine.ResolveBindings` calls
- `TriggerEngine.Apply`

Bindings use the report `DataContext`. `PageNumber` / `PageCount` are **not** rewritten onto this
tree per page — a `{Binding PageNumber, Source={x:Reference Root}}` inside a watermark would freeze
at whatever the report held at `Build()`.

### 3. Fluent — `src/Pysar/Elements/ReportBuilder.cs`

```csharp
public ReportBuilder WithWatermark(Action<Watermark> configure)
```

Get-or-create, then configure — same pattern as `WithPageHeader`.

### 4. XAML

**Child of `<Report>`** — the v1 XAML surface, same as bands. Compiled (`x:Class`) and runtime loaders both take this form:

```xml
<Report>
  <Watermark>…</Watermark>
  …
</Report>
```

Two `<Watermark>` children: last wins, same as `Bands.Set`. `<Report.Watermark>` is not a v1 requirement (`EmitReportBody` only walks report children, as with bands).

Loader — `src/Pysar.Xaml/XamlObjectFactory.cs` `BuildReport` switch:

```csharp
case Watermark watermark: report.Watermark = watermark; break;
```

Unknown children still throw. Source gen — `XamlConstructionEmitter.EmitReportBody`:

```csharp
else if (fullyQualifiedName.EndsWith(".Watermark", StringComparison.Ordinal))
    this.Watermark = local;
else
    this.Bands.Set(local);
```

Check `.Watermark` **before** the `Bands.Set` fallback. `EndsWith(".PageFormat")` style matching
already used there.

### 5. Layout — `src/Pysar/Skia/Layout/ReportLayoutEngine.cs`

`ReportLayout` gains `LayoutNode? Watermark = null`. Only `MeasureAsync` constructs the record.

Measure against the physical page (`0,0` → page size), after the template bands, with
`WidthOverride: Fill` and `IgnorePosition: true`. **No** `HeightOverride`: default `Fill` height
becomes the page height. Do **not** subtract anything from `windowH`.

`IsVisible == false` still measures (cheap) or may be skipped at draw; either is fine as long as
pagination is unchanged. Prefer skip-at-draw (`ElementDrawer` already no-ops invisible nodes) so
layout stays simple.

### 6. Paint — `src/Pysar/Skia/Rendering/PageRenderer.cs`

`DrawPage` draws `layout.Watermark` first at page origin `(0,0)`, using the same
`IntersectsVisible` / `ApplyEdgeBleed` / `DrawTranslated` path as PageHeader.
Then header, flow, footer — unchanged.

The node comes from `ReportLayout`, not `PageBandResolver`. `ReportRenderSession` needs no extra
cache: it already holds `_layout`. Bitmap, PDF, and tiled viewer all go through `DrawPage`.

`CollectImageSources` must walk `design.Watermark` (and `layout.Watermark`) so an `Image` inside
the watermark is prefetched.

## Data flow

```
Report.Watermark (Frame tree)
  → Build: styles, bindings, triggers   (once)
  → ReportLayoutEngine.MeasureAsync     (once, full page)
  → PageRenderer.DrawPage               (every page, under bands)
```

Pagination (`BandPaginator`) never sees it.

## Error handling

- `null` watermark: skip measure and draw.
- Empty watermark (no children, transparent background): nothing visible; pagination unchanged.
- Header+footer oversubscribe check: watermark height is not included.
- Overflow: the box is the physical page. No extra clip layer in v1.
- A `Repeater` inside a watermark is not expanded (`RepeaterExpander` walks `Detail` only).

## Testing

`tests/Pysar.Elements.Tests`:

- Set / replace `Report.Watermark`; it is not in `Bands`.
- `WithWatermark` get-or-create.

`tests/Pysar.Xaml.Tests`:

- Child `<Watermark>` populates `Report.Watermark`.
- `x:Name` is captured.
- Source-gen construction assigns `this.Watermark`, not `Bands.Set`.

`tests/Pysar.Skia.Tests`:

- Layout: `ContentWindowHeight` identical with and without a tall watermark; watermark box equals
  the page size when `Size` is `Fill`.
- Render: a watermark fill is visible on empty page area; where a band paints an opaque fill, the
  band pixel wins.
- Multi-page: the same underlay appears on page 1 and page 2.
- `IsVisible=false`: no watermark pixels.

## Files

| File | Action |
|------|--------|
| `src/Pysar/Elements/Watermark/Watermark.cs` | Create |
| `src/Pysar/Elements/Report/Report.cs` | Edit — property; `Build()` visits watermark |
| `src/Pysar/Elements/ReportBuilder.cs` | Edit — `WithWatermark` |
| `src/Pysar/Elements/Resources/StyleEngine.cs` | Edit — walk `report.Watermark` |
| `src/Pysar.Xaml/XamlObjectFactory.cs` | Edit — `<Watermark>` child |
| `src/Pysar.Xaml.SourceGen/XamlConstructionEmitter.cs` | Edit — assign `this.Watermark` |
| `src/Pysar/Skia/Layout/ReportLayoutEngine.cs` | Edit — measure, `ReportLayout.Watermark` |
| `src/Pysar/Skia/Rendering/PageRenderer.cs` | Edit — underlay draw + image collect |
| `tests/Pysar.Elements.Tests`, `Pysar.Xaml.Tests`, `Pysar.Xaml.SourceGen.Tests`, `Pysar.Skia.Tests` | Add |
| `docs/quick-start.md` | Edit — short watermark section |

Sibling repository `../Pysar.Plugins`: regenerate `Shared/Schemas/pysar.xsd` so `<Watermark>` and
`Report.Watermark` are valid in the IDE snapshot.

## Tooling (`../Pysar.Plugins`)

`Pysar.SchemaGen` reflects the element model. A new `Watermark` type and `Report.Watermark`
property require:

```bash
dotnet run --project Shared/Pysar.SchemaGen/Pysar.SchemaGen.csproj -- Shared/Schemas/pysar.xsd
```

Designers already render through `PreviewHost` → `Pysar.Skia`, so underlay output appears with no
plugin-side draw change.
