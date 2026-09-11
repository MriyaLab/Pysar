# Report.Watermark Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Report.Watermark` — a `Frame` subclass painted under the bands on every page, inside the content zone, without changing pagination.

**Architecture:** `Watermark : Frame` lives on `Report` as a single property, not in `BandCollection`. `Build()` walks it for styles/bindings/triggers. `ReportLayoutEngine` measures it once against the content zone. `PageRenderer.DrawPage` paints that node first, then header / flow / footer.

**Tech Stack:** C# / .NET 10, SkiaSharp, xUnit

**Spec:** [2026-09-11-report-watermark-design.md](../specs/2026-09-11-report-watermark-design.md)

---

## File Map

| File | Action | Purpose |
|------|--------|---------|
| `src/Pysar/Elements/Watermark/Watermark.cs` | Create | `Watermark : Frame` |
| `src/Pysar/Elements/Report/Report.cs` | Edit | Property; `Build()` visits it |
| `src/Pysar/Elements/ReportBuilder.cs` | Edit | `WithWatermark` |
| `src/Pysar/Elements/Resources/StyleEngine.cs` | Edit | Walk `report.Watermark` |
| `src/Pysar.Xaml/XamlObjectFactory.cs` | Edit | `<Watermark>` child of `<Report>` |
| `src/Pysar.Xaml.SourceGen/XamlConstructionEmitter.cs` | Edit | `this.Watermark = local` |
| `src/Pysar/Skia/Layout/ReportLayoutEngine.cs` | Edit | Measure; `ReportLayout.Watermark` |
| `src/Pysar/Skia/Rendering/PageRenderer.cs` | Edit | Underlay draw + image collect |
| `tests/Pysar.Elements.Tests/WatermarkTests.cs` | Create | Property, builder, build pipeline |
| `tests/Pysar.Xaml.Tests/ReportRootTests.cs` | Edit | XAML child + `x:Name` |
| `tests/Pysar.Xaml.Tests/TypeResolutionTests.cs` | Edit | Resolve `Watermark` |
| `tests/Pysar.Xaml.SourceGen.Tests/GeneratorConstructionTests.cs` | Edit | Compiled construction |
| `tests/Pysar.Skia.Tests/Layout/ReportLayoutEngineTests.cs` | Edit | Zone box, window unchanged |
| `tests/Pysar.Skia.Tests/Rendering/WatermarkRenderTests.cs` | Create | Underlay pixels, multi-page, hidden |
| `docs/quick-start.md` | Edit | Short watermark section |
| `../Pysar.Plugins/Shared/Schemas/pysar.xsd` | Regenerate | IDE schema (sibling repo) |

---

## Task 1: `Watermark` type and `Report.Watermark`

**Files:**
- Create: `tests/Pysar.Elements.Tests/WatermarkTests.cs`
- Create: `src/Pysar/Elements/Watermark/Watermark.cs`
- Modify: `src/Pysar/Elements/Report/Report.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class WatermarkTests
{
    [Fact]
    public void Watermark_IsFrame_SupportsChildren()
    {
        var watermark = new Watermark();
        watermark.AddElement(new Text { Content = "DRAFT" });
        Assert.Single(watermark.Children);
        Assert.IsAssignableFrom<Frame>(watermark);
    }

    [Fact]
    public void Report_Watermark_DefaultIsNull()
    {
        Assert.Null(new Report().Watermark);
    }

    [Fact]
    public void Report_Watermark_IsNotInBands()
    {
        var report = new Report();
        var watermark = new Watermark();
        report.Watermark = watermark;
        Assert.Same(watermark, report.Watermark);
        Assert.DoesNotContain(watermark, report.Bands);
    }

    [Fact]
    public void Report_Watermark_SetsParent()
    {
        var report = new Report();
        var watermark = new Watermark();
        report.Watermark = watermark;
        Assert.Same(report, watermark.ParentElement);
    }

    [Fact]
    public void Report_Watermark_Replace_DoesNotThrow()
    {
        var report = new Report();
        report.Watermark = new Watermark();
        var second = new Watermark();
        report.Watermark = second;
        Assert.Same(second, report.Watermark);
        Assert.Same(report, second.ParentElement);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pysar.Elements.Tests --filter WatermarkTests`

