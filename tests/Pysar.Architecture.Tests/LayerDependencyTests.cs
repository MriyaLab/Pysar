using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Pysar.Architecture.Tests;

/// <summary>
///     Core, Binding, Elements, Export and Skia used to be separate projects, and the compiler
///     refused any reference against that order. They are one assembly now, so this test is the
///     only thing standing between the layering and a stray using directive.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly string[] Order = ["Core", "Binding", "Elements", "Export", "Skia"];

    public static TheoryData<string> Layers()
    {
        var data = new TheoryData<string>();
        foreach (var layer in Order) data.Add(layer);
        return data;
    }

    [Theory]
    [MemberData(nameof(Layers))]
    public void Layer_ReferencesOnlyLayersBeneathIt(string layer)
    {
        var rank = Array.IndexOf(Order, layer);
        var forbidden = Order.Skip(rank + 1).ToArray();
        var layerDir = Path.Combine(RepoRoot.Path, "src", "Pysar", layer);

        Assert.True(Directory.Exists(layerDir), $"Layer folder missing: {layerDir}");

        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(layerDir, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

            var referenced = root.DescendantNodes()
                .OfType<QualifiedNameSyntax>()
                .Select(n => n.ToString())
                .Where(n => n.StartsWith("Pysar.", StringComparison.Ordinal));

            foreach (var name in referenced)
            {
                var segment = name["Pysar.".Length..].Split('.')[0];
                if (forbidden.Contains(segment))
                    violations.Add($"{Path.GetRelativePath(RepoRoot.Path, file)} -> Pysar.{segment}");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"{layer} may only reference {string.Join(", ", Order.Take(rank + 1))}. Violations:\n"
            + string.Join("\n", violations.Distinct()));
    }
}
