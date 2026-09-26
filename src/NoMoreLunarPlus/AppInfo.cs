namespace NoMoreLunarPlus;

internal static class AppInfo
{
    public const string Name = "No More Lunar+";

    public static string Version { get; } = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}
