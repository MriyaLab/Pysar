using Pysar.Elements;

//TODO: After separating RXAML do we need that in Pysar?

// Runtime resolution: XamlTypeResolver reads these.
[assembly: XmlnsDefinition("https://mriyalab.com/pysar", "Pysar.Elements")]
[assembly: XmlnsDefinition("https://mriyalab.com/pysar", "Pysar.Core.Structs")]

// Duplicated deliberately: VS and Rider only recognise Microsoft's attribute, by full name.
// Delete these and XAML IntelliSense silently stops offering QReport elements.
// See docs/handoff-windows.md.
[assembly: System.Windows.Markup.XmlnsDefinition(
    "https://mriyalab.com/pysar",
    "Pysar.Elements")]
[assembly: System.Windows.Markup.XmlnsDefinition(
    "https://mriyalab.com/pysar",
    "Pysar.Core.Structs")]
