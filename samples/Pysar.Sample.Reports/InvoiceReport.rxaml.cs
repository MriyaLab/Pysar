using Pysar.Core.Abstractions;

namespace Pysar.Sample.Reports;

public partial class InvoiceReport
{
    public InvoiceReport(Invoice data)
    {
        InitializeComponent();
        DataContext = data;
    }

    public InvoiceReport()
    {
        InitializeComponent();
    }
}

public sealed record InvoiceItem(int Index, string Product, decimal UnitPrice, int Quantity, decimal Discount)
{
    public decimal Total => UnitPrice * Quantity - Discount;
}

public sealed record Customer(string Company, string Name, string Address, string Phone, string Email);

public sealed record Payment(string Account, string Bank, string Swift, string Options);

public sealed record Invoice(
    string Number,
    string Date,
    Customer Customer,
    IReadOnlyList<InvoiceItem> Items,
    Payment Payment) : IDesignTimeCreatable<Invoice>
{
    public decimal SubTotal => Items.Sum(i => i.UnitPrice * i.Quantity);
    public decimal DiscountTotal => Items.Sum(i => i.Discount);
    public decimal GrandTotal => Items.Sum(i => i.Total);

    /// <summary>The sample invoice used by both the console application and design-time preview.</summary>
    public static Invoice CreateDesignInstance() => new(
        "10643", "03/29/26",
        new Customer("Alfreds Futterkiste", "Maria Anders",
            "Obere Str. 57, Berlin, Germany, 12209", "030-0074321", "alfredsfutterkiste@mail.com"),
        InvoiceData.InvoiceItem.ToArray(),
        new Payment("123-45-6789", "1st Enterprise Bank", "SWFTKUS6LXXX", "Visa, MasterCard, American Express"));
}
