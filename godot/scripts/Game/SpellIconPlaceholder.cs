using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'icône d'un sort en attendant son image (maquette du livre de sorts) : un carré de la couleur de son école, un cadre,
/// les initiales du sort ; un cadre doré au survol.
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
        DrawRect(all, Color.Darkened(0.15f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X, Size.Y / 2)), Color);
        DrawRect(all, _highlight ? MapRenderer.Rgb(230, 190, 80) : Color.Darkened(0.55f), false, Grid);
    }
}
