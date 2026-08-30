using System.Globalization;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Console.Sample.Reports.Data;

public class InvoiceReportFluentApi
{
    public Report Build()
    {
        var invoice = Invoice.CreateSample();
        var report = new Report
        {
            DataContext = invoice,
            PageFormat = new PageFormat { Size = PageSize.A4, Orientation = Orientation.Portrait }
        };

        var header = new ReportHeaderBand
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Margin = new Thickness(0, 0, 0, 15)
        };
        header.AddElement(new Grid()
            .WithRowDefinitions("Auto, Auto, Auto, Auto")
            .WithRowSpacing(25)
            .AddElement(Label("Invoicing", 18, FontStyle.Bold)
                .WithHorizontalAlignment(Alignment.End), 0, 0)
            .AddElement(new StackPanel()
                .AddElement(Label("replace with", 18)
                    .WithHorizontalAlignment(Alignment.Start)
                    .WithMargin(0, -5))
                .AddElement(Label("LOGO", 48, FontStyle.Bold)
                    .WithHorizontalAlignment(Alignment.Start)), 1, 0)
            .AddElement(BuildCompanyBlock(invoice.Company), 2, 0)
            .AddElement(BuildBillShipDetails(invoice), 3, 0));

        report.Detail
            .WithData(invoice.Items, (item, row) => row.AddElement(BuildItemRow(item)));
        report.Detail.DetailHeader = BuildItemsHeader();
        report.Detail.DetailFooter = BuildItemsFooter(invoice);

