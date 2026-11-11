using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'icône d'un sort en attendant son image (maquette du livre de sorts) : un carré de la couleur de son école, un cadre,
/// les initiales du sort ; au survol, il luit de sa couleur (des cadres de plus en plus pâles autour, qui battent).
/// </summary>
public partial class SpellIconPlaceholder : Control
{
    public Color Color { get; set; } = Colors.Gray;
    public string Initials { get; set; } = "";

    private bool _highlight;

    /// <summary>Le sort est survolé : un cadre doré.</summary>
    public bool Highlight
    {
        get => _highlight;
        set
        {
            _highlight = value;
            QueueRedraw();
        }
    }

    private Label? _label;

    public SpellIconPlaceholder() => MouseFilter = MouseFilterEnum.Ignore;

    private double _time;

    public override void _Process(double delta)
    {
        if (!_highlight)
            return;
        _time += delta;
        QueueRedraw();
    }

    public override void _Ready()
    {
        _label = MakeLabel(Initials, 16, Colors.White);
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.Size = Size;
        AddChild(_label);
    }

    public override void _Draw()
    {
        var all = new Rect2(Vector2.Zero, Size);
        if (_highlight)
        {
            // la lueur : un halo de la couleur de l'école, des carrés pleins posés l'un sur l'autre (plus dense près de
            // l'icône), qui bat doucement
            float pulse = 0.8f + 0.2f * Mathf.Sin((float)_time * 6);
            var glow = Color.Lightened(0.25f);
            for (int ring = 6; ring >= 1; ring--)
                DrawRect(all.Grow(ring * Grid), new Color(glow, pulse * 0.16f));
        }
        DrawRect(all, Color.Darkened(0.15f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X, Size.Y / 2)), Color);
        DrawRect(all, _highlight ? MapRenderer.Rgb(230, 190, 80) : Color.Darkened(0.55f), false, Grid);
    }
}
