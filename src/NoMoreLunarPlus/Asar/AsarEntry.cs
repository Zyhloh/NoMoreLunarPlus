using System.Globalization;
using System.Text.Json.Nodes;

namespace NoMoreLunarPlus.Asar;

internal sealed record AsarEntry(string Path, JsonObject Node)
{
    public bool IsUnpacked => Node["unpacked"] is JsonValue value && value.TryGetValue<bool>(out var unpacked) && unpacked;

    public bool IsLink => Node.ContainsKey("link");

    public bool IsPacked => !IsUnpacked && !IsLink && Node.ContainsKey("offset");

    public long Offset => long.Parse(Node["offset"]!.GetValue<string>(), CultureInfo.InvariantCulture);

    public long Size => Node["size"]?.GetValue<long>() ?? 0;
}
