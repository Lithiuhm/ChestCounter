using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>Un punto del diálogo: símbolo de color, título corto y explicación.</summary>
public sealed record InfoPoint(string Glyph, Color Color, string Title, string Text);

/// <summary>
/// Diálogo de confirmación con el aspecto de la aplicación (fondo oscuro, tarjeta redondeada, botones píldora),
/// en lugar del cuadro gris de Windows. Devuelve true si se pulsa el botón principal.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class InfoDialog : Form
{
    private const int W = 440, Pad = 20, TextX = 72;

    private InfoDialog(string title, string intro, IReadOnlyList<InfoPoint> points, string ok, string cancel)
    {
        Text = title;
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
        Icon = AppIcon.Load();

        // Cabecera: icono redondo, título e introducción.
        var icon = new Card { Location = new Point(Pad, Pad), Size = new Size(40, 40), Radius = 20, BackColor = Theme.Panel };
        icon.Controls.Add(new Label
        {
            Text = "i",
            Font = new Font("Georgia", 15f, FontStyle.Bold | FontStyle.Italic),
            ForeColor = Theme.Cyan,
            BackColor = Theme.Panel,
            Location = new Point(0, 0),
            Size = new Size(40, 40),
            TextAlign = ContentAlignment.MiddleCenter,
        });
        Controls.Add(icon);

        var y = Pad - 1;
        y = AddText(this, title, Theme.Text(12f, FontStyle.Bold), Theme.Ink, TextX, y, W - TextX - Pad) + 4;
        y = AddText(this, intro, Theme.Text(9f), Theme.Muted, TextX, y, W - TextX - Pad);
        y = Math.Max(y, Pad + 40) + 16;

        // Puntos, dentro de una tarjeta.
        var card = new Card { Location = new Point(Pad, y), Radius = 14, BackColor = Theme.Card };
        var cardW = W - 2 * Pad;
        var cy = 14;
        foreach (var p in points)
        {
            var badge = new Card { Location = new Point(14, cy), Size = new Size(26, 26), Radius = 13, BackColor = Theme.Panel };
            badge.Controls.Add(new Label
            {
                Text = p.Glyph,
                Font = Theme.Text(10f, FontStyle.Bold),
                ForeColor = p.Color,
                BackColor = Theme.Panel,
                Location = new Point(0, 0),
                Size = new Size(26, 26),
                TextAlign = ContentAlignment.MiddleCenter,
            });
            card.Controls.Add(badge);
            var ty = AddText(card, p.Title, Theme.Text(9f, FontStyle.Bold), p.Color, 52, cy + 1, cardW - 52 - 14);
            ty = AddText(card, p.Text, Theme.Text(8.75f), Theme.Ink, 52, ty, cardW - 52 - 14);
            cy = Math.Max(ty, cy + 26) + 12;
        }
        card.Size = new Size(cardW, cy + 2);
        Controls.Add(card);
        y += card.Height + 18;

        // Botones: principal en cian a la derecha, cancelar a su izquierda.
        var okButton = new PillButton
        {
            Text = ok,
            Size = new Size(130, 36),
            Location = new Point(W - Pad - 130, y),
            Radius = 11,
            BackColor = Theme.Accent,
            HoverColor = Theme.AccentHover,
            ForeColor = Theme.AccentInk,
            DialogResult = DialogResult.OK,
        };
        var cancelButton = new PillButton
        {
            Text = cancel,
            Size = new Size(110, 36),
            Location = new Point(W - Pad - 130 - 8 - 110, y),
            Radius = 11,
            DialogResult = DialogResult.Cancel,
        };
        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
        ActiveControl = okButton;
        ClientSize = new Size(W, y + 36 + Pad);
    }

    /// <summary>Muestra el diálogo. True si se pulsa el botón principal; false si se cancela o se cierra.</summary>
    public static bool Confirm(IWin32Window owner, string title, string intro, IReadOnlyList<InfoPoint> points, string ok, string cancel)
    {
        using var dialog = new InfoDialog(title, intro, points, ok, cancel);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.Chrome(Handle);
    }

    /// <summary>Texto con salto de línea, del alto justo. Devuelve la coordenada de debajo.</summary>
    private int AddText(Control parent, string text, Font font, Color color, int x, int y, int width)
    {
        // Se mide a 96 ppp: el escalado automático de la ventana ya agranda después todo junto.
        var scale = DeviceDpi / 96f;
        var measured = TextRenderer.MeasureText(text, font, new Size((int)(width * scale), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        var height = (int)Math.Ceiling(measured.Height / scale) + 2;
        parent.Controls.Add(new Label
        {
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = parent.BackColor,
            Location = new Point(x, y),
            Size = new Size(width, height),
        });
        return y + height;
    }
}
