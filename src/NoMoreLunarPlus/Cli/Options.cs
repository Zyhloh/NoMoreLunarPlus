namespace NoMoreLunarPlus.Cli;

internal enum PatcherAction
{
    Patch,
    Restore,
    Check
}

internal sealed record Options(PatcherAction? Action, string? InstallPath, bool NoPause)
{
    public const string Usage = "Usage: NoMoreLunarPlus [--patch | --restore | --check] [--path <folder>] [--no-pause]";

    public static Options Parse(string[] args)
    {
        PatcherAction? action = null;
        string? installPath = null;
        var noPause = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--patch":
                    action = PatcherAction.Patch;
                    break;
                case "--restore":
                    action = PatcherAction.Restore;
                    break;
                case "--check":
                    action = PatcherAction.Check;
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

        return new Options(action, installPath, noPause);
    }
}
