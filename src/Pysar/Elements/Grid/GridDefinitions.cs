using Pysar.Core.Structs;

namespace Pysar.Elements;

public class ColumnDefinition
{
    public GridLength Width { get; set; }

    public ColumnDefinition() => Width = GridLength.Star();
    public ColumnDefinition(GridLength width) => Width = width;
}

public class RowDefinition
{
    public GridLength Height { get; set; }

    public RowDefinition() => Height = GridLength.Star();
    public RowDefinition(GridLength height) => Height = height;
}