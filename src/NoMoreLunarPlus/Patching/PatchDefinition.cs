using System.Text.RegularExpressions;

namespace NoMoreLunarPlus.Patching;

internal enum PatchScope
{
    Main,
    Renderer,
    Any
}

internal sealed class PatchDefinition(
    string id,
    string title,
    string feature,
    PatchScope scope,
    string pattern,
    string replacement,
    int maxMatches = 1)
{
    public string Id { get; } = id;

    public string Title { get; } = title;

    public string Feature { get; } = feature;

    public PatchScope Scope { get; } = scope;

    public Regex Pattern { get; } = new(pattern, RegexOptions.CultureInvariant);

    public string Replacement { get; } = replacement;

    public int MaxMatches { get; } = maxMatches;

    public bool Targets(string path)
    {
        if (!path.EndsWith(".js", StringComparison.Ordinal))
        {
            return false;
        }

        var isMain = path.StartsWith("dist-electron/", StringComparison.Ordinal);
        var isRenderer = path.StartsWith("dist/", StringComparison.Ordinal);

        return Scope switch
        {
            PatchScope.Main => isMain,
            PatchScope.Renderer => isRenderer,
            _ => isMain || isRenderer
        };
    }
}
