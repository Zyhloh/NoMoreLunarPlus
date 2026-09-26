using System.Security.Principal;
using NoMoreLunarPlus.Asar;
using NoMoreLunarPlus.Cli;
using NoMoreLunarPlus.Configuration;
using NoMoreLunarPlus.Lunar;
using NoMoreLunarPlus.Patching;

namespace NoMoreLunarPlus;

internal sealed class App(PatcherConfig config, string configPath, Options options)
{
    public static int Run(string[] args)
    {
        Terminal.Banner();

        var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        var pause = true;

        try
        {
            var options = Options.Parse(args);
            var config = PatcherConfig.Load(configPath);
            pause = config.PauseOnExit && !options.NoPause;

            EnsureAdministrator();

            var app = new App(config, configPath, options);

            if (options.HasAction)
            {
                app.RunActions();
            }
            else
            {
                app.RunMenu();
                pause = false;
            }

            return 0;
        }
        catch (PatcherException exception)
        {
            Terminal.Error(exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            Terminal.Error($"Unexpected error: {exception.Message}");
            return 1;
        }
        finally
        {
            if (pause)
            {
                Terminal.Pause();
            }
        }
    }

    private void RunActions()
    {
        var install = ResolveInstall();
        var backups = new BackupStore(install);
        var state = ReadState(install);

        PrintSummary(install, state);

        if (options.Check)
        {
            Check(install, backups, state);
        }
        else if (options.Restore)
        {
            Restore(install, backups, state);
        }
        else
        {
            Apply(install, backups, state, new PatchState(
                options.Patch || state.RemoveUpsells,
                options.RemoveTelemetry ?? state.RemoveTelemetry,
                options.BlockUpdates ?? state.BlockUpdates));
        }
    }

    private void RunMenu()
    {
        var install = ResolveInstall();
        var backups = new BackupStore(install);

        while (true)
        {
            var state = ReadState(install);
            PrintSummary(install, state);

            var action = ChooseFromMenu(install, backups, state);

            if (action is null)
            {
                return;
            }

            try
            {
                action();
            }
            catch (PatcherException exception)
            {
                Terminal.Error(exception.Message);
            }

            Terminal.Rule();
        }
    }

    private Action? ChooseFromMenu(LunarInstall install, BackupStore backups, PatchState state)
    {
        var entries = new List<(string Label, Action Run)>
        {
            (state.RemoveUpsells ? "Re-apply ad and upsell removal" : "Remove ads and upsells",
                () => Apply(install, backups, state, state with { RemoveUpsells = true })),
            (state.RemoveTelemetry ? "Restore telemetry and tracking" : "Remove telemetry and tracking",
                () => Apply(install, backups, state, state with { RemoveTelemetry = !state.RemoveTelemetry })),
            (state.BlockUpdates ? "Allow launcher updates" : "Block launcher updates",
                () => ToggleUpdates(install, backups, state))
        };

        if (state.IsPatched)
        {
            entries.Add(("Restore stock launcher", () => Restore(install, backups, state)));
        }

        entries.Add(("Dry run (show which patches match)", () => Check(install, backups, state)));

        var choices = entries
            .Select((entry, index) => ((char)('1' + index), entry.Label))
            .Append(('Q', "Quit"))
            .ToArray();

        var key = Terminal.Choose("What do you want to do?", choices);
        return key == 'Q' ? null : entries[key - '1'].Run;
    }

    private void ToggleUpdates(LunarInstall install, BackupStore backups, PatchState state)
    {
        if (!state.BlockUpdates)
        {
            Terminal.Warning("Lunar Client will stop updating itself until you choose \"Allow launcher updates\".");
            Terminal.Warning("Game versions, mods and assets still update normally.");

            if (Terminal.Choose("Continue?", ('Y', "Yes"), ('N', "No")) != 'Y')
            {
                return;
            }
        }

        Apply(install, backups, state, state with { BlockUpdates = !state.BlockUpdates });
    }

    private void Apply(LunarInstall install, BackupStore backups, PatchState current, PatchState desired)
    {
        if (!desired.IsPatched)
        {
            Restore(install, backups, current);
            return;
        }

        Terminal.Section("Patching");
        CloseLunar(install);

        if (current.IsPatched)
        {
            backups.EnsureUsable();
        }
        else
        {
            Terminal.Step("Backing up stock files");
            backups.Capture();
            Terminal.Done();
        }

        using var archive = AsarArchive.Open(backups.AsarPath);

        Terminal.Step("Applying patches");
        var run = PatchEngine.Run(archive, SelectPatches(desired));
        Terminal.Done($"{run.AppliedCount} applied");

        PrintResults(run);

        if (desired.BlockUpdates && !AnyApplied(run, [PatchCatalog.BlockUpdates]))
        {
            throw new PatcherException("Update blocking isn't supported on this Lunar Client version. Nothing was changed.");
        }

        if (desired.RemoveTelemetry && !AnyApplied(run, PatchCatalog.Telemetry))
        {
            throw new PatcherException("Telemetry removal isn't supported on this Lunar Client version. Nothing was changed.");
        }

        if (run.AppliedCount == 0)
        {
            throw new PatcherException("None of the patches matched this Lunar Client version. Nothing was changed.");
        }

        Install(install, backups, archive, run);

        Terminal.Success($"Lunar Client is patched ({run.AppliedCount}/{run.Results.Count}). Launch it like normal.");

        if (desired.BlockUpdates)
        {
            Terminal.Warning("Launcher updates are blocked. Choose \"Allow launcher updates\" when you want to update.");
        }
        else
        {
            Terminal.Warning("Run this again after Lunar Client updates, since updates replace the patched files.");
        }
    }

    private static void Install(LunarInstall install, BackupStore backups, AsarArchive archive, PatchRun run)
    {
        var files = new Dictionary<string, byte[]>(run.ModifiedFiles, StringComparer.Ordinal)
        {
            [PatchMarker.EntryPath] = PatchMarker.Create(install.Version, run).ToBytes()
        };

        var stagedAsar = install.AsarPath + ".nmlp";
        var stagedExe = install.ExePath + ".nmlp";

        Terminal.Section("Installing");
        Terminal.Step("Building patched app.asar");

        try
        {
            var headerHash = archive.Save(stagedAsar, files);
            ElectronIntegrity.Rewrite(backups.ExePath, stagedExe, archive.HeaderHash, headerHash);
            Terminal.Done();

            Terminal.Step("Replacing launcher files");
            FileSwap.Replace(stagedAsar, install.AsarPath);
            FileSwap.Replace(stagedExe, install.ExePath);
            Terminal.Done();
        }
        finally
        {
            FileSwap.DeleteQuietly(stagedAsar);
            FileSwap.DeleteQuietly(stagedExe);
        }
    }

    private static void Restore(LunarInstall install, BackupStore backups, PatchState state)
    {
        if (!state.IsPatched)
        {
            Terminal.Success("This install is already stock. Nothing to restore.");
            return;
        }

        Terminal.Section("Restoring");
        backups.EnsureUsable();
        CloseLunar(install);

        Terminal.Step("Restoring stock files");
        FileSwap.Replace(backups.AsarPath, install.AsarPath, keepSource: true);
        FileSwap.Replace(backups.ExePath, install.ExePath, keepSource: true);
        Terminal.Done();

        Terminal.Success("Lunar Client has been restored to stock.");
    }

    private void Check(LunarInstall install, BackupStore backups, PatchState state)
    {
        if (state.IsPatched)
        {
            backups.EnsureUsable();
        }

        using var archive = AsarArchive.Open(state.IsPatched ? backups.AsarPath : install.AsarPath);

        Terminal.Section("Dry run");
        Terminal.Step("Matching patches");
        var run = PatchEngine.Run(archive, SelectPatches(new PatchState(true, true, true)));
        Terminal.Done($"{run.AppliedCount} would apply");

        PrintResults(run);
        Terminal.Success("Dry run finished. No files were changed.");
    }

    private IEnumerable<PatchDefinition> SelectPatches(PatchState state)
    {
        if (state.RemoveUpsells)
        {
            foreach (var patch in PatchCatalog.Removals.Where(patch => config.IsEnabled(patch.Feature)))
            {
                yield return patch;
            }
        }

        if (state.RemoveTelemetry)
        {
            foreach (var patch in PatchCatalog.Telemetry)
            {
                yield return patch;
            }
        }

        if (state.BlockUpdates)
        {
            yield return PatchCatalog.BlockUpdates;
        }
    }

    private static bool AnyApplied(PatchRun run, IReadOnlyList<PatchDefinition> patches) =>
        run.Results.Any(result => result.Outcome == PatchOutcome.Applied && patches.Contains(result.Patch));

    private LunarInstall ResolveInstall()
    {
        if (!string.IsNullOrWhiteSpace(options.InstallPath))
        {
            return LunarLocator.FromPath(options.InstallPath)
                ?? throw new PatcherException($"No Lunar Client install was found at '{options.InstallPath}'.");
        }

        if (!string.IsNullOrWhiteSpace(config.InstallPath))
        {
            return LunarLocator.FromPath(config.InstallPath)
                ?? throw new PatcherException($"The installPath in config.json ('{config.InstallPath}') is not a Lunar Client install.");
        }

        Terminal.Step("Looking for Lunar Client");
        var installs = LunarLocator.Discover();
        Terminal.Done(installs.Count switch
        {
            0 => "none found",
            1 => "found",
            _ => $"{installs.Count} installs found"
        });

        return installs.Count switch
        {
            0 => PromptForInstall(),
            1 => installs[0],
            _ => Terminal.Pick("Which install do you want to use?", installs, install => $"{install.Directory}  ({install.Version})")
        };
    }

    private LunarInstall PromptForInstall()
    {
        Terminal.Warning("Lunar Client could not be found automatically.");

        while (true)
        {
            var input = Terminal.Prompt("Paste your Lunar Client folder (leave empty to cancel):");

            if (input.Length == 0)
            {
                throw new PatcherException("No install selected. Nothing was changed.");
            }

            if (LunarLocator.FromPath(input) is { } install)
            {
                config.InstallPath = install.Directory;
                config.Save(configPath);
                Terminal.Note("  Saved to config.json for next time.");
                return install;
            }

            Terminal.Warning($"'{input}' does not contain {LunarInstall.ExecutableName} and resources\\app.asar.");
        }
    }

    private static PatchState ReadState(LunarInstall install)
    {
        using var archive = AsarArchive.Open(install.AsarPath);
        return PatchState.From(PatchMarker.Read(archive));
    }

    private static void PrintSummary(LunarInstall install, PatchState state)
    {
        Console.WriteLine();
        Terminal.Field("Install", install.Directory);
        Terminal.Field("Version", install.Version);
        Terminal.Field("Ads", state.RemoveUpsells ? "Removed" : "Stock", state.RemoveUpsells ? ConsoleColor.Green : ConsoleColor.White);
        Terminal.Field("Tracking", state.RemoveTelemetry ? "Removed" : "Stock", state.RemoveTelemetry ? ConsoleColor.Green : ConsoleColor.White);
        Terminal.Field("Updates", state.BlockUpdates ? "Blocked" : "Allowed", state.BlockUpdates ? ConsoleColor.Yellow : ConsoleColor.White);
    }

    private static void CloseLunar(LunarInstall install)
    {
        Terminal.Step("Closing Lunar Client");
        var closed = LunarProcesses.Terminate(install);
        Terminal.Done(closed == 0 ? "not running" : $"closed {closed} process{(closed == 1 ? "" : "es")}");
    }

    private static void PrintResults(PatchRun run)
    {
        Console.WriteLine();

        foreach (var result in run.Results)
        {
            Terminal.Result(result);
        }
    }

    private static void EnsureAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();

        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            throw new PatcherException("Run No More Lunar+ as administrator.");
        }
    }
}