Expected: FAIL — `Watermark` type does not exist.

- [ ] **Step 3: Minimal implementation**

`src/Pysar/Elements/Watermark/Watermark.cs`:

```csharp
namespace Pysar.Elements;

public sealed class Watermark : Frame { }
```

In `src/Pysar/Elements/Report/Report.cs`, add a field and property next to the band accessors:

```csharp
private Watermark? _watermark;

public Watermark? Watermark
{
    get => _watermark;
    set
    {
        _watermark = value;
        if (_watermark is not null)
            _watermark.ParentElement = this;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Pysar.Elements.Tests --filter WatermarkTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Pysar/Elements/Watermark/Watermark.cs src/Pysar/Elements/Report/Report.cs tests/Pysar.Elements.Tests/WatermarkTests.cs
git commit -m "feat: add Report.Watermark as a Frame subclass"
```

---

## Task 2: `WithWatermark`

**Files:**
- Modify: `tests/Pysar.Elements.Tests/WatermarkTests.cs`
- Modify: `src/Pysar/Elements/ReportBuilder.cs`

- [ ] **Step 1: Write the failing test** (append to `WatermarkTests`)

```csharp
[Fact]
public void Builder_WithWatermark_ConfiguresWatermark()
{
    var design = ReportBuilder.Create("t")
        .WithWatermark(w => w.AddElement(new Text { Content = "DRAFT" }))
        .Build();

    Assert.NotNull(design.Watermark);
    Assert.Single(design.Watermark!.Children);
}

[Fact]
public void Builder_WithWatermark_SecondCallMutatesSameInstance()
{
    var design = ReportBuilder.Create("t")
        .WithWatermark(w => w.BackgroundColor = Colors.Red)
        .WithWatermark(w => w.AddElement(new Text { Content = "DRAFT" }))
        .Build();

    Assert.Equal(Colors.Red, design.Watermark!.BackgroundColor);
    Assert.Single(design.Watermark.Children);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Pysar.Elements.Tests --filter Builder_WithWatermark`

Expected: FAIL — `WithWatermark` is not defined.

- [ ] **Step 3: Implement**

In `src/Pysar/Elements/ReportBuilder.cs`, next to the `WithPageFooter` line:

```csharp
public ReportBuilder WithWatermark(Action<Watermark> configure)
{
    ArgumentNullException.ThrowIfNull(configure);
    _reportDesign.Watermark ??= new Watermark();
    configure(_reportDesign.Watermark);
    return this;
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Pysar.Elements.Tests --filter WatermarkTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Pysar/Elements/ReportBuilder.cs tests/Pysar.Elements.Tests/WatermarkTests.cs
git commit -m "feat: add ReportBuilder.WithWatermark"
```

---

## Task 3: Build pipeline visits the watermark

**Files:**
- Modify: `tests/Pysar.Elements.Tests/WatermarkTests.cs`
- Modify: `src/Pysar/Elements/Report/Report.cs`
- Modify: `src/Pysar/Elements/Resources/StyleEngine.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Build_ResolvesWatermarkBindings()
{
    var text = new Text();
    text.SetBinding(Text.ContentProperty, "Title");

    var report = new Report { DataContext = new WatermarkVm("DRAFT") };
    report.Watermark = new Watermark();
    report.Watermark.AddElement(text);
    report.Build();

    Assert.Equal("DRAFT", text.Content);
}

[Fact]
public void Build_AppliesImplicitStyle_ToWatermarkChild()
{
    var report = new Report();
    report.Resources[typeof(Text)] = new Style
    {
        TargetType = typeof(Text),
        Setters = { new Setter { Member = nameof(Text.FontSize), Value = "14" } }
    };

    var text = new Text();
    report.Watermark = new Watermark();
    report.Watermark.AddElement(text);
    report.Build();

    Assert.Equal(14f, text.FontSize);
}

private sealed record WatermarkVm(string Title);
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pysar.Elements.Tests --filter "Build_ResolvesWatermarkBindings|Build_AppliesImplicitStyle_ToWatermarkChild"`

