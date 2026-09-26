namespace NoMoreLunarPlus.Cli;

internal sealed record Options(
    bool Patch,
    bool? BlockUpdates,
    bool Restore,
    bool Check,
    string? InstallPath,
    bool NoPause)
{
    public const string Usage =
        "Usage: NoMoreLunarPlus [--patch] [--block-updates | --allow-updates] [--restore] [--check] [--path <folder>] [--no-pause]";

    public bool HasAction => Patch || BlockUpdates.HasValue || Restore || Check;

    public static Options Parse(string[] args)
    {
        var patch = false;
        bool? blockUpdates = null;
        var restore = false;
        var check = false;
        string? installPath = null;
        var noPause = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--patch":
                    patch = true;
                    break;
                case "--block-updates":
                    blockUpdates = true;
                    break;
                case "--allow-updates":
                    blockUpdates = false;
                    break;
                case "--restore":
                    restore = true;
                    break;
                case "--check":
                    check = true;
                    break;
                case "--no-pause":
                    noPause = true;
                    break;
                case "--path" when i + 1 < args.Length:
                    installPath = args[++i];
                    break;
                default:
                    throw new PatcherException($"Unknown argument '{args[i]}'. {Usage}");
            }
        }

        if ((restore || check) && (patch || blockUpdates.HasValue) || restore && check)
        {
            throw new PatcherException($"--restore and --check can't be combined with other actions. {Usage}");
        }

        return new Options(patch, blockUpdates, restore, check, installPath, noPause);
    }
}
