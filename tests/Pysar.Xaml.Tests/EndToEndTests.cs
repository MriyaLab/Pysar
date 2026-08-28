using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class EndToEndTests
{
    private sealed record Row(string Col0);
    private sealed record Category(string Name, IReadOnlyList<Row> Items);
    private sealed record Vm(IReadOnlyList<Category> Categories);

    [Fact]
    public void Load_FullReport_BuildsBindsAndExpands()
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

        design.Build();  // Resolve → Expand → Resolve

        // Detail → outer StackPanel → rows StackPanel (one row per category).
        var rows = (StackPanel)((StackPanel)design.Detail.Children[0]).Children[0];
        Assert.Equal(2, rows.Children.Count);

        var catA = (StackPanel)((Frame)rows.Children[0]).Children[0];
        Assert.Equal("A", ((Text)catA.Children[0]).Content);
        var itemsA = (StackPanel)((StackPanel)catA.Children[1]).Children[0];
        Assert.Equal("a0", ((Text)((Frame)itemsA.Children[0]).Children[0]).Content);
    }
}
