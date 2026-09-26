using System.Diagnostics;

namespace NoMoreLunarPlus.Lunar;

internal sealed class LunarInstall
{
    public const string ExecutableName = "Lunar Client.exe";

    private string? _version;

    public LunarInstall(string directory)
    {
        Directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    }

    public string Directory { get; }

    public string ExePath => Path.Combine(Directory, ExecutableName);

    public string AsarPath => Path.Combine(Directory, "resources", "app.asar");

    public bool IsValid => File.Exists(ExePath) && File.Exists(AsarPath);

    public string Version => _version ??= ReadVersion();

    private string ReadVersion()
    {
        var info = FileVersionInfo.GetVersionInfo(ExePath);
        return info.ProductVersion ?? info.FileVersion ?? "unknown";
    }
}
