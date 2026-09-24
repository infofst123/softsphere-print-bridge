using System.Security.Cryptography;

namespace Softsphere.PrintBridge;

public sealed class BridgeSettings
{
    public int Port { get; set; } = 17833;
    public string ApiKey { get; set; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    public string TicketPrinter { get; set; } = string.Empty;
    public string CashDrawerPrinter { get; set; } = string.Empty;
    public bool OpenDrawerWithTicketByDefault { get; set; }
    public bool StartWithWindows { get; set; } = true;
}

public sealed record PrintTicketRequest(
    string Text = "",
    bool OpenCashDrawer = false,
    TicketDocument? Ticket = null
);

public sealed record TicketDocument(
    string CompanyName,
    string Title,
    string OrderName,
    string PartnerName,
    string Date,
    IReadOnlyList<TicketLine> Lines,
    string AmountUntaxed,
    string AmountTax,
    string AmountTotal,
    string Currency,
    string PaymentMethod,
    string PaymentAmount,
    string AmountReceived,
    string AmountReturned,
    bool PaymentIsCash
);

public sealed record TicketLine(string Name, string Quantity, string UnitPrice, string Total);
public sealed record PrintOperationResult(bool Success, string Message);
