namespace NoMoreLunarPlus.Patching;

internal static class PatchCatalog
{
    private const string PromoFilter =
        """(l=>Array.isArray(l)?l.filter(i=>!/store\.lunarclient\.com|lunarclient\.com\/store|moonsworth\.store|Lunar\+|Lunar Plus/.test(JSON.stringify(i))):l)""";

    public const string BlockUpdatesId = "block-updates";

    public static PatchDefinition BlockUpdates { get; } =
        new(BlockUpdatesId, "Launcher self-updates", Features.Updates, PatchScope.Main,
            """let (?<result>[\w$]+)=await (?<updater>[\w$]+)\.autoUpdater\.checkForUpdates\(\);""",
            "let ${result}=(${updater}.autoUpdater.emit(`update-not-available`),null);");

    public static IReadOnlyList<PatchDefinition> All => [.. Removals, .. Telemetry, BlockUpdates];

    public static IReadOnlyList<PatchDefinition> Telemetry { get; } =
    [
        new("analytics-events", "Launcher analytics events", Features.Telemetry, PatchScope.Main,
            """(?<![\w$])(?<head>sendEvent=async\([\w$,]*\)=>\{)(?=if\(![\w$]+\.get\(`allow-analytics-collection`\))""",
            "${head}return;"),

        new("analytics-upload", "Queued and offline analytics uploads", Features.Telemetry, PatchScope.Main,
            """(?<head>async sendEventsWithRetry\([\w$]+\)\{)""",
            "${head}return;"),

        new("sentry", "Sentry error and performance tracking", Features.Telemetry, PatchScope.Any,
            """https://[0-9a-f]+@o\d+\.ingest(?:\.[a-z]+)?\.sentry\.io/\d+""",
            "",
            maxMatches: 6),

        new("email-hashes", "Account email hashing for Overwolf", Features.Telemetry, PatchScope.Main,
            """(?<![\w$])(?<head>setEmailHashes\([\w$]+,[\w$]+\)\{)""",
            "${head}return;"),

        new("email-hash-registry", "Email hashes in Overwolf registry", Features.Telemetry, PatchScope.Main,
            """(?<![\w$])(?<head>setRegistryEmailHashes\([\w$]+,[\w$]+,[\w$]+,[\w$]+\)\{)""",
            "${head}this.clearRegistryEmailHashes();return;"),

        new("email-hash-cache", "Cached email hashes", Features.Telemetry, PatchScope.Main,
            """getCachedOWAEH\(\)\{return this\.store\.get\(`owaeh`,\{\}\)\}""",
            "getCachedOWAEH(){return this.store.delete(`owaeh`),{}}"),

        new("auto-log-upload", "Automatic crash log uploads", Features.Telemetry, PatchScope.Renderer,
            """(?<![\w$])[\w$]+\([\w$]+=>[\w$]+\.settings\.options\[`auto-upload-logs`\]\)""",
            "!1",
            maxMatches: 4)
    ];

