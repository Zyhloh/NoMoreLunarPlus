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
            new App(config, configPath, options).Execute();
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

    private void Execute()
    {
        var install = ResolveInstall();
        var backups = new BackupStore(install);
        var marker = ReadMarker(install);

        Terminal.Field("Install", install.Directory);
        Terminal.Field("Version", install.Version);
        Terminal.Field("Status", marker is null ? "Stock" : $"Patched by v{marker.PatcherVersion}", marker is null ? ConsoleColor.White : ConsoleColor.Green);

        switch (options.Action ?? ChooseAction(marker))
        {
            case PatcherAction.Patch:
                Patch(install, backups, marker is not null);
                break;
            case PatcherAction.Restore:
                Restore(install, backups, marker is not null);
                break;
            case PatcherAction.Check:
                Check(install, backups, marker is not null);
                break;
        }
    }

    private static PatcherAction? ChooseAction(PatchMarker? marker)
    {
        if (marker is null)
        {
            return PatcherAction.Patch;
        }

        return Terminal.Choose(
            "This install is already patched. What do you want to do?",
            ('R', "Re-apply patches"),
            ('U', "Unpatch and restore the stock launcher"),
            ('Q', "Quit")) switch
        {
            'R' => PatcherAction.Patch,
            'U' => PatcherAction.Restore,
            _ => null
        };
    }

    private void Patch(LunarInstall install, BackupStore backups, bool isPatched)
    {
        Terminal.Section("Patching");
        CloseLunar(install);

        if (isPatched)
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
        var run = PatchEngine.Run(archive, EnabledPatches());
        Terminal.Done($"{run.AppliedCount} applied");

        PrintResults(run);

        if (run.AppliedCount == 0)
        {
            throw new PatcherException("None of the patches matched this Lunar Client version. Nothing was changed.");
        }

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

        Terminal.Success($"Lunar Client is patched ({run.AppliedCount}/{run.Results.Count}). Launch it like normal.");
        Terminal.Warning("Run this again after Lunar Client updates, since updates replace the patched files.");
    }

    private static void Restore(LunarInstall install, BackupStore backups, bool isPatched)
    {
        if (!isPatched)
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

    private void Check(LunarInstall install, BackupStore backups, bool isPatched)
    {
        if (isPatched)
        {
            backups.EnsureUsable();
        }

        using var archive = AsarArchive.Open(isPatched ? backups.AsarPath : install.AsarPath);

        Terminal.Section("Dry run");
        Terminal.Step("Matching patches");
        var run = PatchEngine.Run(archive, EnabledPatches());
        Terminal.Done($"{run.AppliedCount} would apply");

        PrintResults(run);
        Terminal.Success("Dry run finished. No files were changed.");
    }

    private IEnumerable<PatchDefinition> EnabledPatches() =>
        PatchCatalog.All.Where(patch => config.IsEnabled(patch.Feature));

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
                Console.WriteLine();
                return install;
            }

            Terminal.Warning($"'{input}' does not contain {LunarInstall.ExecutableName} and resources\\app.asar.");
        }
    }

    private static PatchMarker? ReadMarker(LunarInstall install)
    {
        using var archive = AsarArchive.Open(install.AsarPath);
        return PatchMarker.Read(archive);
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

        Console.WriteLine();
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
