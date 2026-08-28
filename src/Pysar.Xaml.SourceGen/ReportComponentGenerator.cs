using Microsoft.CodeAnalysis;

namespace Pysar.Xaml.SourceGen;

[Generator(LanguageNames.CSharp)]
public sealed class ReportComponentGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var xaml = context.AdditionalTextsProvider
            // Reports are .rxaml and nothing else. Host SDKs glob **/*.xaml into their own item
            // type, so a report sharing that extension is claimed by whichever UI framework the
            // consuming project uses before this generator is ever reached.
            .Where(f => f.Path.EndsWith(".rxaml", StringComparison.OrdinalIgnoreCase))
            .Combine(context.CompilationProvider);

        context.RegisterSourceOutput(xaml, static (spc, pair) =>
        {
            var (file, compilation) = pair;
            var text = file.GetText(spc.CancellationToken)?.ToString();
            if (text is null) return;
            var model = XamlCodeModel.Parse(text, compilation, file.Path, spc);
            if (model is null) return;
            spc.AddSource($"{model.ClassName}.Pysar.g.cs", model.Emit());
        });
    }
}
