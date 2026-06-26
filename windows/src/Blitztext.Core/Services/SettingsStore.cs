using System.Text.Json;
using System.Text.Json.Serialization;
using Blitztext.Core.Models;

namespace Blitztext.Core.Services;

/// <summary>
/// Loads and saves the <see cref="SettingsContainer"/> as JSON. Mirrors the macOS
/// settings persistence (a JSON file under the app-support folder). Missing or corrupt
/// files fall back to defaults.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _filePath;

    public SettingsStore(string filePath) => _filePath = filePath;

    public string FilePath => _filePath;

    public SettingsContainer Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new SettingsContainer();
            var json = File.ReadAllText(_filePath);
            return Deserialize(json) ?? new SettingsContainer();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new SettingsContainer();
        }
    }

    public void Save(SettingsContainer container)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, Serialize(container));
    }

    // Exposed for tests (round-trip without touching disk).
    public static string Serialize(SettingsContainer container) =>
        JsonSerializer.Serialize(container, Options);

    public static SettingsContainer? Deserialize(string json) =>
        JsonSerializer.Deserialize<SettingsContainer>(json, Options);
}
