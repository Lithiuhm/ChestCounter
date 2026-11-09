namespace ChestCounter;

/// <summary>Construye lo que muestran la ventana, el overlay y la página OBS a partir de las partidas guardadas y la partida en curso.</summary>
public static class OverlayModel
{
    /// <summary>Texto del contador derecho entre dos partidas (también sirve para mantener el ancho del overlay).</summary>
    public static string LastMatchCaption => L.T("última partida", "last match");

    /// <param name="live">Cifras de la partida en curso, cuando el replay se pudo leer (null si no).</param>
    /// <param name="inProgress">Hay una partida grabándose (aunque sus cifras aún no se puedan leer).</param>
    /// <param name="initialChests">Cofres estimados de antes de usar la herramienta (solo se suman al total).</param>
    public static OverlayView Build(TrackerData d, MatchRecord? live, bool inProgress, int initialChests = 0)
    {
        var liveChests = live?.ChestsOpened ?? 0;
        var liveTreasures = live?.TreasureFinds.Count ?? 0;

        var total = d.Matches.Sum(m => m.ChestsOpened) + liveChests + Math.Max(0, initialChests);
        var session = d.Matches.Where(ReportWriter.IsThisSession).ToList();
        var rows = new List<OverlayRow>();

        // Sesión: cofres, y debajo "n partidas · media x" (partidas terminadas de esta sesión).
        string? sessionDetail = null;
        if (session.Count > 0)
        {
            var avg = (double)session.Sum(m => m.ChestsOpened) / session.Count;
            var avgText = Math.Round(avg, MidpointRounding.AwayFromZero).ToString("0", L.Culture);
            sessionDetail = $"{L.Count(session.Count, "partida", "partidas", "match", "matches")} · {L.T("media", "avg.")} {avgText}";
        }
        rows.Add(new OverlayRow(L.T("Sesión", "Session"),
            L.Count(session.Sum(m => m.ChestsOpened) + liveChests, "cofre", "cofres", "chest", "chests"), Detail: sessionDetail));

        var last = d.Matches.OrderByDescending(m => m.MatchStartUtc ?? m.AnalyzedAtUtc).FirstOrDefault();
        // Entre partidas el contador derecho ya muestra la última partida: la fila solo aparece durante una partida.
        if (last != null && (live != null || inProgress))
        {
            rows.Add(new OverlayRow(L.T("Última", "Last"), L.Count(last.ChestsOpened, "cofre", "cofres", "chest", "chests")));
        }

        var treasures = d.Matches.Sum(m => m.TreasureFinds.Count) + liveTreasures;
        rows.Add(new OverlayRow(L.T("Tesoros", "Treasures"), $"★ {L.N(treasures)}", Treasure: true));

        // Contador derecho: la partida en curso si se puede leer, "…" mientras no se pueda, si no la última partida.
        if (live != null)
        {
            return new OverlayView(L.N(total), L.N(live.ChestsOpened), L.T("partida actual", "current match"), true,
                L.T("● EN DIRECTO", "● LIVE"), rows);
        }
        if (inProgress)
        {
            return new OverlayView(L.N(total), "…", L.T("en curso", "in progress"), true,
                L.T("● EN PARTIDA", "● MATCH IN PROGRESS"), rows);
        }
        return new OverlayView(L.N(total), last != null ? L.N(last.ChestsOpened) : null, LastMatchCaption, false, null, rows);
    }
}
