using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ChestCounter;

/// <summary>
/// Summary of one player's history that teammates can read: only that player's own matches, no names of
/// other players. Written to the team folder (a shared OneDrive / Google Drive / Dropbox folder) or exported
/// by hand, then merged into the teammates' reports to get exact chest counts.
/// </summary>
public sealed class TeamExport
{
    public const string FormatName = "chestcounter-team";

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = 1;
    public string? App { get; set; }
    public string Player { get; set; } = "";
    public string? EpicId { get; set; }
    /// <summary>Last analysis time of the matches (not the write time: an unchanged history is not rewritten).</summary>
    public DateTime UpdatedUtc { get; set; }
    public List<TeamExportMatch> Matches { get; set; } = new();

    [JsonIgnore] public string Key => !string.IsNullOrEmpty(EpicId) ? EpicId : Player;
    [JsonIgnore] public string SourceFile { get; set; } = "";
}

public sealed class TeamExportMatch
{
    public string SessionId { get; set; } = "";
    public DateTime? StartUtc { get; set; }
    public string? Playlist { get; set; }
    public int? Placement { get; set; }
    public int Chests { get; set; }
    public int Treasures { get; set; }
}

/// <summary>Writes this PC's summaries to the team folder and reads the teammates' ones (cached by file date).</summary>
public static class TeamShare
{
    public const string Extension = ".chestcounter.json";
    private const long MaxFileBytes = 32L * 1024 * 1024;
    private const int MaxName = 64;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Regex Unsafe = new(@"[^\p{L}\p{N}_\-]+", RegexOptions.Compiled);
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (DateTime Stamp, long Length, List<TeamExport> Exports)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Written = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Folder of the files imported by hand (inside the data folder).</summary>
    public static string ImportFolder(Store store) => Path.Combine(store.Folder, "team");

    public static bool HasTeamFolder(Config c) => !string.IsNullOrWhiteSpace(c.TeamFolder) && Directory.Exists(c.TeamFolder);

    /// <summary>One summary per account found in the history (normally one).</summary>
    public static List<TeamExport> Build(TrackerData data) => (data.Matches ?? new())
        .Where(m => m != null && m.IsBattleRoyale && !string.IsNullOrEmpty(m.SessionId))
        .GroupBy(Profiles.Key)
        .Where(g => g.Key.Length > 0)
        .Select(g =>
        {
            var latest = g.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).ToList();
            return new TeamExport
            {
                App = AppInfo.VersionText,
                Player = latest.Select(m => m.PlayerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? g.Key,
                EpicId = latest.Select(m => m.EpicId).FirstOrDefault(e => !string.IsNullOrEmpty(e)),
                UpdatedUtc = g.Max(m => m.AnalyzedAtUtc),
                Matches = g.GroupBy(m => m.SessionId).Select(s => s.First())
                    .OrderBy(m => m.MatchStartUtc ?? m.AnalyzedAtUtc)
                    .Select(m => new TeamExportMatch
                    {
                        SessionId = m.SessionId,
                        StartUtc = m.MatchStartUtc,
                        Playlist = m.Playlist,
                        Placement = m.Placement,
                        Chests = m.ChestsOpened,
                        Treasures = m.TreasureFinds?.Count ?? 0,
                    }).ToList(),
            };
        })
        .ToList();

    public static string FileName(TeamExport e)
    {
        var name = Unsafe.Replace(e.Player, "_").Trim('_');
        if (name.Length == 0) name = "player";
        if (name.Length > 32) name = name[..32];
        var id = Unsafe.Replace(e.EpicId ?? "", "");
        return name + (id.Length > 0 ? "_" + id[..Math.Min(8, id.Length)] : "") + Extension;
    }

    public static string Serialize(TeamExport e) => JsonSerializer.Serialize(e, Json);

