using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>Ajustes: tarjetas redondeadas, todo en oscuro. Cada cambio se aplica y se guarda al instante.</summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsForm : Form
{
    private static readonly string[] UpdateLabels = { L.T("Preguntar", "Ask"), L.T("Automáticas", "Automatic"), L.T("Desactivadas", "Off") };
    private static readonly string[] UpdateModes = { "prompt", "auto", "off" };

    private const int Margin0 = 20, Width0 = 420, CardW = Width0 - 2 * Margin0;
    private const int MaxInitial = 1_000_000;

    private readonly Monitor _monitor;
    private readonly ToggleSwitch _fortnite;
    private readonly ToggleSwitch _report;
    private readonly PillButton[] _updates = new PillButton[UpdateModes.Length];
    private readonly TextBox _folder;
    private readonly TextBox _initial;
    private bool _loading;

    public SettingsForm(Monitor monitor, Store store)
    {
        _monitor = monitor;
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
        ClientSize = new Size(Width0, 622);
        Icon = AppIcon.Load();

        Lbl(this, L.T("Ajustes", "Settings"), 14f, FontStyle.Bold, Theme.Ink, new Point(Margin0, 14), new Size(CardW, 30));

        // ---- General: dos interruptores
        var general = AddCard(56, 88);
        _fortnite = AddToggleRow(general, L.T("Abrir el contador cuando empiece Fortnite", "Open the counter when Fortnite starts"), 10, config.StartWithFortnite);
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
        var divider = new Panel { BackColor = Theme.CardEdge, Location = new Point(16, 44), Size = new Size(CardW - 32, 1) };
        general.Controls.Add(divider);
        _report = AddToggleRow(general, L.T("Abrir el informe al iniciar", "Open the report at startup"), 48, config.OpenReportOnStart);
        _report.CheckedChanged += (_, _) => { if (!_loading) _monitor.Change(c => c.OpenReportOnStart = _report.Checked); };

        // ---- Actualizaciones: tres opciones en píldoras
        var updates = AddCard(156, 76);
        CardTitle(updates, L.T("Actualizaciones", "Updates"));
        var pillW = (CardW - 32 - 2 * 8) / 3;
        for (var i = 0; i < UpdateModes.Length; i++)
        {
            var index = i;
            var pill = new PillButton { Text = UpdateLabels[i], Location = new Point(16 + i * (pillW + 8), 34), Size = new Size(pillW, 30), Radius = 10, Font = Theme.Text(9f) };
            pill.Click += (_, _) => SelectUpdateMode(index, save: true);
            updates.Controls.Add(pill);
            _updates[i] = pill;
        }
        var mode = Array.IndexOf(UpdateModes, config.UpdateMode);
        SelectUpdateMode(mode < 0 ? 0 : mode, save: false);

        // ---- Cofres de antes del contador: − [ n ] +
        var initial = AddCard(244, 96);
        CardTitle(initial, L.T("Cofres abiertos antes del contador", "Chests opened before the counter"));
        var minus = new PillButton { Text = "−", Location = new Point(16, 34), Size = new Size(34, 30), Radius = 10, Font = Theme.Text(11f, FontStyle.Bold) };
        _initial = AddField(initial, new Point(56, 34), 120, readOnly: false, Math.Min(MaxInitial, monitor.InitialChests).ToString(L.Culture));
        _initial.TextAlign = HorizontalAlignment.Center;
        var plus = new PillButton { Text = "+", Location = new Point(182, 34), Size = new Size(34, 30), Radius = 10, Font = Theme.Text(11f, FontStyle.Bold) };
        minus.Click += (_, _) => StepInitial(-10);
        plus.Click += (_, _) => StepInitial(10);
        initial.Controls.Add(minus);
        initial.Controls.Add(plus);
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
                ? L.T($"Un estimado: se suma al total de {profileName}.", $"An estimate: added to {profileName}'s total.")
                : L.T("Un estimado: se suma al total de cofres.", "An estimate: added to the chest total."),
            8.5f, FontStyle.Regular, Theme.Muted, new Point(16, 70), new Size(CardW - 32, 20));

        // ---- OBS
        var obsCard = AddCard(352, 78);
        CardTitle(obsCard, L.T("Overlay para OBS (fuente de navegador)", "Overlay for OBS (browser source)"));
        var obsUrl = ObsServer.Url;
        var obs = AddField(obsCard, new Point(16, 34), CardW - 32 - 96, readOnly: true,
            obsUrl ?? L.T("No disponible (puerto ocupado)", "Not available (port in use)"));
        var copy = new PillButton { Text = L.T("Copiar", "Copy"), Location = new Point(CardW - 16 - 88, 34), Size = new Size(88, 30), Radius = 10, Enabled = obsUrl != null };
        copy.Click += (_, _) =>
        {
            try
            {
                Clipboard.SetText(obsUrl!);
                copy.Text = L.T("¡Copiado!", "Copied!");
            }
            catch
            {
                obs.Focus();
                obs.SelectAll();
            }
        };
        obsCard.Controls.Add(copy);

        // ---- Carpeta de replays y datos
        var folderCard = AddCard(442, 110);
        CardTitle(folderCard, L.T("Carpeta de replays", "Replays folder"));
        _folder = AddField(folderCard, new Point(16, 34), CardW - 32 - 96, readOnly: true, config.DemosFolder);
        var browse = new PillButton { Text = L.T("Cambiar…", "Change…"), Location = new Point(CardW - 16 - 88, 34), Size = new Size(88, 30), Radius = 10 };
        browse.Click += (_, _) => ChooseFolder();
        folderCard.Controls.Add(browse);
        Lbl(folderCard, L.T("Tus datos (historial y ajustes) se guardan en:", "Your data (history and settings) is saved in:") + "\n" + store.Folder,
            8.5f, FontStyle.Regular, Theme.Muted, new Point(16, 70), new Size(CardW - 32, 34));

        // ---- Pie: versión y Cerrar
        Lbl(this, L.T($"Versión {AppInfo.VersionText}", $"Version {AppInfo.VersionText}"), 8.5f, FontStyle.Regular, Theme.Muted,
            new Point(Margin0, 574), new Size(200, 36), ContentAlignment.MiddleLeft);
        var close = new PillButton
        {
            Text = L.T("Cerrar", "Close"),
            Location = new Point(Width0 - Margin0 - 120, 574),
            Size = new Size(120, 36),
            Radius = 12,
            BackColor = Theme.Violet,
            HoverColor = Color.FromArgb(142, 116, 255),
            ForeColor = Color.White,
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
            _updates[i].BackColor = on ? Theme.Violet : Theme.Panel;
            _updates[i].HoverColor = on ? Color.FromArgb(142, 116, 255) : Theme.PanelHover;
            _updates[i].ForeColor = on ? Color.White : Theme.Ink;
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

    // ---- Piezas de la ventana ----------------------------------------------------------------

    private Card AddCard(int top, int height)
    {
        var card = new Card { Location = new Point(Margin0, top), Size = new Size(CardW, height), Radius = 14, BackColor = Theme.Card };
        Controls.Add(card);
        return card;
    }

    private static void CardTitle(Card card, string text) =>
        Lbl(card, text, 9f, FontStyle.Bold, Theme.Ink, new Point(16, 10), new Size(CardW - 32, 20));

    /// <summary>Texto a la izquierda y un interruptor a la derecha; clicar el texto también lo cambia.</summary>
    private static ToggleSwitch AddToggleRow(Card card, string text, int top, bool value)
    {
        var toggle = new ToggleSwitch { Location = new Point(CardW - 16 - 40, top + 4), Checked = value };
        var label = Lbl(card, text, 9f, FontStyle.Regular, Theme.Ink, new Point(16, top), new Size(CardW - 32 - 52, 30), ContentAlignment.MiddleLeft);
        label.Cursor = Cursors.Hand;
        label.Click += (_, _) => toggle.Checked = !toggle.Checked;
        card.Controls.Add(toggle);
        return toggle;
    }

    /// <summary>Campo de texto sin borde dentro de un fondo redondeado.</summary>
    private static TextBox AddField(Card card, Point at, int width, bool readOnly, string text)
    {
        var holder = new Card { Location = at, Size = new Size(width, 30), Radius = 10, BackColor = Theme.Panel };
        var box = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Font = Theme.Text(9.5f),
            ReadOnly = readOnly,
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
