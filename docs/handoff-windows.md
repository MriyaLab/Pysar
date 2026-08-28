# Handoff: work that needs a Windows machine

**Written:** 2026-08-15
**Worked through on Windows:** 2026-08-15 — §0 and §1 are done; §2's Windows hazard and §3's step 1
are measured. See the status note at the head of each section.
**Branch:** `master`, at `d066086`
**Why this file exists:** `Pysar.Wpf` compiles only on Windows. Off-Windows its csproj sets
`EnableDefaultCompileItems=false` and compiles `NonWindowsStub.cs` alone, so every WPF source file in
the repository is currently **unverified** — a typo in one would not fail any build that has been run
so far.

---

## 0. First thing to do: prove the WPF package still builds

> **Done.** Both projects build clean on `net10.0-windows` (2 pre-existing SkiaSharp `CS0618`
> warnings, nothing else), so all five blind changes compile. The whole test suite was also run on
> Windows: **607 passing** across the 11 test projects, after one test fix — see below. The sample
> was driven and behaves: Ctrl+wheel zoom steps 84% → 427% → 107% with sharp tiles at each level,
> double-click magnifies to 200% and back out, resize keeps rendering, keyboard scroll advances
> pages, and closing the window exits the process well within 15s, so `DisposeWhenStillDetached`
> does not hang teardown.
>
> **One test was failing on Windows and is now fixed:**
> `GeneratorParityTests.RuntimeFallback_EmitsSourceBaseDirectory` hard-coded the POSIX path
> `/tmp/qreport`, which `Path.GetFullPath` re-roots onto the current drive on Windows, so the
> directory the generator emitted never matched. The generator is correct — a real build always
> hands it an absolute path. The test now builds its path from `Path.GetTempPath()` and compares
> against the escaped form the generated C# literal actually carries.

Several commits changed WPF sources without ever compiling them. Before writing anything new:

```bash
dotnet build src/Pysar.Wpf/Pysar.Wpf.csproj
dotnet build samples/Pysar.Wpf.Sample/Pysar.Wpf.Sample.csproj
```

Expected: `net10.0-windows` in the output path, not bare `net10.0`. If it says `net10.0`, the
conditional TFM did not take and the real sources are still not being compiled.

**What was changed blind, in commit order:**

| Commit | File | Change |
|---|---|---|
| `b0453e8` | `ReportViewRenderer.cs` | `Instance` became nullable-backed and now throws when `UseQReport` was never called |
| `b0453e8` | `QReportWpf.cs` | dropped a `?? throw` that had become unreachable |
| `b0453e8` | `WpfReportPrinter.cs` | `using Pysar.Export;` added (`IReportPrinter` moved namespace) |
| `b0453e8` | `samples/.../ReportViewerViewModel.cs` | same `using` |
| `1168d90` | `ReportView.cs` | `Unloaded += (_, _) => _reportSession.DisposeWhenStillDetached(() => IsLoaded);` in the constructor |

All four are one-or-two-line changes mirroring Avalonia/MAUI equivalents that **do** compile, so the
risk is low — but it is not zero, and nothing has checked it.

**Also run the sample by hand once**, since there are no UI tests on any host: open a report, scroll,
zoom with Ctrl+wheel, double-click to magnify and back, resize the window, then close the window and
confirm the process exits without hanging. The last one exercises `DisposeWhenStillDetached`.

---

## 1. Migrate `Pysar.Wpf` to `ReportViewController`

> **Done**, exactly as specified in steps 1-8 below, with two departures worth knowing about:
>
> - Step 5's `RequestTiles()` forwarder was **deleted rather than kept**. Once the settle timer moved
>   into the controller nothing called it: WPF's only two callers were the timer and
>   `AfterPresenterUpdate`, both now the controller's. Avalonia's equivalent forwarder was dead for
>   the same reason since `aac4e6e`, so it was removed there too — that is the whole of the Avalonia
>   change in this pass.
> - `SuppressesViewportReaction`'s doc comment is the comment that used to sit inside `OnScrolled`,
>   reworded from "no gesture guard here" to "always false", since it now documents a property
>   rather than an absence.
>
> Verified by build and by driving the sample (see §0). The migrated build's startup state is
> pixel-identical to a build of `d066086` with the migration stashed: page 1 of 2, 130%, FitWidth.

Avalonia and MAUI were migrated and both verified (Avalonia additionally verified by hand: scroll,
zoom, pinch, resize all work). WPF is the last host still carrying its own copy of the orchestration.

**Read first:** `src/Pysar.Viewer/ReportViewController.cs` and `IReportViewSurface.cs`.
**Copy from:** the Avalonia migration, `git show aac4e6e -- src/Pysar.Avalonia/ReportView.cs`.
That diff is the template; WPF is a simpler case than Avalonia.

