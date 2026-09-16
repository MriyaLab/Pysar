using Microsoft.CodeAnalysis;

namespace Pysar.Xaml.SourceGen;

[Generator(LanguageNames.CSharp)]
public sealed class ReportComponentGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Cache file text independently of compilation so a C# keystroke does not re-read every
        // .rxaml. Binding validation still needs symbols, so parse runs after Combine.
        var texts = context.AdditionalTextsProvider
            .Where(f => f.Path.EndsWith(".rxaml", StringComparison.OrdinalIgnoreCase))
            .Select((file, cancellationToken) => (
                Path: file.Path,
                Text: file.GetText(cancellationToken)?.ToString()));

        var xaml = texts.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(xaml, static (spc, pair) =>
        {
            var ((path, text), compilation) = pair;
            if (text is null) return;
            var model = XamlCodeModel.Parse(text, compilation, path, spc);
            if (model is null) return;
            spc.AddSource($"{model.ClassName}.Pysar.g.cs", model.Emit(spc));
        });
    }
}
