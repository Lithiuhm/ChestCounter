using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

/// <summary>Figuras redondeadas para los controles dibujados a mano.</summary>
internal static class Shape
{
    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>Tarjeta con esquinas redondeadas y borde fino. Sus textos hijos usan el color de la tarjeta.</summary>
[SupportedOSPlatform("windows")]
public sealed class Card : Panel
{
    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    /// <summary>Radio de las esquinas, en píxeles.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 16;

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Las esquinas recortadas se ven del color del fondo de la ventana.
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Bg);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var path = Shape.Rounded(rect, Radius);
        using var fill = new SolidBrush(BackColor);
        using var edge = new Pen(Theme.CardEdge, 1f);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(edge, path);
    }
}

/// <summary>Botón con esquinas redondeadas que se ilumina al pasar el ratón.</summary>
[SupportedOSPlatform("windows")]
public sealed class PillButton : Button
{
    private bool _hover;

    public PillButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Panel;
        ForeColor = Theme.Ink;
        Font = Theme.Text(9f, FontStyle.Bold);
        Cursor = Cursors.Hand;
    }

    /// <summary>Color de fondo cuando el ratón está encima.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; } = Theme.PanelHover;

    /// <summary>Radio de las esquinas, en píxeles.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 12;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // Nada: OnPaint pinta el fondo redondeado completo.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        var rect = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using var path = Shape.Rounded(rect, Radius);
        using var fill = new SolidBrush(_hover ? HoverColor : BackColor);
        g.FillPath(fill, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
