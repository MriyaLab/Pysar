using System.Globalization;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Console.Sample.Reports.Data;

public class InvoiceReport
{
    public Report Build()
    {
        var invoice = Invoice.CreateSample();
        var report = new Report
        {
            DataContext = invoice,
            PageFormat = new PageFormat { Size = PageSize.A4, Orientation = Orientation.Portrait }
        };

        var companyBlock = new Grid
        {
            ColumnDefinitions = ParseColumns("*, *"),
            RowDefinitions = ParseRows("Auto, Auto"),
            RowSpacing = 10
        };
        companyBlock.AddElement(BuildCompanyNameAddress(invoice.Company), 0, 0);
        companyBlock.AddElement(BuildCompanyContacts(invoice.Company), 0, 1);

        var headerGrid = new Grid
        {
            RowDefinitions = ParseRows("Auto, Auto, Auto, Auto"),
            RowSpacing = 25
        };
        headerGrid.AddElement(Label("Invoicing", 18, FontStyle.Bold, Alignment.End), 0, 0);
        headerGrid.AddElement(BuildLogo(), 1, 0);
        headerGrid.AddElement(companyBlock, 2, 0);
        headerGrid.AddElement(BuildBillShipDetails(invoice), 3, 0);

        var header = new ReportHeaderBand { Size = new Size(SizeLength.Fill, SizeLength.Auto), Margin = new Thickness(0, 0, 0, 15) };
        header.AddElement(headerGrid);
        report.Bands.Add(header);

        var detail = report.Detail;
        detail.WithData(invoice.Items, (item, row) => row.AddElement(BuildItemRow(item)));
        detail.DetailHeader = BuildItemsHeader();
        detail.DetailFooter = BuildItemsFooter(invoice);

        report.Bands.Add(BuildPageFooter(report));
        return report.Build();
    }

    private static StackPanel BuildLogo()
    {
        var stack = new StackPanel();
        stack.AddElement(new Text
        {
            Content = "replace with",
            Font = Kanit(18),
            HorizontalAlignment = Alignment.Start,
            Margin = new Thickness(0, -5)
        });
        stack.AddElement(new Text
        {
            Content = "LOGO",
            Font = Kanit(48, FontStyle.Bold),
            HorizontalAlignment = Alignment.Start
        });
        return stack;
    }

    private static Grid BuildCompanyNameAddress(Organization company)
    {
        var grid = new Grid
        {
            Size = new Size(200, SizeLength.Auto),
            HorizontalAlignment = Alignment.Start,
            RowDefinitions = ParseRows("Auto, Auto"),
            RowSpacing = 5
        };
        grid.AddElement(Label(company.Name, 16, FontStyle.Bold), 0, 0);
        var address = Label(company.Address, 14);
        address.TextTrimming = TextTrimming.WordWrap;
        grid.AddElement(address, 1, 0);
        return grid;
    }

