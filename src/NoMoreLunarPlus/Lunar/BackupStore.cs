using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NoMoreLunarPlus.Configuration;

namespace NoMoreLunarPlus.Lunar;

internal sealed class BackupManifest
{
    public string LunarVersion { get; set; } = string.Empty;

    public string InstallPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class BackupStore
{
    private readonly LunarInstall _install;

    public BackupStore(LunarInstall install)
    {
        _install = install;
        Root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "NoMoreLunarPlus",
            "backups",
            InstallKey(install.Directory));
    }

    public string Root { get; }

    public string AsarPath => Path.Combine(Root, "app.asar");

    public string ExePath => Path.Combine(Root, LunarInstall.ExecutableName);

    private string ManifestPath => Path.Combine(Root, "backup.json");

    public void Capture()
    {
        Directory.CreateDirectory(Root);
        File.Copy(_install.AsarPath, AsarPath, overwrite: true);
        File.Copy(_install.ExePath, ExePath, overwrite: true);

        var manifest = new BackupManifest
        {
            LunarVersion = _install.Version,
            InstallPath = _install.Directory,
            CreatedAt = DateTimeOffset.Now
        };

        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, PatcherJsonContext.Default.BackupManifest));
    }

    public void EnsureUsable()
    {
        var manifest = ReadManifest();

        if (manifest is null || !File.Exists(AsarPath) || !File.Exists(ExePath))
        {
            throw new PatcherException("No stock backup exists for this install. Reinstall Lunar Client, then run the patcher again.");
        }

        if (!string.Equals(manifest.LunarVersion, _install.Version, StringComparison.OrdinalIgnoreCase))
        {
            throw new PatcherException(
                $"The backup is for Lunar Client {manifest.LunarVersion} but {_install.Version} is installed. Reinstall Lunar Client, then run the patcher again.");
        }
    }

    private BackupManifest? ReadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(ManifestPath), PatcherJsonContext.Default.BackupManifest);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string InstallKey(string directory)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(directory.ToUpperInvariant()));
        return Convert.ToHexStringLower(hash)[..12];
    }
}