### What WPF has to do differently from Avalonia

| Member | Avalonia | WPF |
|---|---|---|
| `SuppressesViewportReaction` | `_pinch.Running` | **`false`** — this control has no pinch. `OnScrolled`'s existing comment explains why, keep it |
| `InvalidateSurface()` | `_canvas.InvalidateVisual()` | same (`_canvas.InvalidateVisual()`) |
| `TilesRequested` subscription | captures pinch-commit perf samples | **not needed** — WPF has no DEBUG instrumentation in `RequestTiles` |
| `TilePolicy` | `(VerticalOverdraw, RenderBudget)` | identical |

### Steps

1. `public partial class ReportView : UserControl, IReportViewHost` → add `, IReportViewSurface`.
2. Replace the `private readonly TileSettleTimer _settleTimer;` field with
   `private readonly ReportViewController _controller;` (keep a doc comment like Avalonia's).
3. In the constructor: delete `_presenter.StateChanged += OnPresenterStateChanged;`, and replace the
   two `_settleTimer` lines with
   ```csharp
   _controller = new ReportViewController(_presenter, _reportSession, this);
   _controller.Failed += exception => RenderFailed?.Invoke(this, exception);
   ```
4. `private void OnPresenterStateChanged()` → `void IReportViewSurface.ReportState(int currentPage, double effectiveZoom)`,
   using the parameters instead of reading `_presenter`. Keep the `_reportingCurrentPage` guard exactly as it is.
5. Collapse to one-liners: `OnScrolled() => _controller.Scrolled();`,
   `OnViewportChanged() => _controller.ViewportChanged();`,
   `AfterPresenterUpdate(bool immediate) => _controller.AfterPresenterUpdate(immediate);`,
   `RequestTiles() => _controller.RequestTiles();`
6. Add the remaining explicit implementations:
   ```csharp
   bool IReportViewSurface.SuppressesViewportReaction => false;

   (double VerticalOverdraw, double RenderBudget) IReportViewSurface.TilePolicy
       => (VerticalOverdraw, RenderBudget);

   void IReportViewSurface.InvalidateSurface() => _canvas.InvalidateVisual();

   void IReportViewSurface.RefreshVisuals() => RefreshVisuals();

   void IReportViewSurface.ClearVisuals() => ClearVisuals();
   ```
   (`RefreshVisuals`/`ClearVisuals` stay private and are forwarded — they are called from this
   control's own code far more often than through the interface.)
7. Fix the stale cross-reference in `OnCurrentPageChanged`'s comment: `OnPresenterStateChanged`
   → `IReportViewSurface.ReportState`.
8. Build both WPF projects, then run the sample by hand as in §0.

**Do not expect the file to shrink much.** Avalonia went 741 → 725 lines. The win is that the
orchestration is shared and under test (`ReportViewControllerTests`, 8 tests), not that the host
files get smaller — most of each host is bindable-property declarations and the `IReportViewHost`
implementation, which are genuinely platform-bound.

---

## 2. Package metadata — blocked on decisions, not on Windows

> **Windows hazard confirmed; the rest is still blocked on you.**
> `dotnet pack src/Pysar.Wpf/Pysar.Wpf.csproj -c Release` on Windows produces
> `Pysar.Wpf.1.0.0.nupkg`, so the package does exist here and is missing only off-Windows —
> whatever CI packs releases must run that step on Windows. The `1.0.0` in the file name is the SDK
> default, which is exactly the versioning gap described below.
>
> Still needed before anything else here can be done: version scheme, licence expression, authors,
> repository URL.

Not Windows-specific, but it does need a `dotnet pack` from Windows to verify the WPF package
actually appears (see below).

**Current state:** only 6 of 13 projects under `src/` carry `PackageId`/`Description`/`PackageTags`.
The other 7 — `Core`, `Binding`, `Elements`, `Skia`, `Xaml`, `Xaml.Model`, `Xaml.SourceGen` — will
still pack under SDK-default identity with no description, tags, licence, authors or repository URL.
No project or props file sets `<Version>`, so everything ships as `1.0.0`. There is no
`Directory.Build.props` anywhere in the repo.

`Elements` and `Skia` are the packages consumers take a hard dependency on (`Report`, `Text`, `Grid`,
`SkiaReportRenderer` all live there), so they are public by accident rather than by decision.

**Needed before this can be done:** version scheme, licence expression, authors, repository URL.

**Windows-specific hazard:** `Pysar.Wpf.csproj` sets `IsPackable=false` in its non-Windows
`PropertyGroup`, so `dotnet pack` from macOS or Linux silently produces **no WPF package at all**.
Whatever CI packs releases must run that step on Windows. Worth confirming with a real
`dotnet pack` there.

---

## 3. Open question: can the XAML compatibility shims move out of `Core`?

`Pysar.Core` declares seven types in Microsoft's own namespaces — `System.Windows.ResourceDictionary`,
`System.Windows.StaticResourceExtension`, and five `System.Windows.Markup.*` attributes. This is
**deliberate and confirmed**: the IDE XAML editors recognise them by full name, and moving them would
silently lose XAML IntelliSense. It is now documented in `<remarks>` on each of those files, along
with the fact that QReport's own loader and source generator read the *other* set (the equivalents in
`Pysar.Elements`), which is why every affected type carries both attributes.

**The unresolved cost:** `Core` is packable, so an application referencing it with `UseWPF` set gets
CS0433 in its own code the moment it names one of those types unqualified under `using System.Windows;`.
Nothing in this repository does, which is the only reason it has never surfaced. A consumer cannot
work around it without QReport making a breaking change to `Core`.

**Worth trying on Windows**, where a WPF project can actually be compiled against it:

1. ~~Build a throwaway WPF app...~~ **Measured.** A `net10.0-windows` project with `UseWPF` and a
   reference to `Pysar.Core`, containing nothing but
   `using System.Windows; public static ResourceDictionary Make() => new();`, fails to build:

   > error CS0433: The type 'ResourceDictionary' exists in both 'Pysar.Core' and
   > 'PresentationFramework'

   A hard error, not a warning, and the consumer's own code is what fails. **But only two of the
   seven types collide** — `ResourceDictionary` and `StaticResourceExtension`. The five
   `System.Windows.Markup.*` attributes do not, and the same probe confirms that is not because
   they are absent: a WPF project with no reference to `Core` resolves all five. WPF exposes them as
   **type forwards** into `System.Xaml`, and C# prefers a directly declared type over a forwarded
   one instead of calling it ambiguous. So the fix in step 2 only has to relocate two types, and
   `MarkupExtension`, `AmbientAttribute`, `XmlnsDefinitionAttribute`, `ContentPropertyAttribute` and
   `DictionaryKeyPropertyAttribute` can stay in `Core` as they are.
2. Try moving the shims into a separate opt-in assembly (e.g. `Pysar.Xaml.Compatibility`)
   that non-WPF hosts reference and `Pysar.Wpf` does not, then check in Visual Studio and
   Rider whether XAML IntelliSense survives. If it does, the collision goes away for consumers.

---

## 4. Deferred, needs its own design document

**The element model doubles as mutable render state.** `Report.PageNumber` / `PageCount` are stamped
into the design object per page by `PageBandResolver`; `Report.Build()` throws on a second call
because the build pipeline mutates the tree in place; `RepeaterExpander` destructively replaces
children. The workarounds are already in the code and say so themselves — `ReportRenderSession` holds
a `SemaphoreSlim _resolveGate` and does a full recursive `Freeze()`/`Clone()` per page to get a
stable snapshot, and `PageBandResolver`'s own doc warns that "a node from call N will silently show
call N+1's content if drawing is deferred past the next `ResolveAsync`."

Consequences: a `Report` cannot be rendered twice or concurrently; the viewer pays a deep clone per
page; a caller wanting both a PDF and an on-screen session must construct the report twice.

Separating the design (immutable after `Build`) from the render pass (per-page values in a
`PageContext` that binding resolution reads) would remove `_resolveGate`, `Freeze` and the one-shot
`Build` throw together. This is large and deserves a spec of its own, in the style of
`docs/superpowers/specs/2026-08-15-report-export-contracts-design.md`.

---

## Where things stand

Everything below is committed and verified on macOS (554+ tests green, all non-WPF projects build,
MAUI builds on maccatalyst/ios/android):

- Dead code removed from `Core` (`Handlers/`, `IDynamicReportContainer`)
- `ReportViewRenderer.Instance` throws instead of silently handing back an unconfigured renderer
- Blazor double-click now uses the shared `GestureModel.DoubleTap()` (it used to multiply by 2 forever)
- `ExportFormat` is a `readonly record struct`, so a new format is not a breaking change
- `IReportSharer` moved to `Pysar.Export` so non-MAUI hosts can inject it
- Compiled XAML no longer silently drops collection property-element children, and a generator
  failure falls back to the runtime loader instead of killing the whole compilation
- `FontCache` reads the font collection on every miss, so a font registered after the first glyph
  now takes effect
- `RectPt` replaced `SKRect` in `Tile`/`TileRequest`, so `IReportViewHost` is free of SkiaSharp
- `ReportViewSession.DisposeWhenStillDetached` closes the teardown leak on all three desktop hosts
- `ReportViewController` + `IReportViewSurface` extracted; all four hosts migrated (WPF on Windows,
  2026-08-15 — see §1)
- `IElementMeasurer` lets a custom element size itself under `Auto` instead of resolving to zero