    /// <summary>Writes this PC's summaries to the team folder, only when they changed.</summary>
    public static void Publish(Config config, TrackerData data)
    {
        if (!HasTeamFolder(config))
        {
            return;
        }
        foreach (var export in Build(data))
        {
            var path = Path.Combine(config.TeamFolder!, FileName(export));
            var text = Serialize(export);
            lock (Gate)
            {
                if (Written.TryGetValue(path, out var last) && last == text)
                {
                    continue;
                }
            }
            try
            {
                if (!File.Exists(path) || File.ReadAllText(path) != text)
                {
                    var tmp = path + ".tmp";
                    File.WriteAllText(tmp, text);
                    File.Move(tmp, path, overwrite: true);
                }
                lock (Gate)
                {
                    Written[path] = text;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Carpeta de equipo: no se pudo escribir {path}", ex);
            }
        }
    }

    /// <summary>
    /// Reads a summary file, or a teammate's data.json (converted to summaries). Returns null when the file is
    /// not one of them. Names are trimmed; nothing else in the file is used.
    /// </summary>
    public static List<TeamExport>? Parse(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > MaxFileBytes)
        {
            return null;
        }
        try
        {
            var text = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
            List<TeamExport> exports;
            if (root.TryGetProperty("Format", out var format) && format.ValueKind == JsonValueKind.String && format.GetString() == TeamExport.FormatName)
            {
                exports = new() { JsonSerializer.Deserialize<TeamExport>(text, Json) };
            }
            else if (root.TryGetProperty("Matches", out var matches) && matches.ValueKind == JsonValueKind.Array && root.TryGetProperty("ProcessedFiles", out _))
            {
                exports = JsonSerializer.Deserialize<TrackerData>(text, Json) is { } other ? Build(other) : new();
            }
            else
            {
                return null;
            }
            exports = exports.Where(e => e != null).ToList();
            foreach (var e in exports)
            {
                e.Player = Clean(e.Player);
                e.EpicId = e.EpicId is { Length: > 0 and <= 64 } ? e.EpicId : null;
                e.Matches = (e.Matches ?? new()).Where(m => m != null && !string.IsNullOrEmpty(m.SessionId) && m.SessionId.Length <= 128 && m.Chests is >= 0 and < 1000).ToList();
                e.SourceFile = path;
            }
            return exports.Where(e => e.Player.Length > 0).ToList();
        }
        catch (Exception)
        {
            // A teammate's file is untrusted: whatever is wrong with it, it is simply not used.
            return null;
        }
    }