    public static IReadOnlyList<PatchDefinition> Removals { get; } =
    [
        new("ad-placements", "Ad placements", Features.Ads, PatchScope.Any,
            """(?<![\w$])(?<state>[\w$]+)\.ads=[\w$]+\.payload\.ads(?![\w$])""",
            "${state}.ads=[]",
            maxMatches: 4),

        new("ad-visibility", "Ad visibility check", Features.Ads, PatchScope.Renderer,
            """(?<![\w$])(?<head>[\w$]+=[\w$]+=>\{)(?=let [\w$]+=[\w$]+\([\w$]+=>[\w$]+\.metadata\.ads\),[\w$]+=[\w$]+\([\w$]+=>[\w$]+\.metadata\.layoutMetadataLoaded\))""",
            "${head}return!1;"),

        new("ad-slots", "Ad slots and Lunar+ ad banners", Features.Ads, PatchScope.Renderer,
            """(?<![\w$])(?<head>[\w$]+=[\w$]+=>\{)(?=let [\w$]+=[\w$]+\(\),[\w$]+=[\w$]+\([\w$]+=>[\w$]+\.metadata\.ads\))""",
            "${head}return null;"),

        new("overwolf-crn", "Overwolf notification ads", Features.Ads, PatchScope.Main,
            """if\(![\w$]+\(`OverwolfCRN`\)\)""",
            "if(!0)"),

        new("ads-setting", "Optimized ads setting", Features.Ads, PatchScope.Renderer,
            """,\{id:`ads-optimization`,[^\[\]]*?alternateTerms:\[[^\[\]]*\]\}""",
            ""),

        new("ads-consent-setting", "Ads and data setting", Features.Ads, PatchScope.Renderer,
            """(?<![\w$])[\w$]+&&(?=[\w$]+\.push\(\{id:`ads-and-data`)""",
            "!1&&"),

        new("video-promotions", "Video ad promotions", Features.Promotions, PatchScope.Any,
            """(?<![\w$])(?<state>[\w$]+)\.videoPromotion=[\w$]+\.payload\.videoPromotion(?![\w$])""",
            "${state}.videoPromotion=void 0",
            maxMatches: 4),

        new("promo-content", "Store promos in news and navigation", Features.Store, PatchScope.Any,
            """(?<![\w$])(?<state>[\w$]+)\.(?<key>navItems|carousel|announcements)=(?<action>[\w$]+)\.payload\.\k<key>(?![\w$])""",
            "${state}.${key}=" + PromoFilter + "(${action}.payload.${key})",
            maxMatches: 12),

        new("sidebar-store", "Sidebar store button", Features.Store, PatchScope.Renderer,
            """\{type:`external`,id:`store`,[^{}]*\},""",
            ""),

        new("search-store", "Store link in search", Features.Store, PatchScope.Renderer,
            """(?<![\w$])[\w$]+\(\{id:`store`,icon:[\w$]+\([\w$]+\),url:[\w$]+\}\),""",
            ""),

        new("tray-store", "Store link in tray menu", Features.Store, PatchScope.Main,
            """\{label:`Store`,click:async\(\)=>\{[\w$]+\(\{url:`[^`]*`,initiator:[\w$]+\.tray_menu\}\)\}\},""",
            ""),

        new("coins-button", "Coins shop button", Features.Coins, PatchScope.Renderer,
            """(?<![\w$])(?<head>[\w$]+=\(\)=>\{)(?=let\{t:[\w$]+\}=[\w$]+\(\),[\w$]+=[\w$]+\([\w$]+=>[\w$]+\.metadata\.coins\);)""",
            "${head}return null;"),

        new("radio-upgrade", "Radio premium button", Features.Radio, PatchScope.Renderer,
            """(?<![\w$])(?<head>[\w$]+=[\w$]+=>\{)(?=let\{t\}=[\w$]+\(\);return\(0,[\w$]+\.jsxs\)\([\w$]+,\{onClick:[\w$]+\.onPremiumClick)""",
            "${head}return null;"),

        new("radio-banner", "Radio subscribe banner", Features.Radio, PatchScope.Renderer,
            """(?<![\w$])(?<head>[\w$]+=\(\)=>\{)(?=let\{t:[\w$]+\}=[\w$]+\(\),[\w$]+=[\w$]+\([\w$]+=>[\w$]+\.styngr\.price\);)""",
            "${head}return null;"),

        new("chat-limit", "Chat message limit upsell", Features.Chat, PatchScope.Renderer,
            """![\w$]+\(\)&&(?=\(0,[\w$]+\.jsx\)\([\w$]+,\{content:[\w$]+\(`satellite\.chat\.subscribe`\))""",
            "!1&&"),

        new("group-chat-limit", "Group chat slot upsell", Features.Chat, PatchScope.Renderer,
            """![\w$]+\(\)&&(?=\(0,[\w$]+\.jsxs\)\([\w$]+,\{gap:1,children:\[\(0,[\w$]+\.jsx\)\([\w$]+,\{[^{}]*plusColor:[^{}]*\}\),\(0,[\w$]+\.jsx\)\([\w$]+,\{[^{}]*children:[\w$]+\(`satellite\.modals\.create-conversation\.subscribe`\))""",
            "!1&&"),

        new("best-friend-limit", "Best friend limit upsell", Features.Chat, PatchScope.Main,
            """,body:[\w$]+\.t\(`notifications\.best-friends-limit\.subtitle`\)""",
            "")
    ];
}
