using Microsoft.Extensions.Hosting;

namespace Softsphere.PrintBridge;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        using var singleInstance = new Mutex(true, @"Local\Softsphere.PrintBridge", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Softsphere Print Bridge is already running.");
            return;
        }

        ApplicationConfiguration.Initialize();
        var settingsStore = new SettingsStore();
        var printerService = new PrinterService(settingsStore);
        StartupManager.Apply(settingsStore.Current.StartWithWindows);

        try
        {
            await using var webHost = BridgeWebHost.Build(settingsStore, printerService);
            await webHost.StartAsync();
            Application.Run(new TrayApplicationContext(settingsStore, printerService));
            await webHost.StopAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Cannot start the HTTPS print bridge on port {settingsStore.Current.Port}.\n\n{exception}",
                "Softsphere Print Bridge",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
