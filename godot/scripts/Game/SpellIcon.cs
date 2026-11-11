using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'icône d'un sort dans le livre : son image (<c>art/spells/{id}.png</c>, 32 × 32, ×2, une petite peinture comme Heroes 2
/// et 3), sinon en attendant un carré de la couleur de son école et les initiales du sort ; au survol, elle luit de la
/// couleur de l'école (un halo qui bat).
/// </summary>
public partial class SpellIcon : Control
{
    /// <summary>L'image du sort ; null : le carré d'attente.</summary>
    public Texture2D? Picture { get; init; }

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

    public SpellIcon() => MouseFilter = MouseFilterEnum.Ignore;

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
        if (Picture != null)
            return;
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
        if (Picture != null)
            DrawTextureRect(Picture, all, false);
        else
        {
            DrawRect(all, Color.Darkened(0.15f));
            DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X, Size.Y / 2)), Color);
        }
        // un cadre fin d'encre, doré au survol
        DrawRect(all, _highlight ? MapRenderer.Rgb(230, 190, 80) : MapRenderer.Rgb(58, 38, 22), false, Grid);
    }
}
