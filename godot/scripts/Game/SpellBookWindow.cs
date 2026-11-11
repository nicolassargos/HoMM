using System;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le livre de sorts d'un héros (étape 9) : le livre ouvert (<see cref="SpellBookView"/>) posé sur l'écran assombri, sans
/// cadre de fenêtre ; dessous, pourquoi on ne peut pas lancer de sort (sur la carte, ou déjà un sort ce round) et
/// « Fermer ». Au combat, un clic sur un sort qu'on peut lancer le choisit ; sinon, le livre se lit seulement.
/// </summary>
public partial class SpellBookWindow : GameDialog
{
    public SpellBookWindow() { } // pour Godot

    /// <param name="points">Les points de sort qui restent (au combat, ceux du combat).</param>
    /// <param name="canCast">Ce sort peut-il être lancé maintenant ? (null : jamais, le livre se lit seulement)</param>
    /// <param name="chosen">Appelé avec le sort choisi (la fenêtre se ferme).</param>
    public SpellBookWindow(Hero hero, int points, Func<SpellType, bool>? canCast = null, string? notice = null,
        Action<SpellType>? chosen = null)
    {
        OkButtonText = Texts.CloseButton;
        // pas de cadre ni de titre : le livre est la fenêtre (une fenêtre de Godot a sinon un fond gris et « Alert! »)
        Title = "";
        Transparent = TransparentBg = true;
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", (int)ArtPx(4));
        column.AddChild(new SpellBookView(hero, points, canCast, spell =>
        {
            Hide();
            QueueFree();
            chosen?.Invoke(spell);
        }));
        if (notice != null)
        {
            var text = MakeLabel(notice, 13, TextColor);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            column.AddChild(text);
        }
        AddChild(column);
        Confirmed += QueueFree;
        Canceled += QueueFree;
    }

    /// <summary>Montre le livre au milieu de l'écran (après l'avoir ajouté à l'arbre).</summary>
    public void Open() => PopupCentered(new Vector2I((int)SpellBookView.TotalSize.X, 0));
}
