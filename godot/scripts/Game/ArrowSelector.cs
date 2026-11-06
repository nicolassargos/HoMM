using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le sélecteur à flèches (proposition D, choisie par l'utilisateur) : deux petits boutons de pierre (◄ ►) de part et
/// d'autre d'un creux qui montre le choix ; un clic passe au précédent ou au suivant, en boucle. Le creux peut contenir
/// un texte (<see cref="SetText"/>) ou n'importe quoi (<see cref="Content"/> : un portrait et un nom…).
/// </summary>
public partial class ArrowSelector : Control
{
    private Label _label = null!;

    /// <summary>Le creux du milieu, où mettre ce que l'on choisit.</summary>
    public Panel Content { get; private set; } = null!;

    /// <summary>Un clic sur une flèche : −1 (précédent) ou +1 (suivant).</summary>
    public event Action<int>? Stepped;

    public ArrowSelector() { } // pour Godot

    /// <param name="size">La taille de tout le sélecteur (les flèches comprises), en pixels.</param>
    /// <param name="sunken">Le milieu dans un creux (un texte) ; sans creux pour une bande de portraits.</param>
    public ArrowSelector(Vector2 size, bool sunken = true)
    {
        Size = CustomMinimumSize = size;
        float button = Mathf.Min(Px(34), size.Y);
        float height = Mathf.Min(Px(60), size.Y); // pas plus haut qu'il ne faut, au milieu
        foreach (var (x, step, direction) in new[] { (0f, -1, '<'), (size.X - button, +1, '>') })
        {
            var arrowButton = FixedButton("", 12, new Vector2(button, height));
            arrowButton.Position = new Vector2(x, (size.Y - height) / 2);
            var arrow = Picture(Arrow(direction, SilverColor), Vector2.Zero, Vector2.Zero);
            arrow.Size = arrow.Texture.GetSize();
            arrow.Position = (arrowButton.Size - arrow.Size) / 2;
            arrowButton.AddChild(arrow);
            int delta = step;
            arrowButton.Pressed += () => Stepped?.Invoke(delta);
            AddChild(arrowButton);
        }
        Content = new Panel { Position = new Vector2(button + Px(4), 0), Size = new Vector2(size.X - 2 * (button + Px(4)), size.Y) };
        Content.AddThemeStyleboxOverride("panel", sunken ? Inset(DarkPanelColor) : new StyleBoxEmpty());
        AddChild(Content);
        _label = MakeLabel("", 15, TextColor);
        _label.Size = Content.Size;
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Center;
        Content.AddChild(_label);
    }

    /// <summary>Le texte du choix, au milieu du creux.</summary>
    public void SetText(string text) => _label.Text = text;
}
