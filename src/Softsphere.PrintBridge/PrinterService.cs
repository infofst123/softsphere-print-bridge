using System.Drawing.Printing;
using System.Text;

namespace Softsphere.PrintBridge;

public sealed class PrinterService(SettingsStore settingsStore)
{
    private const int TicketWidth = 42;
    private static readonly byte[] Initialize = [0x1B, 0x40];
    private static readonly byte[] CodePage858 = [0x1B, 0x74, 0x13];
    private static readonly byte[] AlignLeft = [0x1B, 0x61, 0x00];
    private static readonly byte[] AlignCenter = [0x1B, 0x61, 0x01];
    private static readonly byte[] BoldOn = [0x1B, 0x45, 0x01];
    private static readonly byte[] BoldOff = [0x1B, 0x45, 0x00];
    private static readonly byte[] NormalSize = [0x1D, 0x21, 0x00];
    private static readonly byte[] ComfortableLineSpacing = [0x1B, 0x33, 0x24];
    private static readonly byte[] Cut = [0x1D, 0x56, 0x00];
    private static readonly Encoding PrinterEncoding;

    static PrinterService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        PrinterEncoding = Encoding.GetEncoding(858);
    }

    public IReadOnlyList<string> GetInstalledPrinters() =>
        PrinterSettings.InstalledPrinters.Cast<string>().OrderBy(name => name).ToArray();

    public PrintOperationResult PrintTicket(PrintTicketRequest request)
    {
        try
        {
            var payload = request.Ticket is null
                ? BuildPlainTicket(request.Text)
                : BuildFormattedTicket(request.Ticket);
            RawPrinter.Send(settingsStore.Current.TicketPrinter, payload, "Softsphere ticket");

            if (request.OpenCashDrawer || settingsStore.Current.OpenDrawerWithTicketByDefault)
            {
                OpenCashDrawer();
            }
            return new(true, "Ticket printed.");
        }
        catch (Exception exception)
        {
            return new(false, exception.Message);
        }
    }

    public PrintOperationResult PrintTicket(string text, bool openCashDrawer) =>
        PrintTicket(new PrintTicketRequest(text, openCashDrawer));

    public PrintOperationResult OpenCashDrawer()
    {
        try
        {
            var settings = settingsStore.Current;
            var printer = string.IsNullOrWhiteSpace(settings.CashDrawerPrinter)
                ? settings.TicketPrinter
                : settings.CashDrawerPrinter;
            RawPrinter.Send(printer, [0x1B, 0x70, 0x00, 0x19, 0xFA], "Open cash drawer");
            return new(true, "Cash drawer opened.");
        }
        catch (Exception exception)
        {
            return new(false, exception.Message);
        }
    }

    private static byte[] BuildFormattedTicket(TicketDocument ticket)
    {
        var output = new List<byte>(2048);
        Command(output, Initialize);
        Command(output, CodePage858);
        Command(output, NormalSize);
        Command(output, ComfortableLineSpacing);

        Command(output, AlignCenter);
        Command(output, BoldOn);
        Line(output, ticket.CompanyName);
        Line(output, ticket.Title);
        Line(output, ticket.OrderName);
        Command(output, BoldOff);

        Separator(output);
        Command(output, AlignLeft);
        LabelValue(output, "Client: ", ticket.PartnerName);
        LabelValue(output, "Date: ", ticket.Date);
        Separator(output);

        Command(output, BoldOn);
        Line(output, Columns("Article", "Qté", "PU", "Total"));
        Command(output, BoldOff);
        foreach (var item in ticket.Lines)
        {
            var nameLines = Wrap(item.Name, 17).ToArray();
            Line(output, Columns(
                nameLines.FirstOrDefault() ?? string.Empty,
                item.Quantity,
                item.UnitPrice,
                item.Total
            ));
            foreach (var continuation in nameLines.Skip(1))
            {
                Line(output, continuation);
            }
        }

        Separator(output);
        RightLabelValue(output, "Total HT: ", $"{ticket.AmountUntaxed} {ticket.Currency}");
        RightLabelValue(output, "TVA: ", $"{ticket.AmountTax} {ticket.Currency}");
        Command(output, BoldOn);
        RightLine(output, $"Total TTC: {ticket.AmountTotal} {ticket.Currency}");
        Command(output, BoldOff);

        if (!string.IsNullOrWhiteSpace(ticket.PaymentMethod))
        {
            Separator(output);
            RightLabelValue(output, "Mode de paiement: ", ticket.PaymentMethod);
            RightLabelValue(output, "Payé: ", $"{ticket.PaymentAmount} {ticket.Currency}");
            if (ticket.PaymentIsCash)
            {
                RightLabelValue(output, "Reçu: ", $"{ticket.AmountReceived} {ticket.Currency}");
                RightLabelValue(output, "Monnaie: ", $"{ticket.AmountReturned} {ticket.Currency}");
            }
        }

        Text(output, "\n\n\n");
        Command(output, Cut);
        return output.ToArray();
    }

    private static byte[] BuildPlainTicket(string text)
    {
        var output = new List<byte>(512);
        Command(output, Initialize);
        Command(output, CodePage858);
        Command(output, NormalSize);
        Command(output, ComfortableLineSpacing);
        Command(output, AlignLeft);
        Text(output, text.Replace("\r\n", "\n"));
        Text(output, "\n\n\n");
        Command(output, Cut);
        return output.ToArray();
    }

    private static string Columns(string name, string quantity, string unitPrice, string total) =>
        Fit(name, 17, false) + Fit(quantity, 7, true) + Fit(unitPrice, 8, true) + Fit(total, 10, true);

    private static string Fit(string value, int width, bool alignRight)
    {
        value = (value ?? string.Empty).Trim();
        if (value.Length > width)
        {
            value = value[..width];
        }
        return alignRight ? value.PadLeft(width) : value.PadRight(width);
    }

    private static IEnumerable<string> Wrap(string value, int width)
    {
        var remaining = (value ?? string.Empty).Trim();
        if (remaining.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }
        while (remaining.Length > width)
        {
            var split = remaining.LastIndexOf(' ', width);
            if (split <= 0)
            {
                split = width;
            }
            yield return remaining[..split].Trim();
            remaining = remaining[split..].TrimStart();
        }
        yield return remaining;
    }

    private static void Separator(List<byte> output) =>
        Text(output, "\n" + new string('-', TicketWidth) + "\n");

    private static void LabelValue(List<byte> output, string label, string value)
    {
        Command(output, BoldOn);
        Text(output, label);
        Command(output, BoldOff);
        Line(output, value);
    }

    private static void RightLabelValue(List<byte> output, string label, string value)
    {
        var contentLength = label.Length + value.Length;
        Text(output, new string(' ', Math.Max(0, TicketWidth - contentLength)));
        Command(output, BoldOn);
        Text(output, label);
        Command(output, BoldOff);
        Line(output, value);
    }

    private static void RightLine(List<byte> output, string value) =>
        Line(output, value.Length >= TicketWidth ? value : value.PadLeft(TicketWidth));

    private static void Line(List<byte> output, string value)
    {
        Text(output, value);
        Text(output, "\n");
    }

    private static void Text(List<byte> output, string value) => output.AddRange(PrinterEncoding.GetBytes(value));
    private static void Command(List<byte> output, byte[] command) => output.AddRange(command);
}