Expected: FAIL — `Content` still empty / `FontSize` still the type default, because `Build()` and `StyleEngine` only walk `Bands`.

- [ ] **Step 3: Implement**

In `Report.Build()`, replace the three `Bands`-only walks:

```csharp
StyleEngine.Apply(this);

var engine = new Pysar.Binding.BindingEngine();
engine.ResolveBindings(PipelineRoots(), DataContext);
RepeaterExpander.Expand(this);
engine.ResolveBindings(PipelineRoots(), DataContext);
TriggerEngine.Apply(PipelineRoots(), DataContext);
return this;
```

Add a private iterator on `Report` (below `Build()`):

```csharp
private IEnumerable<IReportElement> PipelineRoots()
{
    foreach (var band in Bands)
        yield return band;
    if (Watermark is { } watermark)
        yield return watermark;
}
```

`Report.cs` already imports `Pysar.Core.Abstractions` (`IReportElement`).

In `src/Pysar/Elements/Resources/StyleEngine.cs` `Apply`:

```csharp
foreach (var band in report.Bands)
    Walk(band, report.Resources);
if (report.Watermark is { } watermark)
    Walk(watermark, report.Resources);
```

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Pysar.Elements.Tests --filter WatermarkTests`

Expected: PASS. Also run `dotnet test tests/Pysar.Elements.Tests --filter StyleEngineTests` so the extra walk did not break existing style tests.

- [ ] **Step 5: Commit**

```bash
git add src/Pysar/Elements/Report/Report.cs src/Pysar/Elements/Resources/StyleEngine.cs tests/Pysar.Elements.Tests/WatermarkTests.cs
git commit -m "feat: include Watermark in report Build pipeline"
```

---

## Task 4: XAML loader

**Files:**
- Modify: `tests/Pysar.Xaml.Tests/ReportRootTests.cs`
- Modify: `tests/Pysar.Xaml.Tests/TypeResolutionTests.cs`
- Modify: `src/Pysar.Xaml/XamlObjectFactory.cs`

- [ ] **Step 1: Write the failing tests**

In `ReportRootTests.cs`:

```csharp
[Fact]
public void Report_WatermarkChild_SetsWatermark()
{
    var design = ReportXaml.Load(
        $"<Report {Root}><Watermark><Text Content=\"DRAFT\"/></Watermark><DetailBand/></Report>");
    Assert.NotNull(design.Watermark);
    Assert.IsType<Text>(Assert.Single(design.Watermark!.Children));
    Assert.DoesNotContain(design.Watermark, design.Bands);
}

[Fact]
public void Report_WatermarkXName_Captured()
{
    var result = new XamlLoaderTestAccess().LoadWithNames(
        $"<Report {Root}><Watermark x:Name=\"stamp\"/></Report>");
    Assert.True(result.Names.ContainsKey("stamp"));
    Assert.IsType<Watermark>(result.Names["stamp"]);
}

