using Pysar.Core.Structs;
using Pysar.Elements;
using Xunit;

namespace Pysar.Xaml.Tests;

public class GridDefinitionTests
{
    private const string Root = "xmlns=\"https://mriyalab.com/pysar\"";

    [Fact]
    public void ColumnDefinitions_PropertyElement_Builds()
    {
        var grid = XamlTestHost.BuildElement<Grid>($@"<Grid {Root}>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""Auto"" />
                <ColumnDefinition Width=""*"" />
                <ColumnDefinition Width=""60"" />
            </Grid.ColumnDefinitions>
        </Grid>");

        Assert.Equal(3, grid.ColumnDefinitions.Count);
        Assert.Equal(GridLengthType.Auto, grid.ColumnDefinitions[0].Width.Type);
        Assert.Equal(GridLengthType.Star, grid.ColumnDefinitions[1].Width.Type);
        Assert.Equal(GridLengthType.Fixed, grid.ColumnDefinitions[2].Width.Type);
        Assert.Equal(60f, grid.ColumnDefinitions[2].Width.Value);
    }

    [Fact]
    public void ColumnDefinitions_PrefixlessChild_Builds()
    {
        var grid = XamlTestHost.BuildElement<Grid>($@"<Grid {Root}>
            <ColumnDefinitions>
                <ColumnDefinition Width=""Auto"" />
                <ColumnDefinition Width=""*"" />
            </ColumnDefinitions>
        </Grid>");

        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(GridLengthType.Auto, grid.ColumnDefinitions[0].Width.Type);
    }

    [Fact]
    public void ColumnDefinitions_AttributeString_Builds()
    {
        var grid = XamlTestHost.BuildElement<Grid>(
            $"<Grid {Root} ColumnDefinitions=\"*, 2*, Auto, 300\" />");

        Assert.Equal(4, grid.ColumnDefinitions.Count);
        Assert.Equal(GridLengthType.Star, grid.ColumnDefinitions[0].Width.Type);
        Assert.Equal(1f, grid.ColumnDefinitions[0].Width.Value);
        Assert.Equal(GridLengthType.Star, grid.ColumnDefinitions[1].Width.Type);
        Assert.Equal(2f, grid.ColumnDefinitions[1].Width.Value);
        Assert.Equal(GridLengthType.Auto, grid.ColumnDefinitions[2].Width.Type);
        Assert.Equal(GridLengthType.Fixed, grid.ColumnDefinitions[3].Width.Type);
        Assert.Equal(300f, grid.ColumnDefinitions[3].Width.Value);
    }

    [Fact]
    public void RowDefinitions_AttributeString_Builds()
    {
        var grid = XamlTestHost.BuildElement<Grid>(
            $"<Grid {Root} RowDefinitions=\"1*, Auto, 25, 14, 20\" />");

        Assert.Equal(5, grid.RowDefinitions.Count);
        Assert.Equal(GridLengthType.Star, grid.RowDefinitions[0].Height.Type);
        Assert.Equal(GridLengthType.Auto, grid.RowDefinitions[1].Height.Type);
        Assert.Equal(25f, grid.RowDefinitions[2].Height.Value);
    }

    [Fact]
    public void RowDefinitions_PrefixlessChild_Builds()
    {
        var grid = XamlTestHost.BuildElement<Grid>($@"<Grid {Root}>
            <RowDefinitions>
                <RowDefinition Height=""20"" />
                <RowDefinition Height=""*"" />
            </RowDefinitions>
        </Grid>");

        Assert.Equal(2, grid.RowDefinitions.Count);
        Assert.Equal(20f, grid.RowDefinitions[0].Height.Value);
    }
}