        report.Bands.Add(header);
        report.Bands.Add(BuildPageFooter(report));
        return report.Build();
    }

    private static Grid BuildCompanyBlock(Organization company)
    {
        return new Grid()
            .WithColumnDefinitions("*, *")
            .WithRowDefinitions("Auto, Auto")
            .WithRowSpacing(10)
            .AddElement(new Grid()
                .WithSize(200, SizeLength.Auto)
                .WithHorizontalAlignment(Alignment.Start)
                .WithRowDefinitions("Auto, Auto")
                .WithRowSpacing(5)
                .AddElement(Label(company.Name, 16, FontStyle.Bold), 0, 0)
                .AddElement(Label(company.Address, 14)
                    .WithTextTrimming(TextTrimming.WordWrap), 1, 0), 0, 0)
            .AddElement(new Grid()
                .WithSize(SizeLength.Auto, SizeLength.Auto)
                .WithHorizontalAlignment(Alignment.End)
                .WithColumnDefinitions("Auto, Auto")
                .WithRowDefinitions("Auto, Auto, Auto")
                .WithColumnSpacing(10)
                .WithRowSpacing(5)
                .AddElement(Label("Phone:", 14, FontStyle.Bold), 0, 0)
                .AddElement(Label(company.Phone ?? string.Empty, 14), 0, 1)
                .AddElement(Label("Email:", 14, FontStyle.Bold), 1, 0)
                .AddElement(Label(company.Email ?? string.Empty, 14), 1, 1)
                .AddElement(Label("Web:", 14, FontStyle.Bold), 2, 0)
                .AddElement(Label(company.Website ?? string.Empty, 14), 2, 1), 0, 1);
    }

    private static Grid BuildBillShipDetails(Invoice invoice) =>
        new Grid()
            .WithPadding(15)
            .WithBackgroundColor("#EEEEEE")
            .WithColumnDefinitions("150, 150, *")
            .WithColumnSpacing(5)
            .AddElement(AddressColumn("Bill to:", invoice.BillTo.Name, invoice.BillTo.Address), 0, 0)
            .AddElement(AddressColumn("Ship to:", invoice.ShipTo.Name, invoice.ShipTo.Address), 0, 1)
            .AddElement(new Grid()
                .WithSize(SizeLength.Auto, SizeLength.Auto)
                .WithHorizontalAlignment(Alignment.End)
                .WithColumnDefinitions("Auto, *")
                .WithRowDefinitions("Auto, Auto, Auto, Auto")
                .WithColumnSpacing(5)
                .AddElement(Label("Details:", 14, FontStyle.Bold), 0, 0)
                .AddElement(Label("Number", 14, FontStyle.Bold), 1, 0)
                .AddElement(Label(invoice.Number, 14).WithTextTrimming(TextTrimming.WordWrap), 1, 1)
                .AddElement(Label("Date", 14, FontStyle.Bold), 2, 0)
                .AddElement(Label(invoice.Date, 14).WithTextTrimming(TextTrimming.WordWrap), 2, 1), 0, 2);

    private static StackPanel AddressColumn(string title, string name, string address) =>
        new StackPanel()
            .AddElement(Label(title, 14, FontStyle.Bold))
            .AddElement(Label(name, 14))
            .AddElement(Label(address, 14).WithTextTrimming(TextTrimming.WordWrap));

    private static Grid BuildItemsHeader() =>
        new Grid()
            .WithSize(SizeLength.Fill, 40)
            .WithMargin(0, 0, 0, 5)
            .WithBorderColor("#CCCCCC")
            .WithBorderThickness(0, 0, 0, 1)
            .WithColumnDefinitions("30, *, 80, 80, 80")
            .AddElement(HeaderCell("#"), 0, 0)
            .AddElement(HeaderCell("Product"), 0, 1)
            .AddElement(HeaderCell("Quantity").WithHorizontalAlignment(Alignment.Center), 0, 2)
            .AddElement(HeaderCell("Unit Price").WithHorizontalAlignment(Alignment.End), 0, 3)
            .AddElement(HeaderCell("Total").WithHorizontalAlignment(Alignment.End), 0, 4);

    private static Grid BuildItemRow(InvoiceItem item) =>
        new Grid()
            .WithSize(SizeLength.Fill, SizeLength.Auto)
            .WithMinHeight(30)
            .WithBorderColor("#CCCCCC")
            .WithBorderThickness(0, 0, 0, 1)
            .WithColumnDefinitions("30, *, 80, 80, 80")
            .AddElement(ItemCell(item.Index.ToString()), 0, 0)
            .AddElement(ItemCell(item.Product)
                .WithSize(SizeLength.Fill, SizeLength.Auto)
                .WithVerticalTextAlignment(TextAlignment.Center), 0, 1)
            .AddElement(ItemCell(item.Quantity.ToString()).WithHorizontalAlignment(Alignment.Center), 0, 2)
            .AddElement(ItemCell(item.UnitPrice.ToString(CultureInfo.InvariantCulture)).WithHorizontalAlignment(Alignment.End), 0, 3)
            .AddElement(ItemCell(item.Total.ToString(CultureInfo.InvariantCulture)).WithHorizontalAlignment(Alignment.End), 0, 4);

    private static Grid BuildItemsFooter(Invoice invoice) =>
        new Grid()
            .WithSize(SizeLength.Fill, 35)
            .WithBackgroundColor("#EEEEEE")
            .WithColumnDefinitions("*, Auto")
            .WithColumnSpacing(10)
            .AddElement(Label("Total", 14, FontStyle.Bold)
                .WithHorizontalAlignment(Alignment.End)
                .WithVerticalAlignment(Alignment.Center), 0, 0)
            .AddElement(Label(invoice.Total.ToString(CultureInfo.InvariantCulture), 14, FontStyle.Bold)
                .WithHorizontalAlignment(Alignment.End)
                .WithVerticalAlignment(Alignment.Center), 0, 1);

    private static PageFooterBand BuildPageFooter(Report report)
    {
        var pageNumber = new Text().WithFont("Kanit", 10f, Colors.Black);
        var pageCount = new Text().WithFont("Kanit", 10f, Colors.Black);
        report.WithPageChanged((number, count) =>
        {
            pageNumber.Content = number.ToString();
            pageCount.Content = count.ToString();
        });

        var footer = new PageFooterBand
        {
            Size = new Size(SizeLength.Fill, 30),
            Margin = new Thickness(-40, 0, -40, -30),
            Padding = new Thickness(25, 0)
        };
        footer.AddElement(new StackPanel()
            .WithOrientation(StackOrientation.Horizontal)
            .WithSize(Size.Auto)
            .WithHorizontalAlignment(Alignment.End)
            .WithVerticalAlignment(Alignment.Center)
            .AddElements(
            [
                new Text { Content = "Page " }.WithFont("Kanit", 10f, Colors.Black),
                pageNumber,
                new Text { Content = " of " }.WithFont("Kanit", 10f, Colors.Black).WithPadding(3, 0),
                pageCount
            ]));
        return footer;
    }

    private static Text HeaderCell(string content) =>
        Label(content, 14, FontStyle.Bold).WithVerticalAlignment(Alignment.Center);

    private static Text ItemCell(string content) =>
        Label(content, 14).WithVerticalAlignment(Alignment.Center);

    private static Text Label(string content, float size, FontStyle style = FontStyle.Normal) =>
        new Text { Content = content }.WithFont("Kanit", size, Colors.Black, style);
}
