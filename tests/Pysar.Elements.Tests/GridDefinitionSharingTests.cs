using Pysar.Binding;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class GridDefinitionSharingTests
{
    [Fact]
    public void ClearValue_ColumnDefinitions_DoesNotShareAListAcrossGrids()
    {
        var first = new Grid();
        var second = new Grid();

        first.ClearValue(Grid.ColumnDefinitionsProperty);
        second.ClearValue(Grid.ColumnDefinitionsProperty);

        first.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star()));

        Assert.Empty(second.ColumnDefinitions);
    }

    [Fact]
    public void ClearValue_RowDefinitions_DoesNotShareAListAcrossGrids()
    {
        var first = new Grid();
        var second = new Grid();

        first.ClearValue(Grid.RowDefinitionsProperty);
        second.ClearValue(Grid.RowDefinitionsProperty);

        first.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        Assert.Empty(second.RowDefinitions);
    }
}
