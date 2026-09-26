namespace NoMoreLunarPlus.Lunar;

internal static class FileSwap
{
    private const int Attempts = 20;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    public static void Replace(string source, string destination, bool keepSource = false)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (keepSource)
                {
                    File.Copy(source, destination, overwrite: true);
                }
                else
                {
                    File.Move(source, destination, overwrite: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == Attempts)
                {
                    throw new PatcherException($"'{Path.GetFileName(destination)}' is locked by another program. Close Lunar Client and try again.");
                }

                Thread.Sleep(RetryDelay);
            }
        }
    }

    public static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