[Fact]
public void Report_TwoWatermarkChildren_LastWins()
{
    var design = ReportXaml.Load(
        $"<Report {Root}><Watermark x:Name=\"a\"/><Watermark x:Name=\"b\"/></Report>");
    Assert.NotNull(design.Watermark);
    Assert.Equal("b", design.Watermark!.Name);
}
```

In `TypeResolutionTests.Resolve_DefaultNamespace_FindsElementType`, add:

```csharp
Assert.Equal(typeof(Watermark), resolver.Resolve(Ns, "Watermark"));
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pysar.Xaml.Tests --filter "Report_Watermark|FindsElementType"`

Expected: FAIL — `<Report> cannot contain <Watermark>.` (`XamlObjectFactory` switch has no `Watermark` case). Type resolution may already succeed once the type exists in `Pysar.Elements`; the child-of-Report tests are the ones that must fail.

- [ ] **Step 3: Implement**

In `src/Pysar.Xaml/XamlObjectFactory.cs` `BuildReport`, inside the `switch (child)`:

```csharp
case PageFormat pageFormat: report.PageFormat = pageFormat; break;
case Metadata metadata: report.Metadata = metadata; break;
case Watermark watermark: report.Watermark = watermark; break;
case Band band: report.Bands.Set(band); break;
default:
    throw new XamlException(
        $"<Report> cannot contain <{childNode.Type.LocalName}>.");
```

`Watermark` is not a `Band`, so it must be matched **before** `case Band` is irrelevant — keep it as its own arm anyway so a later `Watermark : Band` mistake would not silently swallow it.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Pysar.Xaml.Tests --filter "ReportRootTests|TypeResolutionTests"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Pysar.Xaml/XamlObjectFactory.cs tests/Pysar.Xaml.Tests/ReportRootTests.cs tests/Pysar.Xaml.Tests/TypeResolutionTests.cs
git commit -m "feat: load Watermark as a Report XAML child"
```

---

## Task 5: Compiled XAML (source gen)

**Files:**
- Modify: `tests/Pysar.Xaml.SourceGen.Tests/GeneratorConstructionTests.cs`
- Modify: `src/Pysar.Xaml.SourceGen/XamlConstructionEmitter.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Report_WatermarkChild_AssignsWatermarkNotBands()
{
    var src = Gen($"<Report x:Class=\"MyApp.R\" {Head}>" +
                  "<Watermark x:Name=\"Stamp\">" +
                  "<Text Content=\"DRAFT\"/>" +
                  "</Watermark>" +
                  "<DetailBand/></Report>");

    Assert.Contains("new global::Pysar.Elements.Watermark()", src);
    Assert.Contains("this.Watermark = ", src);
    Assert.Contains("this.Stamp = ", src);

    foreach (var line in src.Split('\n'))
    {
        if (line.Contains("Bands.Set", StringComparison.Ordinal)
            && line.Contains("Watermark", StringComparison.Ordinal))
            Assert.Fail($"Watermark must not go through Bands.Set: {line.Trim()}");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Pysar.Xaml.SourceGen.Tests --filter Report_WatermarkChild_AssignsWatermarkNotBands`

Expected: FAIL — generated source has `this.Bands.Set(localN)` for the watermark (current fallback in `EmitReportBody`).

- [ ] **Step 3: Implement**

In `src/Pysar.Xaml.SourceGen/XamlConstructionEmitter.cs` `EmitReportBody`:

```csharp
foreach (var child in report.Children)
{
    var (local, fullyQualifiedName) = EmitElement(child);
    if (fullyQualifiedName.EndsWith(".PageFormat", StringComparison.Ordinal))
        _builder.AppendLine($"            this.PageFormat = {local};");
    else if (fullyQualifiedName.EndsWith(".Metadata", StringComparison.Ordinal))
        _builder.AppendLine($"            this.Metadata = {local};");
    else if (fullyQualifiedName.EndsWith(".Watermark", StringComparison.Ordinal))
        _builder.AppendLine($"            this.Watermark = {local};");
    else
        _builder.AppendLine($"            this.Bands.Set({local});");
}
```

