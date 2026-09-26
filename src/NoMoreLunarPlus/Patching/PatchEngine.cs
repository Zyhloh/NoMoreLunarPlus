using System.Text;
using NoMoreLunarPlus.Asar;

namespace NoMoreLunarPlus.Patching;

internal enum PatchOutcome
{
    Applied,
    NotFound,
    Ambiguous
}

internal sealed record PatchResult(PatchDefinition Patch, PatchOutcome Outcome, int Matches);

internal sealed record PatchRun(IReadOnlyList<PatchResult> Results, IReadOnlyDictionary<string, byte[]> ModifiedFiles)
{
    public int AppliedCount => Results.Count(result => result.Outcome == PatchOutcome.Applied);
}

internal static class PatchEngine
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static PatchRun Run(AsarArchive archive, IEnumerable<PatchDefinition> patches)
    {
        var candidates = archive.Files
            .Where(entry => entry.IsPacked)
            .Select(entry => entry.Path)
            .Where(path => path.EndsWith(".js", StringComparison.Ordinal))
            .ToList();

        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var modified = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<PatchResult>();

        foreach (var patch in patches)
        {
            var targets = candidates
                .Where(patch.Targets)
                .Select(path => (Path: path, Text: Load(archive, sources, path)))
                .Where(target => target.Text is not null)
                .Select(target => (target.Path, Text: target.Text!, Matches: patch.Pattern.Count(target.Text!)))
                .Where(target => target.Matches > 0)
                .ToList();

            var total = targets.Sum(target => target.Matches);

            if (total == 0)
            {
                results.Add(new PatchResult(patch, PatchOutcome.NotFound, 0));
                continue;
            }

            if (total > patch.MaxMatches)
            {
                results.Add(new PatchResult(patch, PatchOutcome.Ambiguous, total));
                continue;
            }

            foreach (var target in targets)
            {
                sources[target.Path] = patch.Pattern.Replace(target.Text, patch.Replacement);
                modified.Add(target.Path);
            }

            results.Add(new PatchResult(patch, PatchOutcome.Applied, total));
        }

        var files = modified.ToDictionary(path => path, path => StrictUtf8.GetBytes(sources[path]), StringComparer.Ordinal);
        return new PatchRun(results, files);
    }

    private static string? Load(AsarArchive archive, Dictionary<string, string> sources, string path)
    {
        if (sources.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            var text = StrictUtf8.GetString(archive.ReadFile(path));
            sources[path] = text;
            return text;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}
