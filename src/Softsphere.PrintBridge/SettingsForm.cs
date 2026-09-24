namespace Softsphere.PrintBridge;

public sealed class SettingsForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly PrinterService _printerService;
    private readonly ComboBox _ticketPrinter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _drawerPrinter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown _port = new() { Minimum = 1024, Maximum = 65535, Dock = DockStyle.Left, Width = 120 };
    private readonly TextBox _apiKey = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _openWithTicket = new() { Text = "Open the cash drawer with every ticket", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };

    public SettingsForm(SettingsStore settingsStore, PrinterService printerService)
    {
        _settingsStore = settingsStore;
        _printerService = printerService;
        Text = "Softsphere Print Bridge";
        Width = 680;
        Height = 430;
        MinimumSize = new Size(620, 390);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BuildLayout();
        LoadSettings();
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ColumnCount = 2,
            RowCount = 8,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(layout, 0, "Ticket printer", _ticketPrinter);
        AddRow(layout, 1, "Cash drawer printer", _drawerPrinter);
        AddRow(layout, 2, "HTTP port", _port);
        AddRow(layout, 3, "API key", _apiKey);
        layout.Controls.Add(_openWithTicket, 1, 4);
        layout.Controls.Add(_startWithWindows, 1, 5);

        var hint = new Label
        {
            AutoSize = true,
            ForeColor = Color.DimGray,
            Text = "Odoo URL: https://127.0.0.1:PORT  |  API header: X-API-Key",
        };
        layout.Controls.Add(hint, 0, 6);
        layout.SetColumnSpan(hint, 2);

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var save = new Button { Text = "Save", AutoSize = true };
        var testTicket = new Button { Text = "Test ticket", AutoSize = true };
        var testDrawer = new Button { Text = "Open drawer", AutoSize = true };
        var regenerate = new Button { Text = "New API key", AutoSize = true };
        save.Click += (_, _) => SaveSettings();
        testTicket.Click += (_, _) => RunTestTicket();
        testDrawer.Click += (_, _) => ShowResult(_printerService.OpenCashDrawer());
        regenerate.Click += (_, _) => _apiKey.Text = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        buttons.Controls.AddRange([save, testDrawer, testTicket, regenerate]);
        layout.Controls.Add(buttons, 0, 7);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        layout.Controls.Add(control, 1, row);
    }

    private void LoadSettings()
    {
        var printers = _printerService.GetInstalledPrinters();
        _ticketPrinter.Items.AddRange(printers.Cast<object>().ToArray());
        _drawerPrinter.Items.Add(string.Empty);
        _drawerPrinter.Items.AddRange(printers.Cast<object>().ToArray());
        var settings = _settingsStore.Current;
        SelectValue(_ticketPrinter, settings.TicketPrinter);
        SelectValue(_drawerPrinter, settings.CashDrawerPrinter);
        _port.Value = settings.Port;
        _apiKey.Text = settings.ApiKey;
        _openWithTicket.Checked = settings.OpenDrawerWithTicketByDefault;
        _startWithWindows.Checked = settings.StartWithWindows;
    }

    private static void SelectValue(ComboBox comboBox, string value)
    {
        var index = comboBox.FindStringExact(value ?? string.Empty);
        comboBox.SelectedIndex = index >= 0 ? index : (comboBox.Items.Count > 0 ? 0 : -1);
    }

    private void SaveSettings()
    {
        var previousPort = _settingsStore.Current.Port;
        var settings = new BridgeSettings
        {
            TicketPrinter = _ticketPrinter.SelectedItem?.ToString() ?? string.Empty,
            CashDrawerPrinter = _drawerPrinter.SelectedItem?.ToString() ?? string.Empty,
            Port = decimal.ToInt32(_port.Value),
            ApiKey = _apiKey.Text.Trim(),
            OpenDrawerWithTicketByDefault = _openWithTicket.Checked,
            StartWithWindows = _startWithWindows.Checked,
        };
        _settingsStore.Save(settings);
        StartupManager.Apply(settings.StartWithWindows);
        var restartNote = previousPort == settings.Port ? string.Empty : " Restart the bridge to apply the new port.";
        MessageBox.Show($"Configuration saved.{restartNote}", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RunTestTicket()
    {
        SaveSettings();
        var ticket = new TicketDocument(
            "Verdé Shop",
            "Ticket Commande Client",
            "TEST/0001",
            "Client passager",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            [new TicketLine("PRODUIT DE TEST 15G", "1.000", "8.320", "9.901")],
            "8.320",
            "1.581",
            "9.901",
            "DT",
            "Espèces",
            "9.901",
            "9.901",
            "0.000",
            true
        );
        ShowResult(_printerService.PrintTicket(new PrintTicketRequest(
            OpenCashDrawer: false,
            Ticket: ticket
        )));
    }

    private void ShowResult(PrintOperationResult result) => MessageBox.Show(
        result.Message,
        Text,
        MessageBoxButtons.OK,
        result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error
    );
}
