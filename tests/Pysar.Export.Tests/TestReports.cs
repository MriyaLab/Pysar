using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Export.Tests;

internal static class TestReports
{
    public static Report Minimal() =>
        ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();
}
