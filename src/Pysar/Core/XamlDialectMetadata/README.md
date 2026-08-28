# XamlDialectMetadata

Types in here are never read by Pysar's own loader or source generator - both read the
equivalent markers in `Pysar.Elements`. These exist only so IDE XAML language services
recognize Pysar's elements under whichever dialect a consuming project is in.

An IDE resolves this kind of metadata by CLR full name, one dialect at a time - `System.Windows`
for WPF, which is also the fallback used when no platform-specific dialect applies. A type
declared under the wrong name is invisible to that dialect, however identical it is otherwise -
which is why every folder here is named after the dialect whose namespace its types occupy, not
after Pysar's own module layout.

**Before adding a dialect, check that something still asks for it.** A `Maui` folder lived here
briefly, for ReSharper: it derives a XAML dialect per project from the assemblies that project
references, so a report inside a .NET MAUI app was read as MAUI markup and its content properties
looked for under `Microsoft.Maui.Controls` rather than `System.Windows`. The Pysar plugins have
since stopped handing reports to a XAML engine at all - VS Code, Visual Studio and Rider all read
`.rxaml` as XML and apply Pysar's own schema - which left that folder with no reader, so it was
removed.

Squatting a platform's namespace costs something real: the type is public, it ships to every
consumer, and it collides outright if the platform ever declares that name itself. Do it only for
a dialect that is demonstrably reading this metadata, and record what you checked.
