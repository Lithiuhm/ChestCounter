using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>
/// Ajustes con el mismo aspecto que la ventana principal: dos columnas de tarjetas, títulos pequeños en gris y
/// cifras grandes de color. Cada cambio se aplica y se guarda al instante.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsForm : Form
{
    private static readonly string[] UpdateLabels = { L.T("Preguntar", "Ask"), L.T("Automáticas", "Automatic"), L.T("Nunca", "Never") };
    private static readonly string[] UpdateModes = { "prompt", "auto", "off" };

    // Misma rejilla que la ventana principal: columna izquierda y derecha.
    private const int LeftX = 15, LeftW = 267, RightX = 297, RightW = 280;
    private const int MaxInitial = 1_000_000;

    private readonly Monitor _monitor;
    private readonly ToggleSwitch _fortnite;
    private readonly ToggleSwitch _report;
    private readonly PillButton[] _updates = new PillButton[UpdateModes.Length];
    private readonly TextBox _folder;
    private readonly TextBox _initial;
    private readonly Store _store;
    private readonly TextBox _team;
    private readonly PillButton _teamClear;
    private readonly Label _teamState;
    private readonly Card _teamBadge;
    private readonly Label _teamHint;
    private readonly System.Windows.Forms.Timer _teamTimer = new() { Interval = 1000 };
    private bool _loading;

    public SettingsForm(Monitor monitor, Store store)
    {
        _monitor = monitor;
        _store = store;
        var config = monitor.Config;

        Text = L.T("Ajustes", "Settings");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        Font = Theme.Text();
        BackColor = Theme.Bg;
        ForeColor = Theme.Ink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(592, 499);
        Icon = AppIcon.Load();

        // Cabecera, como en la ventana principal.
        Lbl(this, L.T("AJUSTES", "SETTINGS"), 8.5f, FontStyle.Bold, Theme.Cyan, new Point(LeftX, 14), new Size(200, 18));
        Lbl(this, L.T($"Versión {AppInfo.VersionText}", $"Version {AppInfo.VersionText}"), 8.0f, FontStyle.Regular, Theme.Muted,
            new Point(RightX, 13), new Size(RightW, 18), ContentAlignment.TopRight);

        // ---- Columna izquierda ---------------------------------------------------------------

        // Inicio: dos interruptores.
        var start = AddCard(LeftX, 40, LeftW, 100, 16);
        CardTitle(start, L.T("Inicio", "Startup"));
        _fortnite = AddToggleRow(start, L.T("Abrir con Fortnite", "Open with Fortnite"),
            L.T("Cuenta tus partidas sin pensar en ello", "Counts your matches without thinking about it"), 24, config.StartWithFortnite);
        _fortnite.CheckedChanged += (_, _) =>
        {
            if (_loading)
            {
                return;
            }
            if (!_monitor.SetStartWithFortnite(_fortnite.Checked))
            {
                _loading = true;
                _fortnite.Checked = !_fortnite.Checked;
                _loading = false;
                MessageBox.Show(this,
                    L.T("No se puede desde aquí: abre el contador con ChestCounter.exe.", "Not possible from here: open the counter with ChestCounter.exe."),
                    L.T("Ajustes", "Settings"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        _report = AddToggleRow(start, L.T("Abrir el informe al iniciar", "Open the report at startup"), null, 64, config.OpenReportOnStart);
        _report.CheckedChanged += (_, _) => { if (!_loading) _monitor.Change(c => c.OpenReportOnStart = _report.Checked); };

        // Actualizaciones: tres píldoras.
        var updates = AddCard(LeftX, 149, LeftW, 74, 14);
        CardTitle(updates, L.T("Actualizaciones", "Updates"));
        var pillW = (LeftW - 26 - 2 * 6) / 3;
        for (var i = 0; i < UpdateModes.Length; i++)
        {
            var index = i;
            var pill = new PillButton { Text = UpdateLabels[i], Location = new Point(13 + i * (pillW + 6), 30), Size = new Size(pillW, 32), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
            pill.Click += (_, _) => SelectUpdateMode(index, save: true);
            updates.Controls.Add(pill);
            _updates[i] = pill;
        }
        var mode = Array.IndexOf(UpdateModes, config.UpdateMode);
        SelectUpdateMode(mode < 0 ? 0 : mode, save: false);

        // Carpeta de replays y datos.
        var folderCard = AddCard(LeftX, 232, LeftW, 94, 14);
        CardTitle(folderCard, L.T("Carpeta de replays", "Replays folder"));
        _folder = AddField(folderCard, new Point(13, 26), LeftW - 26 - 72, config.DemosFolder);
        var browse = new PillButton { Text = L.T("Cambiar", "Change"), Location = new Point(LeftW - 13 - 66, 26), Size = new Size(66, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
        browse.Click += (_, _) => ChooseFolder();
        folderCard.Controls.Add(browse);
        Lbl(folderCard, L.T("Tus datos se guardan en ", "Your data is saved in ") + store.Folder,
            7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 62), new Size(LeftW - 26, 28));

        // ---- Columna derecha -----------------------------------------------------------------

        // Cofres de antes del contador: cifra grande en ámbar, editable, con − / +.
        var initial = AddCard(RightX, 40, RightW, 100, 16);
        CardTitle(initial, L.T("Cofres antes del contador", "Chests before the counter"));
        _initial = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Card,
            ForeColor = Theme.Amber,
            Font = Theme.Text(24f, FontStyle.Bold),
            Location = new Point(15, 26),
            Width = 160,
            MaxLength = 7,
            Text = Math.Min(MaxInitial, monitor.InitialChests).ToString(L.Culture),
        };
        initial.Controls.Add(_initial);
        var plus = new PillButton { Text = "+", Location = new Point(RightW - 13 - 34, 22), Size = new Size(34, 28), Radius = 9, Font = Theme.Text(11f, FontStyle.Bold) };
        var minus = new PillButton { Text = "−", Location = new Point(RightW - 13 - 34, 54), Size = new Size(34, 28), Radius = 9, Font = Theme.Text(11f, FontStyle.Bold) };
        plus.Click += (_, _) => StepInitial(10);
        minus.Click += (_, _) => StepInitial(-10);
        initial.Controls.Add(plus);
        initial.Controls.Add(minus);
        _initial.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar);
        _initial.TextChanged += (_, _) =>
        {
            if (!_loading)
            {
                _monitor.SetInitialChests(ParseInitial());
            }
        };
        var profileName = monitor.ShownProfileName;
        Lbl(initial, profileName != null
                ? L.T($"estimado · se suma al total de {profileName}", $"estimate · added to {profileName}'s total")
                : L.T("estimado · se suma a tu total", "estimate · added to your total"),
            8.0f, FontStyle.Regular, Theme.Muted, new Point(16, 74), new Size(RightW - 70, 16));

        // OBS: dirección y estado.
        var obsCard = AddCard(RightX, 149, RightW, 74, 14);
        CardTitle(obsCard, L.T("Overlay para OBS", "Overlay for OBS"));
        var obsUrl = ObsServer.Url;
        var stateCard = new Card { Location = new Point(RightW - 13 - 70, 6), Size = new Size(70, 18), Radius = 9, BackColor = Theme.Panel };
        Lbl(stateCard, obsUrl != null ? L.T("● ACTIVO", "● ON") : L.T("● NO", "● OFF"), 7.0f, FontStyle.Bold,
            obsUrl != null ? Theme.Mint : Theme.Red, new Point(0, 0), new Size(70, 18), ContentAlignment.MiddleCenter);
        obsCard.Controls.Add(stateCard);
        var obs = AddField(obsCard, new Point(13, 30), RightW - 26 - 72, obsUrl ?? L.T("Puerto ocupado", "Port in use"));
        var copy = new PillButton { Text = L.T("Copiar", "Copy"), Location = new Point(RightW - 13 - 66, 30), Size = new Size(66, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold), Enabled = obsUrl != null };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(obsUrl!);
                copy.Text = "✓";
            }
            catch
            {
                obs.Focus();
                obs.SelectAll();
            }
        };
        obsCard.Controls.Add(copy);

        // Atajos del overlay.
        var keys = AddCard(RightX, 232, RightW, 45, 12);
        Lbl(keys, L.T("Atajos del overlay", "Overlay shortcuts"), 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 6), new Size(RightW - 26, 13));
        Lbl(keys, L.T("Ctrl+Mayús+O mostrar  ·  Ctrl+Mayús+L fijar", "Ctrl+Shift+O show  ·  Ctrl+Shift+L lock"),
            8.0f, FontStyle.Bold, Theme.Ink, new Point(13, 21), new Size(RightW - 26, 18));

        // ---- Equipo: ancho completo ----------------------------------------------------------
        const int TeamW = RightX + RightW - LeftX;
        var teamCard = AddCard(LeftX, 335, TeamW, 104, 16);
        CardTitle(teamCard, L.T("Equipo · cofres exactos de tus compañeros", "Team · your teammates' exact chests"));
        // Estado arriba a la derecha: su ancho sigue al texto (ver RefreshTeam) y va por delante del título.
        _teamBadge = new Card { Location = new Point(TeamW - 13 - 70, 6), Size = new Size(70, 18), Radius = 9, BackColor = Theme.Panel };
        _teamState = Lbl(_teamBadge, "", 7.0f, FontStyle.Bold, Theme.Muted, new Point(0, 0), new Size(70, 18), ContentAlignment.MiddleCenter);
        teamCard.Controls.Add(_teamBadge);
        _teamBadge.BringToFront();

        // Fila 1: carpeta compartida (OneDrive, Google Drive, Dropbox...) con Elegir / Quitar.
        const int Btn = 84, Gap = 6;
        _team = AddField(teamCard, new Point(13, 30), TeamW - 26 - 2 * (Btn + Gap), "");
        var chooseTeam = new PillButton { Text = L.T("Elegir", "Choose"), Location = new Point(TeamW - 13 - 2 * Btn - Gap, 30), Size = new Size(Btn, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
        chooseTeam.Click += (_, _) => ChooseTeamFolder();
        teamCard.Controls.Add(chooseTeam);
        _teamClear = new PillButton { Text = L.T("Quitar", "Remove"), Location = new Point(TeamW - 13 - Btn, 30), Size = new Size(Btn, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
        _teamClear.Click += (_, _) => { _monitor.ChangeTeamFolder(null); RefreshTeam(); };
        teamCard.Controls.Add(_teamClear);

        // Fila 2: explicación y, sin carpeta compartida, pasarse los archivos a mano.
        _teamHint = Lbl(teamCard, "", 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, 66), new Size(TeamW - 26 - 2 * (Btn + Gap) - 8, 32));
        var import = new PillButton { Text = L.T("Importar…", "Import…"), Location = new Point(TeamW - 13 - 2 * Btn - Gap, 66), Size = new Size(Btn, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
        import.Click += (_, _) => ImportTeamFile();
        teamCard.Controls.Add(import);
        var export = new PillButton { Text = L.T("Exportar…", "Export…"), Location = new Point(TeamW - 13 - Btn, 66), Size = new Size(Btn, 30), Radius = 10, Font = Theme.Text(8.5f, FontStyle.Bold) };
        export.Click += (_, _) => ExportTeamFile();
        teamCard.Controls.Add(export);

        RefreshTeam();
        _teamTimer.Tick += (_, _) => RefreshTeam();
        _teamTimer.Start();
        FormClosed += (_, _) => _teamTimer.Dispose();

        Lbl(this, L.T("Los cambios se guardan al instante", "Changes are saved instantly"), 8.0f, FontStyle.Regular, Theme.Muted,
            new Point(LeftX + 2, 459), new Size(LeftW, 18));

        // Cerrar, en el sitio de los botones de la ventana principal.
        var close = new PillButton
        {
            Text = L.T("Cerrar", "Close"),
            Location = new Point(RightX, 448),
            Size = new Size(RightW, 40),
            Radius = 12,
            BackColor = Theme.Accent,
            HoverColor = Theme.AccentHover,
            ForeColor = Theme.AccentInk,
            DialogResult = DialogResult.OK,
        };
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
        ActiveControl = close; // sin anillo de foco en el primer interruptor al abrir
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Chrome(Handle);
    }

    private void SelectUpdateMode(int index, bool save)
    {
        for (var i = 0; i < _updates.Length; i++)
        {
            var on = i == index;
            _updates[i].BackColor = on ? Theme.Accent : Theme.Panel;
            _updates[i].HoverColor = on ? Theme.AccentHover : Theme.PanelHover;
            _updates[i].ForeColor = on ? Theme.AccentInk : Theme.Ink;
            _updates[i].Invalidate();
        }
        if (save)
        {
            var chosen = UpdateModes[index];
            _monitor.Change(c => c.UpdateMode = chosen);
        }
    }

    private int ParseInitial() =>
        int.TryParse(_initial.Text, System.Globalization.NumberStyles.None, L.Culture, out var n) ? Math.Clamp(n, 0, MaxInitial) : 0;

    private void StepInitial(int delta)
    {
        var value = Math.Clamp(ParseInitial() + delta, 0, MaxInitial);
        _initial.Text = value.ToString(L.Culture); // TextChanged guarda el valor
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = L.T("Carpeta de replays de Fortnite", "Fortnite replays folder"),
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(_folder.Text) ? _folder.Text : "",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _monitor.ChangeDemosFolder(dialog.SelectedPath);
            _folder.Text = dialog.SelectedPath;
        }
    }

    // ---- Equipo -------------------------------------------------------------------------------

    /// <summary>Carpeta, estado y explicación según lo que haya (se refresca cada segundo: el informe se rehace aparte).</summary>
    private void RefreshTeam()
    {
        var folder = _monitor.Config.TeamFolder;
        var hasFolder = !string.IsNullOrWhiteSpace(folder);
        var missing = hasFolder && !Directory.Exists(folder);
        _team.Text = hasFolder ? folder! : L.T("Sin carpeta compartida", "No shared folder");
        _team.ForeColor = hasFolder ? Theme.Ink : Theme.Muted;
        _teamClear.Enabled = hasFolder;
        _teamClear.ForeColor = hasFolder ? Theme.Ink : Theme.Muted;

        var shared = ReportWriter.LastShared;
        var names = string.Join(", ", shared.Select(e => e.Player).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase));
        (string text, Color color) state = missing ? (L.T("● CARPETA NO ENCONTRADA", "● FOLDER NOT FOUND"), Theme.Red)
            : shared.Count > 0 ? (L.T($"● {shared.Count} {(shared.Count == 1 ? "COMPARTE" : "COMPARTEN")}", $"● {shared.Count} SHARING"), Theme.Mint)
            : hasFolder ? (L.T("● ESPERANDO", "● WAITING"), Theme.Amber)
            : (L.T("● NO", "● OFF"), Theme.Muted);
        if (_teamState.Text != state.text)
        {
            _teamState.Text = state.text;
            _teamState.ForeColor = state.color;
            var width = Math.Max(70, TextRenderer.MeasureText(state.text, _teamState.Font).Width + 18);
            var right = _teamBadge.Right;
            _teamBadge.SetBounds(right - width, _teamBadge.Top, width, _teamBadge.Height);
            _teamState.Width = width;
        }

        var hint = missing
            ? L.T("No se encuentra la carpeta: comprueba que la nube esté sincronizada o elige otra.", "Folder not found: check that your cloud app is syncing, or choose another one.")
            : shared.Count > 0
            ? L.T($"Datos de {names}.", $"Data from {names}.")
            : hasFolder
                ? L.T("Tus compañeros deben elegir esta misma carpeta en su ChestCounter.", "Your teammates must choose this same folder in their ChestCounter.")
                : L.T("Elige una carpeta que compartáis (OneDrive, Google Drive, Dropbox…) o pasaos el archivo con Exportar / Importar.",
                    "Choose a folder you share (OneDrive, Google Drive, Dropbox…) or swap files with Export / Import.");
        if (_teamHint.Text != hint)
        {
            _teamHint.Text = hint;
        }
    }

    private void ChooseTeamFolder()
    {
        var current = _monitor.Config.TeamFolder;
        using var dialog = new FolderBrowserDialog
        {
            Description = L.T("Carpeta compartida con tu equipo", "Folder shared with your team"),
            UseDescriptionForTitle = true,
            SelectedPath = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current) ? current : "",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        var chosen = dialog.SelectedPath;
        if (Path.GetFullPath(chosen).TrimEnd('\\').Equals(Path.GetFullPath(_store.Folder).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, L.T("Elige una carpeta compartida con tus compañeros, no la de tus datos.", "Choose a folder shared with your teammates, not your data folder."),
                L.T("Equipo", "Team"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _monitor.ChangeTeamFolder(chosen);
        RefreshTeam();
    }

    private void ImportTeamFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = L.T("Importar los cofres de un compañero", "Import a teammate's chests"),
            Filter = L.T("Archivos de ChestCounter|*" + TeamShare.Extension + ";data.json|JSON|*.json",
                "ChestCounter files|*" + TeamShare.Extension + ";data.json|JSON|*.json"),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            var players = TeamShare.Import(_store, _monitor.OwnKeys, dialog.FileName);
            _monitor.RequestReport();
            MessageBox.Show(this, players.Count > 0
                    ? L.T($"Importado: {string.Join(", ", players)}. El informe se actualiza en unos segundos.", $"Imported: {string.Join(", ", players)}. The report updates in a few seconds.")
                    : L.T("Este archivo es tuyo: no hay nada que importar.", "This file is yours: nothing to import."),
                L.T("Equipo", "Team"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, L.T("No es un archivo de ChestCounter (el resumen de un compañero o su data.json).",
                    "Not a ChestCounter file (a teammate's summary or their data.json)."),
                L.T("Equipo", "Team"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ExportTeamFile()
    {
        var export = _monitor.OwnExport();
        if (export == null)
        {
            MessageBox.Show(this, L.T("Aún no hay partidas que compartir.", "No matches to share yet."), L.T("Equipo", "Team"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = L.T("Exportar tus cofres para tu equipo", "Export your chests for your team"),
            Filter = L.T("Archivo de ChestCounter|*" + TeamShare.Extension, "ChestCounter file|*" + TeamShare.Extension),
            FileName = TeamShare.FileName(export),
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }
        try
        {
            File.WriteAllText(dialog.FileName, TeamShare.Serialize(export));
            MessageBox.Show(this, L.T($"Guardado ({L.Count(export.Matches.Count, "partida", "partidas", "match", "matches")}). Pásaselo a tus compañeros: lo abren con Ajustes → Importar.",
                    $"Saved ({L.Count(export.Matches.Count, "partida", "partidas", "match", "matches")}). Send it to your teammates: they open it with Settings → Import."),
                L.T("Equipo", "Team"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, L.T($"No se pudo guardar: {ex.Message}", $"Could not save: {ex.Message}"), L.T("Equipo", "Team"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ---- Piezas de la ventana ----------------------------------------------------------------

    private Card AddCard(int x, int y, int w, int h, int radius)
    {
        var card = new Card { Location = new Point(x, y), Size = new Size(w, h), Radius = radius, BackColor = Theme.Card };
        Controls.Add(card);
        return card;
    }

    /// <summary>Título pequeño en gris, como "Hoy" o "Mejor día" en la ventana principal.</summary>
    private static void CardTitle(Card card, string text) =>
        Lbl(card, text, 8.0f, FontStyle.Regular, Theme.Muted, new Point(13, 7), new Size(card.Width - 100, 15));

    /// <summary>Texto (y una línea gris opcional) a la izquierda y un interruptor a la derecha; clicar el texto también lo cambia.</summary>
    private static ToggleSwitch AddToggleRow(Card card, string text, string? hint, int top, bool value)
    {
        var toggle = new ToggleSwitch { Location = new Point(card.Width - 13 - 40, top + 5), Checked = value };
        var label = Lbl(card, text, 9.5f, FontStyle.Bold, Theme.Ink, new Point(13, top), new Size(card.Width - 26 - 48, 18));
        label.Cursor = Cursors.Hand;
        label.Click += (_, _) => toggle.Checked = !toggle.Checked;
        if (hint != null)
        {
            var small = Lbl(card, hint, 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, top + 18), new Size(card.Width - 26 - 48, 14));
            small.Cursor = Cursors.Hand;
            small.Click += (_, _) => toggle.Checked = !toggle.Checked;
        }
        card.Controls.Add(toggle);
        return toggle;
    }

    /// <summary>Campo de solo lectura sin borde dentro de un fondo redondeado.</summary>
    private static TextBox AddField(Card card, Point at, int width, string text)
    {
        var holder = new Card { Location = at, Size = new Size(width, 30), Radius = 10, BackColor = Theme.Panel };
        var box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Font = Theme.Text(9f),
            ReadOnly = true,
            Text = text,
            Width = width - 20,
        };
        box.Location = new Point(10, (holder.Height - box.Height) / 2);
        holder.Controls.Add(box);
        card.Controls.Add(holder);
        return box;
    }

    private static Label Lbl(Control parent, string text, float size, FontStyle style, Color color, Point at, Size sz, ContentAlignment align = ContentAlignment.TopLeft)
    {
        var label = new Label
        {
            Text = text,
            Font = Theme.Text(size, style),
            ForeColor = color,
            BackColor = parent.BackColor,
            Location = at,
            Size = sz,
            TextAlign = align,
            AutoEllipsis = true,
        };
        parent.Controls.Add(label);
        return label;
    }
}