The `.Watermark` check must come **before** the `Bands.Set` fallback. `EndsWith(".Watermark")` matches `global::Pysar.Elements.Watermark`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Pysar.Xaml.SourceGen.Tests --filter GeneratorConstructionTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Pysar.Xaml.SourceGen/XamlConstructionEmitter.cs tests/Pysar.Xaml.SourceGen.Tests/GeneratorConstructionTests.cs
git commit -m "feat: emit Report.Watermark in compiled XAML"
```

---

## Task 6: Layout — measure in the content zone, do not reserve height

**Files:**
- Modify: `tests/Pysar.Skia.Tests/Layout/ReportLayoutEngineTests.cs`
- Modify: `src/Pysar/Skia/Layout/ReportLayoutEngine.cs`

- [ ] **Step 1: Write the failing tests** (append to `ReportLayoutEngineTests`)

```csharp
[Fact]
public async Task Measure_Watermark_DoesNotReduceContentWindow()
{
    var without = ReportBuilder.Create("t")
        .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
        .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(40)))
        .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(30)))
        .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
        .Build();

    var with = ReportBuilder.Create("t")
        .WithPageFormat(new PageFormat { Margin = new Thickness(30), Size = PageSize.A4 })
        .WithPageHeader(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(40)))
        .WithPageFooter(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(30)))
        .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
        .WithWatermark(w => w.WithBackgroundColor(Colors.Red))
        .Build();

    var layoutWithout = await ReportLayoutEngine.MeasureAsync(without, new MeasureContext(1f), CancellationToken.None);
    var layoutWith = await ReportLayoutEngine.MeasureAsync(with, new MeasureContext(1f), CancellationToken.None);

    Assert.Equal(layoutWithout.ContentWindowHeight, layoutWith.ContentWindowHeight);
    Assert.NotNull(layoutWith.Watermark);
    Assert.Equal(layoutWith.ContentZone.Width, layoutWith.Watermark!.Bounds.Width);
    Assert.Equal(layoutWith.ContentZone.Height, layoutWith.Watermark.Bounds.Height);
}

