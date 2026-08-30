using Pysar.Core.Abstractions;

namespace Pysar.Sample.Reports;

public sealed record RevenueReportData(
    IReadOnlyList<RevenueCustomer> Customers,
    decimal TotalRevenue,
    int OrderCount,
    decimal AverageSaleAmount) : IDesignTimeCreatable<RevenueReportData>
{
    public static RevenueReportData CreateDesignInstance() => RevenueData.CreateSample();
}

public sealed record RevenueCustomer(
    string Company,
    string City,
    string State,
    int Orders,
    decimal AverageSaleAmount,
    IReadOnlyList<RevenueOrder> OrderItems)
{
    public decimal GrandTotal => OrderItems.Sum(order => order.SaleAmount);
}

public sealed record RevenueOrder(
    DateTime OrderDate,
    string InvoiceNumber,
    string Salesperson,
    string DeliveryStatus,
    decimal SaleAmount)
{
    public string OrderDateLabel => OrderDate.ToString("M/d/yyyy");

    public string StatusIcon => DeliveryStatus switch
    {
        "Delivered" => "",
        "Call" => "",
        "Document" => "",
        _ => DeliveryStatus,
    };
}

public static class RevenueData
{
    public static RevenueReportData CreateSample()
    {
        RevenueCustomer[] customers =
        [
            Customer("Super Mart of the West", "Bentonville", "Arkansas",
            [
                Order(2024, 6, 30, "81042", "Jim Packard", "Delivered", 8300m),
                Order(2024, 6, 29, "84744", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 10, "85028", "Clark Morgan", "Call", 2625m),
                Order(2024, 7, 17, "86497", "Todd Hoffman", "Delivered", 9650m),
                Order(2024, 7, 22, "88027", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("Electronics Depot", "Atlanta", "Georgia",
            [
                Order(2024, 6, 28, "239068", "Jim Packard", "Delivered", 8300m),
                Order(2024, 6, 30, "239071", "Harv Mudd", "Call", 4750m),
                Order(2024, 7, 11, "239074", "Clark Morgan", "Delivered", 2625m),
            ]),
            Customer("K&S Music", "Minneapolis", "Minnesota",
            [
                Order(2024, 7, 2, "239776", "Jim Packard", "Call", 8300m),
                Order(2024, 6, 30, "239779", "Harv Mudd", "Document", 4750m),
                Order(2024, 7, 14, "239785", "Todd Hoffman", "Delivered", 9650m),
            ]),
            Customer("Tom's Club", "Issaquah", "Washington",
            [
                Order(2024, 7, 1, "240484", "Jim Packard", "Delivered", 8300m),
                Order(2024, 6, 30, "240487", "Harv Mudd", "Document", 4750m),
                Order(2024, 7, 24, "240496", "Todd Hoffman", "Delivered", 14400m),
                Order(2024, 7, 22, "240499", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("E-Mart", "Hoffman Estates", "Illinois",
            [
                Order(2024, 6, 29, "241192", "Jim Packard", "Call", 8300m),
                Order(2024, 6, 30, "241195", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 22, "241204", "Todd Hoffman", "Delivered", 14400m),
            ]),
            Customer("Walters", "Deerfield", "Illinois",
            [
                Order(2024, 6, 29, "241900", "Jim Packard", "Delivered", 8300m),
                Order(2024, 7, 2, "241903", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 7, "241906", "Clark Morgan", "Delivered", 2625m),
                Order(2024, 7, 15, "241909", "Todd Hoffman", "Call", 9650m),
                Order(2024, 7, 24, "241912", "Todd Hoffman", "Call", 14400m),
                Order(2024, 7, 23, "241915", "Clark Morgan", "Document", 14050m),
            ]),
            Customer("StereoShack", "Fort Worth", "Texas",
            [
                Order(2024, 7, 2, "242608", "Jim Packard", "Delivered", 8300m),
                Order(2024, 7, 4, "242611", "Harv Mudd", "Document", 4750m),
                Order(2024, 7, 12, "242614", "Clark Morgan", "Call", 2625m),
                Order(2024, 7, 17, "242617", "Todd Hoffman", "Call", 9650m),
                Order(2024, 7, 26, "242623", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("Circuit Town", "Oak Brook", "Illinois",
            [
                Order(2024, 7, 2, "243316", "Jim Packard", "Delivered", 8300m),
                Order(2024, 6, 29, "243319", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 16, "243325", "Todd Hoffman", "Delivered", 9650m),
                Order(2024, 7, 23, "243328", "Todd Hoffman", "Delivered", 14400m),
                Order(2024, 7, 22, "243331", "Clark Morgan", "Call", 14050m),
            ]),
            Customer("Premier Buy", "Richfield", "Minnesota",
            [
                Order(2024, 6, 30, "244024", "Jim Packard", "Delivered", 8300m),
                Order(2024, 7, 15, "244033", "Todd Hoffman", "Call", 9650m),
                Order(2024, 7, 24, "244036", "Todd Hoffman", "Call", 14400m),
                Order(2024, 7, 24, "244039", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("ElectrixMax", "Naperville", "Illinois",
            [
                Order(2024, 7, 3, "244732", "Jim Packard", "Call", 14100m),
                Order(2024, 7, 4, "244735", "Harv Mudd", "Document", 5125m),
                Order(2024, 7, 11, "244738", "Clark Morgan", "Delivered", 2735m),
                Order(2024, 7, 17, "244741", "Todd Hoffman", "Delivered", 13250m),
                Order(2024, 7, 23, "244744", "Todd Hoffman", "Call", 14505m),
                Order(2024, 7, 24, "244747", "Clark Morgan", "Delivered", 17200m),
            ]),
            Customer("Video Emporium", "Dallas", "Texas",
            [
                Order(2024, 7, 1, "245440", "Jim Packard", "Call", 8300m),
                Order(2024, 7, 1, "245443", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 9, "245446", "Clark Morgan", "Delivered", 2625m),
                Order(2024, 7, 15, "245449", "Todd Hoffman", "Call", 9650m),
                Order(2024, 7, 21, "245452", "Todd Hoffman", "Delivered", 14400m),
                Order(2024, 7, 26, "245455", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("Screen Shop", "Mooresville", "North Carolina",
            [
                Order(2024, 7, 1, "246148", "Jim Packard", "Delivered", 8300m),
                Order(2024, 6, 29, "246151", "Harv Mudd", "Call", 4750m),
                Order(2024, 7, 7, "246154", "Clark Morgan", "Delivered", 2625m),
                Order(2024, 7, 17, "246157", "Todd Hoffman", "Delivered", 9650m),
                Order(2024, 7, 21, "246160", "Todd Hoffman", "Delivered", 14400m),
                Order(2024, 7, 24, "246163", "Clark Morgan", "Call", 14050m),
            ]),
            Customer("Braeburn", "Cupertino", "California",
            [
                Order(2024, 6, 30, "246856", "Jim Packard", "Delivered", 14100m),
                Order(2024, 6, 30, "246859", "Harv Mudd", "Call", 5350m),
                Order(2024, 7, 9, "246862", "Clark Morgan", "Delivered", 2985m),
                Order(2024, 7, 15, "246865", "Todd Hoffman", "Delivered", 11450m),
                Order(2024, 7, 22, "246868", "Todd Hoffman", "Delivered", 14715m),
                Order(2024, 7, 23, "246871", "Clark Morgan", "Delivered", 16250m),
            ]),
            Customer("PriceCo", "Camp Hill", "Pennsylvania",
            [
                Order(2024, 7, 3, "247564", "Jim Packard", "Delivered", 8300m),
                Order(2024, 7, 3, "247567", "Harv Mudd", "Call", 4750m),
                Order(2024, 7, 11, "247570", "Clark Morgan", "Call", 2625m),
                Order(2024, 7, 16, "247573", "Todd Hoffman", "Call", 9650m),
                Order(2024, 7, 25, "247576", "Todd Hoffman", "Call", 14400m),
                Order(2024, 7, 22, "247579", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("Ultimate Gadget", "Warner Robbins", "Georgia",
            [
                Order(2024, 6, 28, "248272", "Jim Packard", "Delivered", 7475m),
                Order(2024, 7, 3, "248275", "Harv Mudd", "Delivered", 4625m),
                Order(2024, 7, 7, "248278", "Clark Morgan", "Delivered", 2385m),
                Order(2024, 7, 14, "248281", "Todd Hoffman", "Delivered", 4250m),
                Order(2024, 7, 22, "248284", "Todd Hoffman", "Delivered", 11200m),
                Order(2024, 7, 24, "248287", "Clark Morgan", "Delivered", 11850m),
            ]),
            Customer("EZ Stop", "Arcadia", "California",
            [
                Order(2024, 6, 29, "248980", "Jim Packard", "Call", 8300m),
                Order(2024, 7, 2, "248983", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 8, "248986", "Clark Morgan", "Delivered", 2625m),
                Order(2024, 7, 19, "248989", "Todd Hoffman", "Delivered", 9650m),
                Order(2024, 7, 22, "248992", "Todd Hoffman", "Delivered", 14400m),
                Order(2024, 7, 27, "248995", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("Clicker", "Compton", "California",
            [
                Order(2024, 6, 29, "249688", "Jim Packard", "Delivered", 11200m),
                Order(2024, 7, 4, "249691", "Harv Mudd", "Delivered", 5020m),
                Order(2024, 7, 7, "249694", "Clark Morgan", "Delivered", 2855m),
                Order(2024, 7, 19, "249697", "Todd Hoffman", "Delivered", 13250m),
                Order(2024, 7, 24, "249700", "Todd Hoffman", "Delivered", 20200m),
                Order(2024, 7, 25, "249703", "Clark Morgan", "Call", 16250m),
            ]),
            Customer("Store of America", "Seattle", "Washington",
            [
                Order(2024, 6, 28, "250396", "Jim Packard", "Delivered", 8300m),
                Order(2024, 7, 2, "250399", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 11, "250402", "Clark Morgan", "Delivered", 2625m),
                Order(2024, 7, 19, "250405", "Todd Hoffman", "Delivered", 9650m),
                Order(2024, 7, 23, "250408", "Todd Hoffman", "Delivered", 14400m),
                Order(2024, 7, 23, "250411", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("Zone Toys", "Los Angeles", "California",
            [
                Order(2024, 7, 2, "251104", "Jim Packard", "Call", 8300m),
                Order(2024, 7, 2, "251107", "Harv Mudd", "Delivered", 4750m),
                Order(2024, 7, 12, "251110", "Clark Morgan", "Delivered", 2625m),
                Order(2024, 7, 19, "251113", "Todd Hoffman", "Delivered", 9650m),
                Order(2024, 7, 20, "251116", "Todd Hoffman", "Delivered", 14400m),
                Order(2024, 7, 22, "251119", "Clark Morgan", "Delivered", 14050m),
            ]),
            Customer("ACME", "El Segundo", "California",
            [
                Order(2024, 6, 28, "251812", "Jim Packard", "Delivered", 9125m),
                Order(2024, 6, 29, "251815", "Harv Mudd", "Delivered", 5950m),
                Order(2024, 7, 8, "251818", "Clark Morgan", "Document", 2970m),
                Order(2024, 7, 15, "251821", "Todd Hoffman", "Delivered", 10450m),
                Order(2024, 7, 25, "251824", "Todd Hoffman", "Delivered", 23100m),
                Order(2024, 7, 22, "251827", "Clark Morgan", "Delivered", 17350m),
            ]),
        ];

        var allOrders = customers.SelectMany(customer => customer.OrderItems).ToArray();
        var totalRevenue = allOrders.Sum(order => order.SaleAmount);
        var orderCount = allOrders.Length;
        var averageSaleAmount = orderCount == 0 ? 0m : Math.Round(totalRevenue / orderCount, 0);

        return new RevenueReportData(customers, totalRevenue, orderCount, averageSaleAmount);
    }

    private static RevenueCustomer Customer(
        string company,
        string city,
        string state,
        RevenueOrder[] orderItems)
    {
        var total = orderItems.Sum(order => order.SaleAmount);
        var average = orderItems.Length == 0 ? 0m : Math.Round(total / orderItems.Length, 2);
        return new RevenueCustomer(company, city, state, orderItems.Length, average, orderItems);
    }

    private static RevenueOrder Order(
        int year,
        int month,
        int day,
        string invoiceNumber,
        string salesperson,
        string deliveryStatus,
        decimal saleAmount)
        => new(new DateTime(year, month, day), invoiceNumber, salesperson, deliveryStatus, saleAmount);
}
