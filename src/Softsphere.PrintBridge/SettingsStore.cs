using System.Text.Json;

namespace Softsphere.PrintBridge;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();

    public string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Softsphere",
        "PrintBridge"
    );

    public string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");
    public bool IsFirstRun { get; }
    public BridgeSettings Current { get; private set; }

    public SettingsStore()
    {
        Directory.CreateDirectory(SettingsDirectory);
        IsFirstRun = !File.Exists(SettingsPath);
        Current = Load();
        Save(Current);
    }

    public BridgeSettings Load()
    {
        lock (_sync)
        {
            if (!File.Exists(SettingsPath))
            {
                return new BridgeSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(SettingsPath))
                    ?? new BridgeSettings();
            }
            catch
            {
                return new BridgeSettings();
            }
        }
    }

    public void Save(BridgeSettings settings)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
            Current = settings;
        }
    }
}
