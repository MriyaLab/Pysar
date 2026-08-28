using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Pysar.Xaml.SourceGen.Tests;

internal sealed record GenResult(string? GeneratedSource, ImmutableArray<Diagnostic> Diagnostics);

internal static class GeneratorTestHarness
{
    private sealed class TextFile(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;
        public override SourceText GetText(CancellationToken ct = default) => SourceText.From(text);
    }

    public static GenResult Run(
        string csharp,
        params (string path, string xaml)[] xamlFiles)
        => Run(csharp, withXamlRuntime: true, xamlFiles);

    /// <summary>
    ///     <paramref name="withXamlRuntime"/> false reproduces a project that has the generator but
    ///     no reference to Pysar.Xaml, which is what PQX007 exists to explain.
    /// </summary>
    public static GenResult Run(
        string csharp,
        bool withXamlRuntime,
        params (string path, string xaml)[] xamlFiles)
    {
        var types = new List<Type>
        {
            typeof(object), typeof(Pysar.Elements.Report),
            typeof(Pysar.Core.Structs.Font), typeof(System.Collections.IEnumerable)
        };
        if (withXamlRuntime)
            types.Add(typeof(Pysar.Xaml.ReportXaml));

        var refs = types
            .Select(t => t.Assembly.Location).Distinct()
            .Select(p => MetadataReference.CreateFromFile(p))
            .Concat(Basic());

        var compilation = CSharpCompilation.Create("TestAsm",
            new[] { CSharpSyntaxTree.ParseText(csharp) }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ReportComponentGenerator())
            .AddAdditionalTexts(xamlFiles.Select(f => (AdditionalText)new TextFile(f.path, f.xaml)).ToImmutableArray());

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var generated = runResult.GeneratedTrees.FirstOrDefault()?.ToString();
        return new GenResult(generated, runResult.Diagnostics);
    }

    /// <summary>
    ///     The framework assemblies the generator's own symbol queries need. System.Collections is
    ///     required for a real reason, not for completeness: without it <c>List&lt;T&gt;</c> resolves
    ///     as an error type with no interfaces, so every check the emitter makes about a collection
    ///     property silently answers "no" and the tests pass against a compilation the compiler would
    ///     never hand it.
    /// </summary>
    private static IEnumerable<MetadataReference> Basic()
    {
        var dir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        foreach (var name in new[]
                 {
                     "System.Runtime.dll", "System.Private.CoreLib.dll", "netstandard.dll",
                     "System.Collections.dll"
                 })
        {
            var p = System.IO.Path.Combine(dir, name);
            if (System.IO.File.Exists(p)) yield return MetadataReference.CreateFromFile(p);
        }
    }
}
