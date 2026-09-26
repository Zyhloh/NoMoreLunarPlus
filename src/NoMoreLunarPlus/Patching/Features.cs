namespace NoMoreLunarPlus.Patching;

internal static class Features
{
    public const string Ads = "ads";
    public const string Promotions = "promotions";
    public const string Store = "store";
    public const string Coins = "coins";
    public const string Radio = "radio";
    public const string Chat = "chat";
    public const string Updates = "updates";

    public static IReadOnlyList<string> All { get; } = [Ads, Promotions, Store, Coins, Radio, Chat];
}
