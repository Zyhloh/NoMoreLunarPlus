using System.Text;

namespace NoMoreLunarPlus.Asar;

internal static class ElectronIntegrity
{
    public static void Rewrite(string sourceExe, string destinationExe, string currentHash, string newHash)
    {
        var bytes = File.ReadAllBytes(sourceExe);
        var needle = Encoding.ASCII.GetBytes(currentHash);
        var position = FindSingle(bytes, needle);

        Encoding.ASCII.GetBytes(newHash).CopyTo(bytes, position);
        File.WriteAllBytes(destinationExe, bytes);
    }

    private static int FindSingle(byte[] haystack, byte[] needle)
    {
        var span = haystack.AsSpan();
        var first = span.IndexOf(needle);

        if (first < 0)
        {
            throw new PatcherException("The launcher executable does not match its app.asar. Reinstall Lunar Client and try again.");
        }

        if (span[(first + needle.Length)..].IndexOf(needle) >= 0)
        {
            throw new PatcherException("The launcher executable contains an unexpected integrity layout. Nothing was changed.");
        }

        return first;
    }
}
