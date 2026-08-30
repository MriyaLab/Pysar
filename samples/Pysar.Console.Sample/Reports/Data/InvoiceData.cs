using Pysar.Core.Abstractions;

namespace Pysar.Console.Sample.Reports.Data;

public sealed record Invoice(
    string Number,
    string Date,
    Organization Company,
    Organization BillTo,
    Organization ShipTo,
    IReadOnlyList<InvoiceItem> Items) : IDesignTimeCreatable<Invoice>
{
    public decimal Total => Items.Sum(i => i.Total);

    public static Invoice CreateDesignInstance() => new(
        "10643", "03/29/26",
        new Organization("You'r Company Name", "Street Address, City, State, Zip", "111 111 1111", "comapany@email.com",
            "www.comapny.com"),
        new Organization("Client Name", "Street Address, City, State, Zip, Some long address", "222 222 2222", null, null),
        new Organization("Client Name", "Street Address, City, State, Zip", "222 222 2222", null, null),
        [
            new InvoiceItem(1, "Premium Wireless Headphones", 45.60m, 5),
            new InvoiceItem(2, "Mechanical Keyboard", 18.00m, 4),
            new InvoiceItem(3, "Wireless Charging Pad", 12.00m, 2),
            new InvoiceItem(4, "USB-C to HDMI Adapter", 2.00m, 12),
            new InvoiceItem(5, "Portable Power Bank, with vary long name and number 1234567890", 15.00m, 1)
        ]);

    public static Invoice CreateSample() => new(
        "INV-2026-0158", "08/20/26",
        new Organization("Nexus IT Solutions", "500 Tech Park Drive, Building C, San Jose, CA 95113", "(408) 555-0172",
            "billing@nexusit.com",
            "www.nexusit.com"),
        new Organization("Pacific Healthcare Group", "2200 Medical Center Blvd, Los Angeles, CA 90027",
            "(310) 555-0891", "finance@pacifichealth.com",
            "www.pacifichealth.com"),
        new Organization("Pacific Healthcare Group", "2200 Medical Center Blvd, Los Angeles, CA 90027",
            "(310) 555-0891", null, null),
        [
            new InvoiceItem(1, "Network Infrastructure Design & Implementation", 275.00m, 12),
            new InvoiceItem(2, "Cisco Meraki Firewall MX-105 - Hardware", 1850.00m, 2),
            new InvoiceItem(3, "24/7 Network Monitoring & Support - Monthly", 1200.00m, 3),
            new InvoiceItem(4, "Data Backup & Disaster Recovery Setup", 450.00m, 4),
            new InvoiceItem(5, "Cloud Storage - 5TB Annual Plan", 600.00m, 1),
            new InvoiceItem(6, "Workstation Deployment (Dell OptiPlex)", 895.00m, 8),
            new InvoiceItem(7, "Microsoft 365 Business Premium - Annual License", 264.00m, 12),
            new InvoiceItem(8, "Cyber Security Vulnerability Assessment", 320.00m, 5),
            new InvoiceItem(9, "Employee Security Awareness Training", 150.00m, 6),
            new InvoiceItem(10, "Server Rack Installation & Cable Management", 380.00m, 3),
            new InvoiceItem(11, "Uninterruptible Power Supply (UPS) - 1500VA", 420.00m, 4),
            new InvoiceItem(12, "Network Switches - 48 Port Gigabit", 540.00m, 3),
            new InvoiceItem(13, "Wireless Access Points - WiFi 6", 320.00m, 6),
            new InvoiceItem(14, "On-Site Technical Support (8hrs/visit)", 175.00m, 8),
            new InvoiceItem(15, "IT Documentation & SOP Creation", 295.00m, 4)
        ]);
}

public sealed record Organization(string Name, string Address, string? Phone, string? Email, string? Website);

public sealed record InvoiceItem(int Index, string Product, decimal UnitPrice, int Quantity)
{
    public decimal Total => UnitPrice * Quantity;
}

