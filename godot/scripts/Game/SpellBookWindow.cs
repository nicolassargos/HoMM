using System;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le livre de sorts d'un héros (étape 9) : ses points de sort, puis chaque sort (un bouton à son nom, ses points et ce
/// qu'il fait, d'après la puissance du héros), sur deux colonnes s'il y en a beaucoup. Au combat, un clic sur un sort
/// qu'on peut lancer le choisit ; sinon (sur la carte, ou quand on ne peut pas lancer), le livre se lit seulement, et
/// <paramref name="notice"/> dit pourquoi. En attendant le vrai livre, une page par niveau (étape 10).
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
        Title = Texts.SpellBookTitle(hero);
        OkButtonText = Texts.CloseButton;
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", (int)Px(6));
        list.AddChild(MakeLabel(Texts.SpellPointsLine(points, hero.MaxSpellPoints), 15, TextColor));
        if (notice != null)
            list.AddChild(MakeLabel(notice, 13, SoftTextColor));
        if (hero.Spells.Count == 0)
            list.AddChild(MakeLabel(Texts.NoSpellsKnown, 13, SoftTextColor));
        var grid = new GridContainer { Columns = hero.Spells.Count > 8 ? 2 : 1 };
        grid.AddThemeConstantOverride("h_separation", (int)Px(24));
        grid.AddThemeConstantOverride("v_separation", (int)Px(6));
        list.AddChild(grid);
        foreach (var spell in hero.Spells)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", (int)Px(10));
            bool castable = canCast?.Invoke(spell) == true;
            var button = FixedButton(spell.Name, 14, new Vector2(Px(206), ButtonHeight)); // aussi sa taille dans une rangée
            button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            button.Disabled = !castable;
            button.Pressed += () =>
            {
                Hide();
                QueueFree();
                chosen?.Invoke(spell);
            };
            row.AddChild(button);
            string cost = hero.SpellCost(spell) > points ? $"{Texts.SpellCost(spell, hero)} · {Texts.NotEnoughSpellPoints}" : Texts.SpellCost(spell, hero);
            var text = MakeLabel($"{cost}\n{Texts.SpellEffectText(spell, hero)}", 12, SoftTextColor);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = V(330, 0);
            text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(text);
            grid.AddChild(row);
        }
        AddChild(list);
        Confirmed += QueueFree;
        Canceled += QueueFree;
        _width = Px(grid.Columns == 2 ? 1120 : 580);
    }

    private readonly float _width;

    /// <summary>Montre le livre au milieu de l'écran (après l'avoir ajouté à l'arbre).</summary>
    public void Open() => PopupCentered(new Vector2I((int)_width, 0));
}
