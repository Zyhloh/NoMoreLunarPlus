namespace NoMoreLunarPlus.Patching;

internal sealed record PatchState(bool RemoveUpsells, bool BlockUpdates)
{
    public static PatchState Stock { get; } = new(false, false);

    public bool IsPatched => RemoveUpsells || BlockUpdates;

    public static PatchState From(PatchMarker? marker)
    {
        if (marker is null)
        {
            return Stock;
        }

        var blocksUpdates = marker.Patches.Contains(PatchCatalog.BlockUpdatesId);
        var removesUpsells = marker.Patches.Count == 0 || marker.Patches.Any(id => id != PatchCatalog.BlockUpdatesId);

        return new PatchState(removesUpsells, blocksUpdates);
    }
}
