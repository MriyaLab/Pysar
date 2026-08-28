using Pysar.Elements;

namespace Pysar.Xaml.CodeBehind.Tests;

public partial class SalesReport
{
    public SalesReport() => InitializeComponent();

    // Prove (at compile time) that the generated strongly-typed x:Name fields exist.
    internal PageHeaderBand HeaderField => Header;
    internal Text CellField => Cell;
}
