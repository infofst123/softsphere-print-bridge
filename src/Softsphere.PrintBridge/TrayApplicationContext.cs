namespace Softsphere.PrintBridge;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly SettingsStore _settingsStore;
    private readonly PrinterService _printerService;

    public TrayApplicationContext(SettingsStore settingsStore, PrinterService printerService)
    {
        _settingsStore = settingsStore;
        _printerService = printerService;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Configuration", null, (_, _) => ShowSettings());
        menu.Items.Add("Open cash drawer", null, (_, _) => ShowDrawerResult());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        _notifyIcon = new NotifyIcon
        {
            Text = "Softsphere Print Bridge",
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();

        if (_settingsStore.IsFirstRun || string.IsNullOrWhiteSpace(_settingsStore.Current.TicketPrinter))
        {
            EventHandler? showConfiguration = null;
            showConfiguration = (_, _) =>
            {
                Application.Idle -= showConfiguration;
                ShowSettings();
            };
            Application.Idle += showConfiguration;
        }
    }

    private void ShowSettings()
    {
        using var form = new SettingsForm(_settingsStore, _printerService);
        form.ShowDialog();
    }

    private void ShowDrawerResult()
    {
        var result = _printerService.OpenCashDrawer();
        _notifyIcon.ShowBalloonTip(
            3000,
            "Softsphere Print Bridge",
            result.Message,
            result.Success ? ToolTipIcon.Info : ToolTipIcon.Error
        );
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }
}
