using System;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class BindingValidationTests
{
    private const string Types = """
        namespace App
        {
            public class Invoice
            {
                public string Number { get; set; }
                public Customer Customer { get; set; }
                public System.Collections.Generic.List<Item> Items { get; set; }
                public System.Collections.Generic.Dictionary<string, object> Extras { get; set; }
                public string LooseField;
            }
            public class Customer { public string Name { get; set; } }
            public class Item { public string ItemOnlyProp { get; set; } }
            public interface IDocument { string Number { get; } }
            public interface IInvoice : IDocument { Customer Customer { get; } }
            public class Outer
            {
                public string Title { get; set; }
                public System.Collections.Generic.IReadOnlyList<IInvoice> Documents { get; set; }
            }
        }
        namespace X { public partial class Y { } }
        """;

    private static bool HasDiagnostic(GenResult result, string id) =>
        System.Linq.Enumerable.Any(result.Diagnostics, d => d.Id == id);

    private static string Report(string dataType, string body) => $$"""
        <Report x:Class="X.Y"
                xmlns="https://mriyalab.com/pysar"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:vm="clr-namespace:App"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                {{dataType}}>
            {{body}}
        </Report>
        """;

    private static bool HasBindingError(GenResult result) =>
        System.Linq.Enumerable.Any(
            result.Diagnostics,
            d => d.Id == "PQX010" && d.Severity == DiagnosticSeverity.Error);

    [Fact]
    public void UnknownProperty_ReportsError()
    {
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"", "<Text Text=\"{Binding Nope}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void KnownProperty_NoError()
    {
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"", "<Text Text=\"{Binding Number}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void XDataTypeDirective_Interop_StillValidated()
    {
        // MAUI-style x:DataType remains honoured as a fallback for d:DataContext.
        var xaml = Report("x:DataType=\"vm:Invoice\"", "<Text Text=\"{Binding Nope}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void ValidNestedPath_NoError()
    {
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"", "<Text Text=\"{Binding Customer.Name}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void UnknownNestedLeaf_ReportsError()
    {
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"", "<Text Text=\"{Binding Customer.Nope}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void WithoutDataType_SkipsValidation()
    {
        var xaml = Report(dataType: "", "<Text Text=\"{Binding Nope}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void DataContext_RetargetsChildrenToMemberType()
    {
        // DataContext switches the child scope to Customer, so Name (on Customer, not Invoice)
        // is valid and must not be flagged.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Grid DataContext=\"{Binding Customer}\"><Text Text=\"{Binding Name}\" /></Grid>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void DataContext_StillFlagsUnknownMemberOnRetargetedType()
    {
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Grid DataContext=\"{Binding Customer}\"><Text Text=\"{Binding Nope}\" /></Grid>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void DataSourceItemScope_ValidatesChildrenAgainstElementType()
    {
        // The DataSource collection's element type is Item, so ItemOnlyProp (on Item) is valid.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Repeater DataSource=\"{Binding Items}\"><Text Text=\"{Binding ItemOnlyProp}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void DataSourceItemScope_FlagsUnknownMemberOnElementType()
    {
        // Nope is not on the element type Item, so an item-scoped binding to it must be flagged.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Repeater DataSource=\"{Binding Items}\"><Text Text=\"{Binding Nope}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void DataSourcePropertyElement_StaysInOuterContext()
    {
        // The DetailFooter is a property element (chrome), so it binds against the outer Invoice
        // (Number is on Invoice), while the content row binds against the item (ItemOnlyProp).
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<DetailBand DataSource=\"{Binding Items}\">"
            + "<DetailBand.DetailFooter><Text Text=\"{Binding Number}\" /></DetailBand.DetailFooter>"
            + "<Text Text=\"{Binding ItemOnlyProp}\" />"
            + "</DetailBand>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void DataSourceContent_FlagsItemMemberUsedAsIfOuter()
    {
        // A content-row binding to an outer-only member (Number is on Invoice, not the item) is flagged.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<DetailBand DataSource=\"{Binding Items}\"><Text Text=\"{Binding Number}\" /></DetailBand>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void DictionaryContext_AcceptsAnyKey()
    {
        // Extras is IDictionary<string,object>; any key is valid at runtime, so it must not be flagged.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Grid DataContext=\"{Binding Extras}\"><Text Text=\"{Binding AnyRandomKey}\" /></Grid>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void FieldBinding_IsFlagged()
    {
        // LooseField is a field, not a property; the runtime resolver ignores fields, so it is invalid.
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"", "<Text Text=\"{Binding LooseField}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void UnresolvableDataType_ReportsWarningNotError()
    {
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:DoesNotExist}\"", "<Text Text=\"{Binding Whatever}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasDiagnostic(result, "PQX011"));
        Assert.False(HasBindingError(result)); // no PQX010 — bindings under it are not validated
    }

    [Fact]
    public void ReportWithoutXClass_StillValidatesBindings()
    {
        // No x:Class: the generator produces nothing, but binding validation must still run.
        const string xaml = """
            <Report xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:vm="clr-namespace:App"
                    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                    d:DataContext="{d:DesignInstance Type=vm:Invoice}">
                <Text Text="{Binding Nope}" />
            </Report>
            """;

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.Null(result.GeneratedSource);
        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void DataSourceItemScope_UnresolvableSource_SkipsChildren()
    {
        // The DataSource path itself is invalid (flagged separately), so the item type is unknown
        // and child bindings are not validated against a wrong type.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Repeater DataSource=\"{Binding NoSuchCollection}\"><Text Text=\"{Binding Whatever}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        // The DataSource binding is flagged; the child is not double-flagged against a wrong type.
        var errors = System.Linq.Enumerable.ToList(
            System.Linq.Enumerable.Where(result.Diagnostics, d => d.Id == "PQX010"));
        Assert.Single(errors);
        Assert.Contains("NoSuchCollection", errors[0].GetMessage());
    }

    [Fact]
    public void Diagnostic_SpansTheOffendingAttributeOnly()
    {
        // The trailing Margin attribute must stay outside the squiggle.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Text Text=\"{Binding Nope}\" Margin=\"5\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        var error = System.Linq.Enumerable.Single(result.Diagnostics, d => d.Id == "PQX010");
        var lineSpan = error.Location.GetLineSpan();
        var line = xaml.Split('\n')[lineSpan.StartLinePosition.Line].TrimEnd('\r');
        const string attribute = "Text=\"{Binding Nope}\"";

        Assert.Equal(line.IndexOf(attribute, StringComparison.Ordinal), lineSpan.StartLinePosition.Character);
        Assert.Equal(
            line.IndexOf(attribute, StringComparison.Ordinal) + attribute.Length,
            lineSpan.EndLinePosition.Character);
    }

    [Fact]
    public void InterfaceDataType_ResolvesInheritedInterfaceMembers()
    {
        // Number is declared on IDocument, which IInvoice extends. Interfaces have no base type,
        // so the members of extended interfaces must be enumerated as well.
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:IInvoice}\"", "<Text Text=\"{Binding Number}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void InterfaceItemType_ResolvesInheritedInterfaceMembers()
    {
        // The item type of IReadOnlyList<IInvoice> is an interface; nested paths through it must
        // resolve across the whole interface hierarchy.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Outer}\"",
            "<Repeater DataSource=\"{Binding Documents}\"><Text Text=\"{Binding Customer.Name}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void InterfaceDataType_StillFlagsUnknownMember()
    {
        var xaml = Report("d:DataContext=\"{d:DesignInstance Type=vm:IInvoice}\"", "<Text Text=\"{Binding Nope}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void DataSourceOnNodeWithOwnDataType_ResolvesAgainstThatType()
    {
        // The node's own DataType describes the node's own context, so its DataSource path resolves
        // against it — not against the inherited outer type (which has no Items).
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Outer}\"",
            "<Repeater d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\" DataSource=\"{Binding Items}\">"
            + "<Text Text=\"{Binding ItemOnlyProp}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void DataContextOnNodeWithOwnDataType_PathResolvesAgainstInheritedType()
    {
        // The opposite of DataSource: a runtime DataContext binding is evaluated before it replaces
        // the context, so its own path resolves against the inherited type (Customer is on Invoice).
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Grid d:DataContext=\"{d:DesignInstance Type=vm:Customer}\" DataContext=\"{Binding Customer}\" />");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void DataContextOnNodeWithOwnDataType_ChildrenValidatedAgainstThatType()
    {
        // The node's own DataType describes the context its DataContext produces, so the children
        // must be validated against it — not left unvalidated because the path was walked twice.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Grid d:DataContext=\"{d:DesignInstance Type=vm:Customer}\" DataContext=\"{Binding Customer}\">"
            + "<Text Text=\"{Binding NoSuchMember}\" /></Grid>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void DataContextOnNodeWithOwnDataType_ValidChildBinding_NoError()
    {
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Grid d:DataContext=\"{d:DesignInstance Type=vm:Customer}\" DataContext=\"{Binding Customer}\">"
            + "<Text Text=\"{Binding Name}\" /></Grid>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void LegacyDataSourcePath_PutsChildrenInItemScope()
    {
        // DataSourcePath is the literal, pre-binding form of DataSource; it expands per item at
        // runtime, so its children belong to the item scope just the same.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Repeater DataSourcePath=\"Items\"><Text Text=\"{Binding ItemOnlyProp}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void LegacyDataSourcePath_StillFlagsUnknownItemMember()
    {
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Repeater DataSourcePath=\"Items\"><Text Text=\"{Binding Number}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void StringDataSource_LeavesChildrenUnvalidated()
    {
        // A string is IEnumerable<char>, but the runtime expander refuses to iterate one, so it is
        // not a collection here either — the item type stays unknown rather than becoming char.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Repeater DataSource=\"{Binding Number}\"><Text Text=\"{Binding Anything}\" /></Repeater>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void StyleInResources_IsNotValidated()
    {
        // A style is reusable: it is applied to elements in scopes the declaration knows nothing
        // about, so its setters must not be validated against the report's own data type.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Report.Resources><ResourceDictionary>"
            + "<Style x:Key=\"S\" TargetType=\"Text\">"
            + "<Setter Member=\"Text\" Value=\"{Binding ItemOnlyProp}\" /></Style>"
            + "</ResourceDictionary></Report.Resources>"
            + "<DetailBand DataSource=\"{Binding Items}\"><Text Style=\"{StaticResource S}\" /></DetailBand>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void DataTriggerPath_IsValidated()
    {
        // DataTrigger.Binding is a plain property path, resolved by the same runtime resolver.
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Text Text=\"x\"><Text.Triggers>"
            + "<DataTrigger Binding=\"NoSuchMember\" Value=\"1\" /></Text.Triggers></Text>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void DataTriggerPath_Valid_NoError()
    {
        var xaml = Report(
            "d:DataContext=\"{d:DesignInstance Type=vm:Invoice}\"",
            "<Text Text=\"x\"><Text.Triggers>"
            + "<DataTrigger Binding=\"Customer.Name\" Value=\"1\" /></Text.Triggers></Text>");

        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void SourceBinding_MisspelledProperty_ReportsPQX010()
    {
        var result = GeneratorTestHarness.Run(
            @"namespace MyApp {
                public partial class Header {
                    public string Title { get; set; } = string.Empty;
                } }",
            ("Header.rxaml",
                "<ReportView x:Class=\"MyApp.Header\" x:Name=\"root\" "
                + "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
                + "<Text Content=\"{Binding Titel, Source={x:Reference root}}\"/></ReportView>"));

        Assert.True(HasBindingError(result));
    }

    [Fact]
    public void SourceBinding_MemberInheritedFromTheRootElementType_ReportsNothing()
    {
        // The x:Class partial declares no base type — the generator supplies it — so the class symbol
        // alone cannot see Report.PageNumber. The root element's own type has to be consulted too.
        var result = GeneratorTestHarness.Run(
            @"namespace MyApp {
                public partial class Sales {
                } }",
            ("Sales.rxaml",
                "<Report x:Class=\"MyApp.Sales\" x:Name=\"root\" "
                + "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
                + "<PageFooterBand><Text Content=\"{Binding PageNumber, Source={x:Reference root}}\"/>"
                + "</PageFooterBand></Report>"));

        Assert.False(HasBindingError(result));
    }

    [Fact]
    public void SourceBinding_ValidProperty_ReportsNothing()
    {
        var result = GeneratorTestHarness.Run(
            @"namespace MyApp {
                public partial class Header {
                    public string Title { get; set; } = string.Empty;
                } }",
            ("Header.rxaml",
                "<ReportView x:Class=\"MyApp.Header\" x:Name=\"root\" "
                + "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
                + "<Text Content=\"{Binding Title, Source={x:Reference root}}\"/></ReportView>"));

        Assert.False(HasBindingError(result));
    }
}