[Fact]
public async Task Measure_NoWatermark_NodeIsNull()
{
    var design = ReportBuilder.Create("t")
        .WithDetail(b => b.WithSize(SizeLength.Fill, SizeLength.Fixed(100)))
        .Build();

    var layout = await ReportLayoutEngine.MeasureAsync(design, new MeasureContext(1f), CancellationToken.None);
    Assert.Null(layout.Watermark);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Pysar.Skia.Tests --filter Measure_Watermark`

Expected: FAIL — `ReportLayout` has no `Watermark` member.

- [ ] **Step 3: Implement**

In `src/Pysar/Skia/Layout/ReportLayoutEngine.cs`, extend the record (keep existing optional args, add `Watermark` last):

```csharp
public sealed record ReportLayout(
    LayoutNode? PageHeader, float PageHeaderHeight,
    LayoutNode? PageFooter, float PageFooterHeight,
    IReadOnlyList<LayoutNode> Flow,
    float FlowHeight,
    float ContentWindowHeight,
    Rect ContentZone,
    LayoutNode? RepeatDetailHeader = null,
    float RepeatDetailHeaderHeight = 0f,
    LayoutNode? Watermark = null);
```

In `MeasureAsync`, after the content `zone` is computed and **without** touching `windowH`, measure the watermark with the same constraint as PageHeader:

```csharp
LayoutNode? watermark = null;
if (design.Watermark is not null)
{
    watermark = await LayoutEngine.MeasureAsync(design.Watermark,
        new MeasureConstraint(new Rect(0, 0, zone.Width, zone.Height),
            WidthOverride: SizeLength.Fill, IgnorePosition: true), ctx, ct);
}
```

Pass it into the `return new ReportLayout(...)` call as the last argument. Do **not** add a `HeightOverride`: default `Size.Fill` already stretches to `zone.Height`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Pysar.Skia.Tests --filter ReportLayoutEngineTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Pysar/Skia/Layout/ReportLayoutEngine.cs tests/Pysar.Skia.Tests/Layout/ReportLayoutEngineTests.cs
git commit -m "feat: measure Watermark in the content zone without reserving height"
```

---

## Task 7: Paint underlay

**Files:**
- Create: `tests/Pysar.Skia.Tests/Rendering/WatermarkRenderTests.cs`
- Modify: `src/Pysar/Skia/Rendering/PageRenderer.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Skia;
using SkiaSharp;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class WatermarkRenderTests
{
    [Fact]
    public async Task Render_Watermark_PaintsUnderContent_InsideContentZone()
    {
        var design = ReportBuilder.Create("t")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithWatermark(w => w.WithBackgroundColor(Colors.Red))
            .WithDetail(d => d.AddElement(new Frame
            {
                Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)),
                BackgroundColor = Colors.Blue
            }))
            .Build();

        var page = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).First();

        // Margin gutter stays paper white — watermark is content-zone only.
        Assert.Equal(SKColors.White, page.GetPixel(5, 5));
        // Detail child wins where it overlaps the watermark.
        Assert.Equal(SKColors.Blue, page.GetPixel(20, 20));
        // Empty content-zone area shows the watermark.
        Assert.Equal(SKColors.Red, page.GetPixel(300, 400));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Pysar.Skia.Tests --filter Render_Watermark_PaintsUnderContent_InsideContentZone`

Expected: FAIL — pixel (300, 400) is white (page surface), not red. Layout now produces a watermark node but `DrawPage` never paints it.

- [ ] **Step 3: Implement**

In `src/Pysar/Skia/Rendering/PageRenderer.cs` `DrawPage`, **before** the PageHeader block:

```csharp
if (layout.Watermark is { } watermark)
{
    var watermarkOx = contentLeft;
    var watermarkOy = layout.ContentZone.Top;
    if (IntersectsVisible(watermark.Bounds, watermarkOx, watermarkOy, paddedVisible))
    {
        ctx.CullBoundsPt = ToLocalCull(paddedVisible, watermarkOx, watermarkOy);
        DrawTranslated(ApplyEdgeBleed(watermark, contentLeft, pageWidth),
            ctx, watermarkOx, watermarkOy, scale, drawers);
    }
}
```

In `CollectImageSources`, after the `foreach (var band in design.Bands)` loop:

```csharp
if (design.Watermark is not null)
    CollectImagesFromElement(design.Watermark, list);
```

and after the page-header node collect:

```csharp
if (layout.Watermark is not null)
    CollectImagesFromNode(layout.Watermark, list);
```

`ReportRenderSession` needs no change: it already holds `_layout` and calls `DrawPage`.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Pysar.Skia.Tests --filter WatermarkRenderTests`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/Pysar/Skia/Rendering/PageRenderer.cs tests/Pysar.Skia.Tests/Rendering/WatermarkRenderTests.cs
git commit -m "feat: paint Watermark under page bands"
```

---

## Task 8: Multi-page, hidden, opaque band wins

**Files:**
- Modify: `tests/Pysar.Skia.Tests/Rendering/WatermarkRenderTests.cs`

No production code if Task 7 already draws from `layout.Watermark` on every `DrawPage` call and `ElementDrawer` already skips `IsVisible == false`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Render_Watermark_RepeatsOnEveryPage()
{
    var design = ReportBuilder.Create("t")
        .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
        .WithWatermark(w => w.WithBackgroundColor(Colors.Red))
        .WithDetail(d => d.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fixed(2000))
        }))
        .Build();

    var pages = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).ToList();
    Assert.True(pages.Count >= 2);
    Assert.Equal(SKColors.Red, pages[0].GetPixel(300, 400));
    Assert.Equal(SKColors.Red, pages[1].GetPixel(300, 400));
}

[Fact]
public async Task Render_Watermark_Hidden_DoesNotPaint()
{
    var design = ReportBuilder.Create("t")
        .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
        .WithWatermark(w =>
        {
            w.WithBackgroundColor(Colors.Red);
            w.IsVisible = false;
        })
        .WithDetail(d => d.AddElement(new Frame
        {
            Size = new Size(SizeLength.Fixed(50), SizeLength.Fixed(50)),
            BackgroundColor = Colors.Blue
        }))
        .Build();

    var page = (await new SkiaReportRenderer().RenderPageAsync(design, scale: 1f)).First();
    Assert.Equal(SKColors.White, page.GetPixel(300, 400));
    Assert.Equal(SKColors.Blue, page.GetPixel(20, 20));
}
```

The 2000pt detail `Frame` is transparent by default, so the red watermark shows through on every page.

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Pysar.Skia.Tests --filter "Render_Watermark_RepeatsOnEveryPage|Render_Watermark_Hidden_DoesNotPaint"`

