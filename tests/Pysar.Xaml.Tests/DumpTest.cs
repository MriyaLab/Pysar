using System.Text;
using Pysar.Elements;
using Pysar.Core.Abstractions;
using Pysar.Xaml;
using Xunit;
using Xunit.Abstractions;

namespace Pysar.Xaml.Tests;

public class DumpTest
{
    private readonly ITestOutputHelper _o;
    public DumpTest(ITestOutputHelper o) => _o = o;

    private sealed record Row(string Col0);
    private sealed record Category(string Name, IReadOnlyList<Row> Items);
    private sealed record Vm(IReadOnlyList<Category> Categories);

    [Fact]
    public void Dump()
    {
        const string xaml = """
        <Report xmlns="https://mriyalab.com/pysar" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <PageFormat Size="A4" Orientation="Portrait" Margin="30"/>
          <DetailBand DataSource="{Binding Categories}">
            <StackPanel>
              <Text Content="{Binding Name}"/>
              <Repeater DataSource="{Binding Items}">
                <Text Content="{Binding Col0}"/>
              </Repeater>
            </StackPanel>
          </DetailBand>
        </Report>
        """;
        var design = ReportXaml.Load(xaml);
        design.DataContext = new Vm(new[]
        {
            new Category("A", new[] { new Row("a0"), new Row("a1") }),
            new Category("B", new[] { new Row("b0") }),
        });
        design.Build();
        var sb = new StringBuilder();
        DumpNode(design.Detail, 0, sb);
        _o.WriteLine(sb.ToString());
    }

    private static void DumpNode(object el, int d, StringBuilder sb)
    {
        var pad = new string(' ', d * 2);
        string extra = "";
        if (el is Text t) extra = $" Content='{t.Content}'";
        sb.AppendLine($"{pad}{el.GetType().Name}{extra}");
        if (el is IReportContainer c)
            foreach (var ch in c.Children) DumpNode(ch, d + 1, sb);
    }
}
