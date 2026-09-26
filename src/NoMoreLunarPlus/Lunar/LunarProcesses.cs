using System.ComponentModel;
using System.Diagnostics;

namespace NoMoreLunarPlus.Lunar;

internal static class LunarProcesses
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(10);

    public static int Terminate(LunarInstall install)
    {
        var processName = Path.GetFileNameWithoutExtension(LunarInstall.ExecutableName);
        var closed = 0;

        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                if (!BelongsTo(process, install))
                {
                    continue;
                }

                try
                {
                    process.Kill();
                    process.WaitForExit(ExitTimeout);
                    closed++;
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                {
                }
            }
        }

        return closed;
    }

    private static bool BelongsTo(Process process, LunarInstall install)
    {
        try
        {
            var path = process.MainModule?.FileName;
            return path is null || path.StartsWith(install.Directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            return true;
        }
    }
}
