using System.Text.Json;
using System.Text.Json.Serialization;
using NoMoreLunarPlus.Lunar;
using NoMoreLunarPlus.Patching;

namespace NoMoreLunarPlus.Configuration;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(PatcherConfig))]
[JsonSerializable(typeof(PatchMarker))]
[JsonSerializable(typeof(BackupManifest))]
internal sealed partial class PatcherJsonContext : JsonSerializerContext;
