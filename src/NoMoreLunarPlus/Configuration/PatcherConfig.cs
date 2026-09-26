using System.Text.Json;
using NoMoreLunarPlus.Patching;

namespace NoMoreLunarPlus.Configuration;

internal sealed class PatcherConfig
{
    public string InstallPath { get; set; } = string.Empty;

    public bool PauseOnExit { get; set; } = true;

    public Dictionary<string, bool> Features { get; set; } = Patching.Features.All.ToDictionary(feature => feature, _ => true);

    public bool IsEnabled(string feature) =>
        !Features.Any(pair => string.Equals(pair.Key, feature, StringComparison.OrdinalIgnoreCase) && !pair.Value);

    public static PatcherConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            var created = new PatcherConfig();
            created.Save(path);
            return created;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), PatcherJsonContext.Default.PatcherConfig) ?? new PatcherConfig();
        }
        catch (JsonException exception)
        {
            throw new PatcherException($"config.json is not valid JSON ({exception.Message}). Fix it or delete it to regenerate.");
        }
    }

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, PatcherJsonContext.Default.PatcherConfig));
}