    private static string Clean(string? name)
    {
        var s = new string((name ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
        return s.Length > MaxName ? s[..MaxName] : s;
    }

    /// <summary>
    /// Every teammate summary available (team folder + imported files), except this PC's own accounts.
    /// One per account: the most recent one wins.
    /// </summary>
    public static List<TeamExport> Load(Store store, Config config, TrackerData data)
    {
        var own = data.Matches.Select(Profiles.Key).Where(k => k.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        foreach (var folder in new[] { HasTeamFolder(config) ? config.TeamFolder! : null, ImportFolder(store) })
        {
            if (folder == null || !Directory.Exists(folder)) continue;
            try
            {
                files.AddRange(Directory.EnumerateFiles(folder, "*" + Extension));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // cloud folder offline: use what is cached
            }
        }

        var all = new List<TeamExport>();
        lock (Gate)
        {
            foreach (var file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    if (!Cache.TryGetValue(file, out var hit) || hit.Stamp != info.LastWriteTimeUtc || hit.Length != info.Length)
                    {
                        hit = (info.LastWriteTimeUtc, info.Length, Parse(file) ?? new());
                        Cache[file] = hit;
                    }
                    all.AddRange(hit.Exports);
                }
                catch (Exception)
                {
                    // unreadable file: skipped
                }
            }
        }
        return all
            .Where(e => !own.Contains(e.Key) && !own.Contains(e.Player))
            .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(e => e.UpdatedUtc).First())
            .ToList();
    }

    /// <summary>Signature of the team files (names, dates, sizes): changes when a teammate's summary is updated.</summary>
    public static string Signature(Store store, Config config)
    {
        var parts = new List<string>();
        foreach (var folder in new[] { HasTeamFolder(config) ? config.TeamFolder! : null, ImportFolder(store) })
        {
            if (folder == null || !Directory.Exists(folder)) continue;
            try
            {
                foreach (var f in new DirectoryInfo(folder).EnumerateFiles("*" + Extension))
                {
                    parts.Add($"{f.FullName}|{f.LastWriteTimeUtc.Ticks}|{f.Length}");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        parts.Sort(StringComparer.Ordinal);
        return string.Join("\n", parts);
    }

    /// <summary>Copies a teammate's file (summary or data.json) into the import folder, as a summary. Returns the players imported.</summary>
    public static List<string> Import(Store store, IReadOnlySet<string> own, string path)
    {
        var exports = Parse(path) ?? throw new InvalidDataException();
        var imported = new List<string>();
        Directory.CreateDirectory(ImportFolder(store));
        foreach (var e in exports.Where(e => !own.Contains(e.Key) && !own.Contains(e.Player)))
        {
            e.App ??= AppInfo.VersionText;
            var target = Path.Combine(ImportFolder(store), FileName(e));
            File.WriteAllText(target, Serialize(e));
            imported.Add(e.Player);
        }
        return imported;
    }

    /// <summary>Removes the imported files (the team folder is not touched).</summary>
    public static int ClearImported(Store store)
    {
        var folder = ImportFolder(store);
        if (!Directory.Exists(folder)) return 0;
        var n = 0;
        foreach (var f in Directory.EnumerateFiles(folder, "*" + Extension))
        {
            File.Delete(f);
            n++;
        }
        return n;
    }
}

/// <summary>
/// The teammates of each match, merging what the replay shows (chests seen nearby) with the exact counts shared by
/// teammates (same match id). A shared match with the same placement as yours (team modes) is a teammate even
/// when the replay is gone.
/// </summary>
public sealed class TeamView
{
    public sealed record Mate(string Key, string Name, int? Seen, int? Exact)
    {
        public int Chests => Exact ?? Seen ?? 0;
    }

    private readonly Dictionary<MatchRecord, List<Mate>> _byMatch = new();
    public IReadOnlyList<TeamExport> Shared { get; }

    public TeamView(IEnumerable<MatchRecord> matches, IReadOnlyList<TeamExport> shared)
    {
        Shared = shared;
        var lookup = shared.Select(e => (Export: e, Sessions: e.Matches.GroupBy(m => m.SessionId).ToDictionary(g => g.Key, g => g.First()))).ToList();
        foreach (var m in matches)
        {
            var mates = new List<Mate>();
            foreach (var t in m.Teammates ?? new())
            {
                var key = t.EpicId ?? t.Name;
                var hit = lookup.FirstOrDefault(l => string.Equals(l.Export.Key, key, StringComparison.OrdinalIgnoreCase)
                    || (t.EpicId == null && string.Equals(l.Export.Player, t.Name, StringComparison.OrdinalIgnoreCase)));
                int? exact = hit.Export != null && hit.Sessions.TryGetValue(m.SessionId, out var sm) ? sm.Chests : null;
                mates.Add(new Mate(key, t.Name, t.ChestsSeen, exact));
            }
            if (m.Placement is int place && place > 0 && !IsSolo(m.Playlist))
            {
                foreach (var (export, sessions) in lookup)
                {
                    if (mates.Any(x => string.Equals(x.Key, export.Key, StringComparison.OrdinalIgnoreCase))) continue;
                    if (sessions.TryGetValue(m.SessionId, out var sm) && sm.Placement == place)
                    {
                        mates.Add(new Mate(export.Key, export.Player, null, sm.Chests));
                    }
                }
            }
            if (mates.Count > 0 || m.Teammates != null)
            {
                _byMatch[m] = mates;
            }
        }
    }

    /// <summary>Teammates of a match; null when unknown (match analyzed before the teammates were recorded, nothing shared).</summary>
    public List<Mate>? Of(MatchRecord m) => _byMatch.TryGetValue(m, out var list) ? list : null;

    public IEnumerable<(MatchRecord Match, Mate Mate)> All => _byMatch.SelectMany(kv => kv.Value.Select(x => (kv.Key, x)));

    public TeamExport? SharedOf(string key) => Shared.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    private static bool IsSolo(string? playlist) => playlist?.Contains("Solo", StringComparison.OrdinalIgnoreCase) == true;
}
