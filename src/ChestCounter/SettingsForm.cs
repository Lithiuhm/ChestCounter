using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>Ajustes mínimos: cada cambio se aplica y se guarda al instante.</summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsForm : Form
{
    private static readonly string[] UpdateLabels = { L.T("Preguntar", "Ask"), L.T("Instalar solas", "Install automatically"), L.T("Desactivadas", "Off") };
    private static readonly string[] UpdateModes = { "prompt", "auto", "off" };

    private readonly Monitor _monitor;
    private readonly CheckBox _fortnite;
    private readonly CheckBox _report;
    private readonly ComboBox _updates;
    private readonly TextBox _folder;
    private readonly NumericUpDown _initial;
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
        // Se ajusta al contenido (incluido el botón Cerrar) con margen inferior, sin depender del escalado de pantalla.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(0, 0, 20, 24);
        Icon = AppIcon.Load();

        AddLabel(L.T("Ajustes", "Settings"), 13f, FontStyle.Bold, new Point(20, 16), new Size(340, 28));

        _fortnite = AddCheck(L.T("Abrir el contador cuando empiece Fortnite", "Open the counter when Fortnite starts"), new Point(20, 56), config.StartWithFortnite);
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

        _report = AddCheck(L.T("Abrir el informe al iniciar", "Open the report at startup"), new Point(20, 88), config.OpenReportOnStart);
        _report.CheckedChanged += (_, _) => { if (!_loading) _monitor.Change(c => c.OpenReportOnStart = _report.Checked); };

        AddLabel(L.T("Actualizaciones", "Updates"), 9f, FontStyle.Regular, new Point(20, 134), new Size(160, 24));
        _updates = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Font = Theme.Text(),
            Location = new Point(200, 130),
            Size = new Size(160, 26),
        };
        _updates.Items.AddRange(UpdateLabels);
        var mode = Array.IndexOf(UpdateModes, config.UpdateMode);
        _updates.SelectedIndex = mode < 0 ? 0 : mode;
        _updates.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading)
            {
                var chosen = UpdateModes[_updates.SelectedIndex];
                _monitor.Change(c => c.UpdateMode = chosen);
            }
        };
        Controls.Add(_updates);

        AddLabel(L.T("Cofres abiertos antes del contador", "Chests opened before the counter"), 9f, FontStyle.Regular, new Point(20, 176), new Size(240, 24));
        _initial = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 1_000_000,
            Increment = 10,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.Text(),
            Location = new Point(260, 172),
            Size = new Size(100, 26),
            TextAlign = HorizontalAlignment.Right,
            Value = Math.Min(1_000_000, monitor.InitialChests),
        };
        _initial.ValueChanged += (_, _) => { if (!_loading) _monitor.SetInitialChests((int)_initial.Value); };
        Controls.Add(_initial);
        var profileName = monitor.ShownProfileName;
        AddLabel(profileName != null
                ? L.T($"Un estimado: se suma al total de {profileName}.", $"An estimate: added to {profileName}'s total.")
                : L.T("Un estimado: se suma al total de cofres.", "An estimate: added to the chest total."),
            8.5f, FontStyle.Regular, new Point(20, 200), new Size(340, 20), Theme.Muted);

        AddLabel(L.T("Carpeta de replays", "Replays folder"), 9f, FontStyle.Regular, new Point(20, 230), new Size(340, 22));
        _folder = new TextBox
        {
            Location = new Point(20, 254),
            Size = new Size(270, 26),
            ReadOnly = true,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            BorderStyle = BorderStyle.FixedSingle,
            Text = config.DemosFolder,
        };
        Controls.Add(_folder);
        var browse = new Button { Text = L.T("Cambiar…", "Change…"), Location = new Point(296, 252), Size = new Size(64, 30) };
        Theme.Style(browse);
        browse.Click += (_, _) => ChooseFolder();
        Controls.Add(browse);

        AddLabel(L.T("Tus datos (historial, ajustes) se guardan en:", "Your data (history, settings) is saved in:"),
            8.5f, FontStyle.Regular, new Point(20, 302), new Size(340, 20), Theme.Muted);
        AddLabel(store.Folder, 8.5f, FontStyle.Regular, new Point(20, 322), new Size(340, 32), Theme.Muted);

        var close = new Button { Text = L.T("Cerrar", "Close"), Location = new Point(260, 364), Size = new Size(100, 30), DialogResult = DialogResult.OK };
        Theme.Style(close);
        Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Chrome(Handle);
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

    private void AddLabel(string text, float size, FontStyle style, Point at, Size sz, Color? color = null)
    {
        Controls.Add(new Label
        {
            Text = text,
            Font = Theme.Text(size, style),
            ForeColor = color ?? Theme.Ink,
            BackColor = Theme.Bg,
            Location = at,
            Size = sz,
        });
    }

    private CheckBox AddCheck(string text, Point at, bool value)
    {
        var box = new CheckBox
        {
            Text = text,
            Location = at,
            Size = new Size(340, 26),
            ForeColor = Theme.Ink,
            BackColor = Theme.Bg,
            Font = Theme.Text(),
            Checked = value,
        };
        Controls.Add(box);
        return box;
    }
}
