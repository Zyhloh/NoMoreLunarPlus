using System.Text.Json;
using NoMoreLunarPlus.Asar;
using NoMoreLunarPlus.Configuration;

namespace NoMoreLunarPlus.Patching;

internal sealed class PatchMarker
{
    public const string EntryPath = ".nomorelunarplus";

    public string PatcherVersion { get; set; } = string.Empty;

    public string LunarVersion { get; set; } = string.Empty;

    public List<string> Patches { get; set; } = [];

    public DateTimeOffset PatchedAt { get; set; }

    public static PatchMarker Create(string lunarVersion, PatchRun run) => new()
    {
        PatcherVersion = AppInfo.Version,
        LunarVersion = lunarVersion,
        Patches = run.Results
            .Where(result => result.Outcome == PatchOutcome.Applied)
            .Select(result => result.Patch.Id)
            .ToList(),
        PatchedAt = DateTimeOffset.Now
    };

    public static PatchMarker? Read(AsarArchive archive)
    {
        if (!archive.Contains(EntryPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(archive.ReadFile(EntryPath), PatcherJsonContext.Default.PatchMarker) ?? new PatchMarker();
        }
        catch (JsonException)
        {
            return new PatchMarker();
        }
    }

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, PatcherJsonContext.Default.PatchMarker);
}
