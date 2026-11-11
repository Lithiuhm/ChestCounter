namespace ChestCounter;

/// <summary>An Epic account that recorded replays on this PC. Stats are kept per profile.</summary>
public sealed record Profile(string Key, string Name, int Matches, int Chests, DateTime LastPlayed);

/// <summary>
/// Replays saved on a PC are normally those of its player, so the replay owner (the recording player) is tracked.
/// When several accounts recorded replays here, each one gets its own profile and the player picks the account
/// they play on: the overlay and the report only show that profile.
/// </summary>
public static class Profiles
{
    /// <summary>Stable key of a match's account: Epic id (survives a name change), else the display name.</summary>
    public static string Key(MatchRecord m) => !string.IsNullOrEmpty(m.EpicId) ? m.EpicId : m.PlayerName ?? "";

    public static List<Profile> List(TrackerData d, Config? c = null) => d.Matches
        .GroupBy(Key)
        .Select(g =>
        {
            var latest = g.OrderByDescending(When).First();
            var name = g.OrderByDescending(When).Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
            var chests = g.Sum(m => (c != null ? Adjust(m, c)! : m).ChestsOpened);
            return new Profile(g.Key, name ?? L.T("Cuenta desconocida", "Unknown account"), g.Count(), chests, When(latest));
        })
        .OrderByDescending(p => p.LastPlayed)
        .ToList();

    private static DateTime When(MatchRecord m) => m.MatchStartUtc ?? m.AnalyzedAtUtc;

    /// <summary>The selected profile when it exists, else the most recently played one (null = no match yet).</summary>
    public static Profile? Current(TrackerData d, Config c)
    {
        var all = List(d);
        return all.FirstOrDefault(p => p.Key == c.Profile) ?? all.FirstOrDefault();
    }

    /// <summary>
    /// The data of one profile only, as seen by the window, the overlay and the report. With the "count ammo
    /// boxes / food boxes / loot cannons" settings, the matches are copies whose total includes them.
    /// </summary>
    public static TrackerData View(TrackerData d, Config c)
    {
        var current = Current(d, c);
        return new TrackerData
        {
            Version = d.Version,
            AnalyzerVersion = d.AnalyzerVersion,
            CreatedUtc = d.CreatedUtc,
            Matches = d.Matches.Where(m => current == null || Key(m) == current.Key).Select(m => Adjust(m, c)!).ToList(),
            ProcessedFiles = d.ProcessedFiles,
        };
    }

    /// <summary>A match as shown: its total also includes the non-chest kinds chosen in the settings.</summary>
    public static MatchRecord? Adjust(MatchRecord? m, Config c) => m?.WithExtras(c.ExtraKinds());

    /// <summary>Estimated chests opened before the tool for a profile key ("" = before the first match).</summary>
    public static int InitialChests(Config c, string? key) =>
        c.InitialChests != null && c.InitialChests.TryGetValue(key ?? "", out var n) ? Math.Max(0, n) : 0;

    /// <summary>Estimated chests opened before the tool for the profile shown.</summary>
    public static int InitialChests(TrackerData d, Config c) => InitialChests(c, Current(d, c)?.Key);

    public static string Describe(Profile p) =>
        $"{p.Name}  ({L.Count(p.Matches, "partida", "partidas", "match", "matches")}, {L.Count(p.Chests, "cofre", "cofres", "chest", "chests")})";
}
