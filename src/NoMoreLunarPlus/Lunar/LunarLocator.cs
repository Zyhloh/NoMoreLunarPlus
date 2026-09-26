using System.Security;
using Microsoft.Win32;

namespace NoMoreLunarPlus.Lunar;

internal static class LunarLocator
{
    private const string FolderName = "Lunar Client";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly string[] UninstallValues = ["InstallLocation", "DisplayIcon", "UninstallString"];

    public static IReadOnlyList<LunarInstall> Discover() =>
        RegistryCandidates()
            .Concat(FolderCandidates())
            .Select(Normalize)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(directory => new LunarInstall(directory))
            .Where(install => install.IsValid)
            .ToList();

    public static LunarInstall? FromPath(string input)
    {
        var directory = Normalize(input);

        if (directory is null)
        {
            return null;
        }

        if (string.Equals(Path.GetFileName(directory), "resources", StringComparison.OrdinalIgnoreCase))
        {
            directory = Path.GetDirectoryName(directory) ?? directory;
        }

        var install = new LunarInstall(directory);
        return install.IsValid ? install : null;
    }

    private static IEnumerable<string> RegistryCandidates()
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);

            foreach (var candidate in ScanUninstallEntries(machine))
            {
                yield return candidate;
            }
        }

        using var users = RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Default);

        foreach (var sid in users.GetSubKeyNames().Where(name => !name.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)))
        {
            using var user = TryOpen(users, sid);

            if (user is null)
            {
                continue;
            }

            foreach (var candidate in ScanUninstallEntries(user))
            {
                yield return candidate;
            }
        }
    }

    private static IEnumerable<string> ScanUninstallEntries(RegistryKey root)
    {
        using var uninstall = TryOpen(root, UninstallKey);

        if (uninstall is null)
        {
            yield break;
        }

        foreach (var name in uninstall.GetSubKeyNames())
        {
            using var entry = TryOpen(uninstall, name);

            if (entry?.GetValue("DisplayName") is not string displayName ||
                !displayName.StartsWith(FolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var valueName in UninstallValues)
            {
                if (entry.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }
            }
        }
    }

    private static IEnumerable<string> FolderCandidates()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", FolderName);

        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
        {
            var root = Environment.GetFolderPath(folder);

            if (root.Length > 0)
            {
                yield return Path.Combine(root, FolderName);
            }
        }

        foreach (var profile in UserProfiles())
        {
            yield return Path.Combine(profile, "AppData", "Local", "Programs", FolderName);
        }
    }

    private static IEnumerable<string> UserProfiles()
    {
        var usersRoot = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        if (usersRoot is null || !Directory.Exists(usersRoot))
        {
            return [];
        }

        try
        {
            return Directory.GetDirectories(usersRoot);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static string? Normalize(string raw)
    {
        var value = raw.Trim().Trim('"');
        var exeIndex = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);

        if (exeIndex >= 0)
        {
            value = Path.GetDirectoryName(value[..(exeIndex + 4)].Trim('"')) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(Environment.ExpandEnvironmentVariables(value)));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static RegistryKey? TryOpen(RegistryKey parent, string name)
    {
        try
        {
            return parent.OpenSubKey(name);
        }
        catch (Exception exception) when (exception is SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
