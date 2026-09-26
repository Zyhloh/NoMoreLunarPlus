namespace NoMoreLunarPlus.Patching;

internal sealed record PatchState(bool RemoveUpsells, bool RemoveTelemetry, bool BlockUpdates)
{
    public static PatchState Stock { get; } = new(false, false, false);

    public bool IsPatched => RemoveUpsells || RemoveTelemetry || BlockUpdates;

    public static PatchState From(PatchMarker? marker)
    {
        if (marker is null)
        {
            return Stock;
        }

        var applied = marker.Patches.ToHashSet(StringComparer.Ordinal);

        return new PatchState(
            applied.Count == 0 || PatchCatalog.Removals.Any(patch => applied.Contains(patch.Id)),
            PatchCatalog.Telemetry.Any(patch => applied.Contains(patch.Id)),
            applied.Contains(PatchCatalog.BlockUpdatesId));
    }
}