    private static Grid BuildCompanyContacts(Organization company)
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Auto, SizeLength.Auto),
            HorizontalAlignment = Alignment.End,
            ColumnDefinitions = ParseColumns("Auto, Auto"),
            RowDefinitions = ParseRows("Auto, Auto, Auto"),
            ColumnSpacing = 10,
            RowSpacing = 5
        };
        grid.AddElement(Label("Phone:", 14, FontStyle.Bold), 0, 0);
        grid.AddElement(Label(company.Phone ?? string.Empty, 14), 0, 1);
        grid.AddElement(Label("Email:", 14, FontStyle.Bold), 1, 0);
        grid.AddElement(Label(company.Email ?? string.Empty, 14), 1, 1);
        grid.AddElement(Label("Web:", 14, FontStyle.Bold), 2, 0);
        grid.AddElement(Label(company.Website ?? string.Empty, 14), 2, 1);
        return grid;
    }

    private static Grid BuildBillShipDetails(Invoice invoice)
    {
        var grid = new Grid
        {
            Padding = new Thickness(15),
            BackgroundColor = Color.FromHex("#EEEEEE"),
            ColumnDefinitions = ParseColumns("150, 150, *"),
            ColumnSpacing = 5
        };
        grid.AddElement(AddressColumn("Bill to:", invoice.BillTo.Name, invoice.BillTo.Address), 0, 0);
        grid.AddElement(AddressColumn("Ship to:", invoice.ShipTo.Name, invoice.ShipTo.Address), 0, 1);
        grid.AddElement(BuildDetails(invoice), 0, 2);
        return grid;
    }

    private static StackPanel AddressColumn(string title, string name, string address)
    {
        var stack = new StackPanel();
        stack.AddElement(Label(title, 14, FontStyle.Bold));
        stack.AddElement(Label(name, 14));
        var addressText = Label(address, 14);
        addressText.TextTrimming = TextTrimming.WordWrap;
        stack.AddElement(addressText);
        return stack;
    }

    private static Grid BuildDetails(Invoice invoice)
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Auto, SizeLength.Auto),
            HorizontalAlignment = Alignment.End,
            ColumnDefinitions = ParseColumns("Auto, *"),
            RowDefinitions = ParseRows("Auto, Auto, Auto, Auto"),
            ColumnSpacing = 5
        };
        grid.AddElement(Label("Details:", 14, FontStyle.Bold), 0, 0);
        grid.AddElement(Label("Number", 14, FontStyle.Bold), 1, 0);
        var number = Label(invoice.Number, 14);
        number.TextTrimming = TextTrimming.WordWrap;
        grid.AddElement(number, 1, 1);
        grid.AddElement(Label("Date", 14, FontStyle.Bold), 2, 0);
        var date = Label(invoice.Date, 14);
        date.TextTrimming = TextTrimming.WordWrap;
        grid.AddElement(date, 2, 1);
        return grid;
    }

    private static Grid BuildItemsHeader()
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Fill, 40),
            Margin = new Thickness(0, 0, 0, 5),
            BorderColor = Color.FromHex("#CCCCCC"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnDefinitions = ParseColumns("30, *, 80, 80, 80")
        };
        grid.AddElement(HeaderCell("#", Alignment.Start), 0, 0);
        grid.AddElement(HeaderCell("Product", Alignment.Start), 0, 1);
        grid.AddElement(HeaderCell("Quantity", Alignment.Center), 0, 2);
        grid.AddElement(HeaderCell("Unit Price", Alignment.End), 0, 3);
        grid.AddElement(HeaderCell("Total", Alignment.End), 0, 4);
        return grid;
    }

    private static Grid BuildItemRow(InvoiceItem item)
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            MinHeight = 30,
            BorderColor = Color.FromHex("#CCCCCC"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnDefinitions = ParseColumns("30, *, 80, 80, 80")
        };
        grid.AddElement(ItemCell(item.Index.ToString(), Alignment.Start), 0, 0);
        var product = ItemCell(item.Product, Alignment.Start);
        product.Size = new Size(SizeLength.Fill, SizeLength.Auto);
        product.VerticalTextAlignment = TextAlignment.Center;
        grid.AddElement(product, 0, 1);
        grid.AddElement(ItemCell(item.Quantity.ToString(), Alignment.Center), 0, 2);
        grid.AddElement(ItemCell(item.UnitPrice.ToString(CultureInfo.InvariantCulture), Alignment.End), 0, 3);
        grid.AddElement(ItemCell(item.Total.ToString(CultureInfo.InvariantCulture), Alignment.End), 0, 4);
        return grid;
    }

    private static Grid BuildItemsFooter(Invoice invoice)
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Fill, 35),
            BackgroundColor = Color.FromHex("#EEEEEE"),
            ColumnDefinitions = ParseColumns("*, Auto"),
            ColumnSpacing = 10
        };
        grid.AddElement(Label("Total", 14, FontStyle.Bold, Alignment.End, Alignment.Center), 0, 0);
        var total = Label(invoice.Total.ToString(CultureInfo.InvariantCulture), 14, FontStyle.Bold);
        total.HorizontalAlignment = Alignment.End;
        total.VerticalAlignment = Alignment.Center;
        grid.AddElement(total, 0, 1);
        return grid;
    }

    private static PageFooterBand BuildPageFooter(Report report)
    {
        var pageNumber = new Text { Font = Kanit(10) };
        var pageCount = new Text { Font = Kanit(10) };
        report.WithPageChanged((number, count) =>
        {
            pageNumber.Content = number.ToString();
            pageCount.Content = count.ToString();
        });

        var stack = new StackPanel
        {
            Orientation = StackOrientation.Horizontal,
            Size = Size.Auto,
            HorizontalAlignment = Alignment.End,
            VerticalAlignment = Alignment.Center
        };
        stack.AddElements(
        [
            new Text { Content = "Page ", Font = Kanit(10) },
            pageNumber,
            new Text { Content = " of ", Font = Kanit(10), Padding = new Thickness(3, 0) },
            pageCount
        ]);

        var footer = new PageFooterBand
        {
            Size = new Size(SizeLength.Fill, 30),
            Margin = new Thickness(-40, 0, -40, -30),
            Padding = new Thickness(25, 0)
        };
        footer.AddElement(stack);
        return footer;
    }

    private static Text HeaderCell(string content, Alignment horizontal) =>
        new()
        {
            Content = content,
            Font = Kanit(14, FontStyle.Bold),
            HorizontalAlignment = horizontal,
            VerticalAlignment = Alignment.Center
        };

    private static Text ItemCell(string content, Alignment horizontal)
    {
        var text = Label(content, 14);
        text.HorizontalAlignment = horizontal;
        text.VerticalAlignment = Alignment.Center;
        return text;
    }

    private static Text Label(
        string content,
        float size,
        FontStyle style = FontStyle.Normal,
        Alignment horizontal = Alignment.Start,
        Alignment vertical = Alignment.Start) =>
        new()
        {
            Content = content,
            Font = Kanit(size, style),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };

    private static Font Kanit(float size, FontStyle style = FontStyle.Normal) =>
        new("Kanit", size, Colors.Black, style);

    private static List<ColumnDefinition> ParseColumns(string definitions) =>
        definitions.Split(',').Select(s => new ColumnDefinition(GridLength.Parse(s))).ToList();

    private static List<RowDefinition> ParseRows(string definitions) =>
        definitions.Split(',').Select(s => new RowDefinition(GridLength.Parse(s))).ToList();
}
