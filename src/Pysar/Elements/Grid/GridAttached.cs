using System.Runtime.CompilerServices;
using Pysar.Core.Abstractions;
using Pysar.Core.Structs;

namespace Pysar.Elements;

internal class GridCellData
{
    public int Row        { get; set; } = 0;
    public int Column     { get; set; } = 0;
    public int RowSpan    { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
}

public static class GridAttached
{
    private static readonly ConditionalWeakTable<IReportElement, GridCellData> _table = new();

    private static GridCellData GetOrCreate(IReportElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _table.GetOrCreateValue(element);
    }

    public static int GetRow(IReportElement element) => GetOrCreate(element).Row;
    public static void SetRow(IReportElement element, int value) => GetOrCreate(element).Row = value;

    public static int GetColumn(IReportElement element) => GetOrCreate(element).Column;
    public static void SetColumn(IReportElement element, int value) => GetOrCreate(element).Column = value;

    public static int GetRowSpan(IReportElement element) => GetOrCreate(element).RowSpan;
    public static void SetRowSpan(IReportElement element, int value) => GetOrCreate(element).RowSpan = value;

    public static int GetColumnSpan(IReportElement element) => GetOrCreate(element).ColumnSpan;
    public static void SetColumnSpan(IReportElement element, int value) => GetOrCreate(element).ColumnSpan = value;
}