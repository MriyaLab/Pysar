# API versioning policy

Pysar follows Semantic Versioning 2.0.0 for published packages and documented release
artifacts.

## Current stability

The project is currently pre-1.0. During the `0.y.z` phase:

- a minor release may contain breaking changes;
- every known breaking change must be documented in release notes;
- patch releases must remain backward compatible with the corresponding minor release;
- avoid breaking changes when a deprecation path is practical.

Starting with `1.0.0`, breaking changes require a new major version.

## Version categories

### Patch

A patch release may include:

- bug fixes that restore documented behavior;
- performance improvements without observable semantic changes;
- internal refactoring;
- additional tests and documentation;
- compatible diagnostic-message improvements.

### Minor

A minor release may include:

- new public APIs;
- new report elements, bindings, converters, renderers, or XAML features;
- new optional behavior whose default preserves existing output;
- deprecations with migration guidance.

Before `1.0.0`, a minor release may also contain documented breaking changes.

### Major

After `1.0.0`, a major release is required for incompatible changes to supported contracts.

## Supported compatibility surface

The compatibility surface includes:

- public and protected .NET types and members in supported packages;
- documented exception types and lifecycle rules;
- supported XAML element, attribute, property-element, directive, and markup-extension syntax;
- XAML namespace mappings and CLR-backed element names;
- source-generator inputs, generated partial-class behavior, and diagnostic identifiers;
- report pagination and rendering behavior documented as contractual;
- serialized or persisted formats explicitly declared stable.

Internal types, implementation details, test seams, and undocumented generated local-variable names are
not compatibility contracts.

## Breaking changes

Examples include:

- removing or renaming a public type or member;
- making a public member less accessible;
- adding a required parameter or changing a return type;
- changing default values or report lifecycle semantics;
- changing XAML parsing or binding behavior for previously valid markup;
- removing or renaming a supported XAML element or namespace mapping;
- changing source-generator requirements in a way that breaks existing project files;
- materially changing pagination or layout output without an opt-in compatibility mode.

## Deprecation lifecycle

After `1.0.0`, a supported API should normally be marked obsolete for at least one minor release before
removal:

1. Add `[Obsolete]` with a concrete replacement or migration instruction.
2. Document the deprecation in release notes.
3. Keep the deprecated API functional during the announced transition period.
4. Remove it only in a major release.

Security, data-loss, or fundamentally incorrect APIs may require an accelerated process. Such exceptions
must be clearly documented.

## XAML compatibility

XAML is a first-class public API. Changes to parsing, type resolution, value conversion, bindings,
resources, styles, triggers, attached properties, or property-element semantics follow the same
compatibility rules as .NET APIs.

Runtime loading and compiled/source-generated XAML should have equivalent semantics for their shared
feature set. A parity regression is treated as a bug.

## Source-generator compatibility

Stable contracts include:

- recognition of `.rxaml` report files;
- standard `x:Class` partial-class generation and the documented legacy `Report.CodeBehind`
  compatibility path;
- generated `InitializeComponent()`;
- strongly typed `x:Name` fields;
- published diagnostic identifiers and their severity.

Generated implementation details may change between compatible releases.

## Change documentation

Each release should include:

- Added, Changed, Fixed, Deprecated, Removed, and Security sections as applicable;
- explicit `BREAKING` entries with migration instructions;
- affected packages and target frameworks;
- XAML or generated-code impact;
- links to replacement APIs or examples.

The project should adopt a root `CHANGELOG.md` before its first published package.
