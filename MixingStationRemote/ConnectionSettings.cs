using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MixingStationRemote;

public class ConnectionSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string StationUrl { get; set; } = "http://localhost:8080";
    public List<SavedMixer> SavedMixers { get; set; } = new();
    public Guid? AutoConnectMixerId { get; set; }

    // Kept only to migrate settings written by the previous "last mixer" implementation.
    public bool AutoConnectToLastMixer { get; set; }
    public RecentMixer? LastMixer { get; set; }

    private static string SettingsPath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MixingStationRemote");

            return Path.Combine(directory, "connection-settings.json");
        }
    }

    public static ConnectionSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new ConnectionSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<ConnectionSettings>(json) ?? new ConnectionSettings();
            settings.MigrateRecentMixer();
            return settings;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load connection settings: {ex.Message}");
            return new ConnectionSettings();
        }
    }

    public void Save()
    {
        try
        {
            LastMixer = null;
            AutoConnectToLastMixer = false;

            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save connection settings: {ex.Message}");
        }
    }

    public SavedMixer AddOrUpdate(MixerDevice device, ConsoleGroup console)
    {
        var existing = SavedMixers.FirstOrDefault(m =>
            m.ConsoleId == console.consoleId &&
            string.Equals(m.Ip, device.ip, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            existing = new SavedMixer { Id = Guid.NewGuid() };
            SavedMixers.Add(existing);
        }

        existing.ConsoleId = console.consoleId;
        existing.Ip = device.ip;
        existing.Name = device.name;
        existing.Model = device.model;
        existing.Version = device.version;
        existing.ConsoleName = console.name;
        existing.Manufacturer = console.manufacturer;
        existing.LastConnectedAt = DateTimeOffset.Now;
        return existing;
    }

    private void MigrateRecentMixer()
    {
        if (LastMixer == null || SavedMixers.Count > 0)
            return;

        var saved = new SavedMixer
        {
            Id = Guid.NewGuid(),
            ConsoleId = LastMixer.ConsoleId,
            Ip = LastMixer.Ip,
            Name = LastMixer.Name,
            Model = LastMixer.Model,
            Version = LastMixer.Version,
            ConsoleName = LastMixer.ConsoleName,
            Manufacturer = LastMixer.Manufacturer
        };

        SavedMixers.Add(saved);
        if (AutoConnectToLastMixer)
            AutoConnectMixerId = saved.Id;
    }
}

public class SavedMixer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ConsoleId { get; set; }
    public string Ip { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ConsoleName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public DateTimeOffset? LastConnectedAt { get; set; }

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Name) ? Ip : Name;
            var model = string.IsNullOrWhiteSpace(Model) ? ConsoleName : Model;
            return string.IsNullOrWhiteSpace(model) ? $"{name} ({Ip})" : $"{name} - {model} ({Ip})";
        }
    }

    public override string ToString() => DisplayName;
}

public class RecentMixer
{
    public int ConsoleId { get; set; }
    public string Ip { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string ConsoleName { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
}