Expected: PASS if Task 7 is correct (`ElementDrawer` already returns on `!IsVisible`). If `RepeatsOnEveryPage` fails, `DrawPage` is not using `layout.Watermark` on later pages — fix that before continuing.

- [ ] **Step 3: Commit**

```bash
git add tests/Pysar.Skia.Tests/Rendering/WatermarkRenderTests.cs
git commit -m "test: watermark repeats on every page and honors IsVisible"
```

---

## Task 9: Docs

**Files:**
- Modify: `docs/quick-start.md`

- [ ] **Step 1: Add a short section after the page-numbers section** (before `## Report lifetime`)

Insert this heading and body:

- Heading: `## Watermark`
- Body: `Watermark` is a `Frame` painted on every page under the bands, inside the content zone. It does not reserve height and is not a band — pagination is unchanged.
- Example:

        <Report>
          <Watermark>
            <Text Content="DRAFT"
                  HorizontalAlignment="Center"
                  VerticalAlignment="Center"
                  FontColor="#33000000" />
          </Watermark>
          <PageHeaderBand Height="30"/>
          <DetailBand/>
        </Report>

- Closing note: It is measured once. `PageNumber` / `OnPageChanged` do not apply to it. There is no rotation or opacity primitive in this version: use a faded `FontColor` or `BackgroundColor`. Fluent: `ReportBuilder.Create("t").WithWatermark(w => w.AddElement(...))`.

- [ ] **Step 2: Commit**

```bash
git add docs/quick-start.md
git commit -m "docs: document Report.Watermark"
```

---

## Task 10: IDE schema (sibling repo)

**Files:**
- `../Pysar.Plugins/Shared/Schemas/pysar.xsd` (regenerate)

- [ ] **Step 1: Confirm the sibling repo exists** at `../Pysar.Plugins`. If it is missing, stop and note it in the PR — do not invent a schema by hand.

- [ ] **Step 2: Regenerate**

```bash
dotnet run --project Shared/Pysar.SchemaGen/Pysar.SchemaGen.csproj -- Shared/Schemas/pysar.xsd
```

from the `Pysar.Plugins` root. Expected diff: a `Watermark` element (same attributes as `Frame`) and a `Watermark` property on `Report`.

- [ ] **Step 3: Commit in that repository** with message `feat: add Watermark to Pysar XAML schema`.

---

## Verify the whole change

From the Pysar repo root:

```bash
dotnet test tests/Pysar.Elements.Tests tests/Pysar.Xaml.Tests tests/Pysar.Xaml.SourceGen.Tests tests/Pysar.Skia.Tests
```

Expected: all PASS.

---

## Spec coverage

| Spec item | Task |
|-----------|------|
| `Watermark : Frame` | 1 |
| `Report.Watermark` property, not in `Bands`, replace does not throw | 1 |
| `WithWatermark` | 2 |
| `Build()` styles / bindings / triggers | 3 |
| XAML child, last wins, `x:Name` | 4 |
| Source gen `this.Watermark` | 5 |
| Measure in content zone, no window shrink | 6 |
| Underlay paint, content-zone only | 7 |
| Same node every page, `IsVisible` | 8 |
| `quick-start.md` | 9 |
| Plugin schema | 10 |
| No Opacity / Rotation | none (out of scope) |
| No per-page `PageNumber` | none (out of scope; Task 3 bindings are report `DataContext` only) |
| Image prefetch | 7 (`CollectImageSources`) |
