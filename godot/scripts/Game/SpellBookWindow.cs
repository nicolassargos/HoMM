using System;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le livre de sorts (étape 9) : le livre ouvert (<see cref="SpellBookView"/>) posé sur l'écran assombri, sans cadre de
/// fenêtre ; dessous, une ligne (pourquoi on ne peut pas lancer de sort, ou ce que fait la guilde) et « Fermer ». Le livre
/// d'un héros : au combat, un clic sur un sort qu'on peut lancer le choisit ; sinon, il se lit seulement. Le grimoire d'une
/// guilde des mages : ses sorts par niveau, ceux que le héros en visite connaît déjà marqués (<see cref="ForGuild"/>).
/// </summary>
public partial class SpellBookWindow : GameDialog
{
    public SpellBookWindow() { } // pour Godot

    /// <summary>Le livre d'un héros.</summary>
    /// <param name="points">Les points de sort qui restent (au combat, ceux du combat).</param>
    /// <param name="canCast">Ce sort peut-il être lancé maintenant ? (null : jamais, le livre se lit seulement)</param>
    /// <param name="chosen">Appelé avec le sort choisi (la fenêtre se ferme).</param>
    public SpellBookWindow(Hero hero, int points, Func<SpellType, bool>? canCast = null, string? notice = null,
        Action<SpellType>? chosen = null)
        : this(new SpellBookView.Content(hero.Spells.ToList(), hero, points), notice, canCast, chosen)
    {
    }

    private SpellBookWindow(SpellBookView.Content content, string? notice, Func<SpellType, bool>? canCast, Action<SpellType>? chosen)
    {
        OkButtonText = Texts.CloseButton;
        // plus sombre derrière : dans la ville, le livre s'ouvre par-dessus la fenêtre de la guilde, qui ne doit pas lui
        // disputer le regard
        VeilOpacity = 0.7f;
        // pas de cadre ni de titre : le livre est la fenêtre (une fenêtre de Godot a sinon un fond gris et « Alert! »)
        Title = "";
        Transparent = TransparentBg = true;
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", (int)ArtPx(4));
        column.AddChild(new SpellBookView(content, canCast, spell =>
        {
            Hide();
            QueueFree();
            chosen?.Invoke(spell);
        }));
        if (notice != null)
        {
            var text = MakeLabel(notice, 13, TextColor);
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = new Vector2(SpellBookView.TotalSize.X - Px(24), 0);
            // sur une plaque sombre : derrière, l'écran assombri a ses propres textes
            var plate = new PanelContainer();
            plate.AddThemeStyleboxOverride("panel", Plate(DarkPanelColor));
            plate.AddChild(text);
            column.AddChild(plate);
        }
        AddChild(column);
        Confirmed += QueueFree;
        Canceled += QueueFree;
    }

    /// <summary>
    /// Le grimoire de la guilde des mages d'une ville : tous ses niveaux (un niveau pas encore construit le dit), ses sorts ;
    /// <paramref name="visitor"/> (le héros en visite) : ses coûts, et les sorts qu'il connaît déjà marqués.
    /// </summary>
    public static SpellBookWindow ForGuild(Town town, Hero? visitor) =>
        new(new SpellBookView.Content(town.GuildSpells.ToList(), visitor, null,
                Enumerable.Range(1, Town.GuildSpellsPerLevel.Length).ToList(), level => level <= town.MageGuildLevel, MarkKnown: true),
            Texts.GuildNotice(town), null, null);

    /// <summary>Montre le livre au milieu de l'écran (après l'avoir ajouté à l'arbre).</summary>
    public void Open() => PopupCentered(new Vector2I((int)SpellBookView.TotalSize.X, 0));
}
