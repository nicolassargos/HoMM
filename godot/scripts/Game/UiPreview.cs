using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Développement (« --ui-preview ») : des propositions d'éléments d'interface, dessinées avec les vraies pièces du jeu
/// (pierre des boutons, creux, plaques, cadre), pour les choisir sur une capture. Pour l'instant : des listes
/// déroulantes (taille de la carte, adversaires, difficulté), fermées et ouvertes, et un sélecteur à flèches.
/// </summary>
public partial class UiPreview : CanvasLayer
{
    private static readonly string[] Sizes = { "Très petite", "Petite", "Normale", "Grande", "Très grande" };
    private static readonly Color Wood = MapRenderer.Rgb(96, 64, 36), DarkWood = MapRenderer.Rgb(62, 40, 22);

    public void Build(Vector2 screenSize)
    {
        Layer = 20;
        var root = new Panel { Size = screenSize };
        root.AddThemeStyleboxOverride("panel", Stone(DarkPanelColor));
        AddChild(root);
        root.AddChild(At(MakeLabel("Listes déroulantes : propositions", 22, TextColor), 24, 16));
        root.AddChild(At(MakeLabel("En haut : fermée (et grisée en dessous) ; au milieu : ouverte, « Normale » choisie, la souris sur « Grande ».",
            13, SoftTextColor), 24, 48));

        var designs = new (string Title, string Note, Func<StyleBox> Field, Func<Control, Control> Decorate, StyleBox List, StyleBox Hover, StyleBox Chosen, Color ArrowColor)[]
        {
            ("A · Pierre", "Le bouton de pierre A1, une flèche d'argent. La liste : un creux, les lignes à plat.",
                () => StoneButton(), field => ArrowRight(field, SilverColor), Inset(DarkPanelColor), Plate(PanelColor, SilverColor),
                StoneButton(0.7f), SilverColor),
            ("B · Creusé", "Un creux comme les champs de texte, un petit bouton de pierre pour ouvrir.",
                () => Inset(DarkPanelColor), field => SideButton(field), Inset(DarkPanelColor), Plate(PanelColor, SilverColor),
                Plate(MapRenderer.Rgb(70, 66, 60), SilverColor), SilverColor),
            ("C · Bois et or", "Une plaque de bois, un trait d'or, une flèche d'or ; la liste en bois sombre.",
                () => Plate(Wood, BorderColor), field => ArrowRight(field, BorderColor), Plate(DarkWood, BorderColor),
                Plate(Wood, BorderColor), Plate(MapRenderer.Rgb(120, 84, 44), BorderColor), BorderColor),
        };
        float x = 24;
        foreach (var design in designs)
        {
            root.AddChild(At(MakeLabel(design.Title, 18, BorderColor), x, 84));
            var note = At(MakeLabel(design.Note, 12, SoftTextColor), x, 108);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            note.CustomMinimumSize = V(280, 0);
            root.AddChild(note);
            // fermée, puis grisée
            root.AddChild(Field(design.Field(), design.Decorate, "Normale", x, 150, 1f));
            root.AddChild(Field(design.Field(), design.Decorate, "Normale", x, 192, 0.5f));
            // ouverte : le champ, puis la liste juste dessous
            root.AddChild(Field(design.Field(), design.Decorate, "Normale", x, 250, 1f));
            var list = new Panel { Position = V(x, 286), Size = V(240, 5 * 30 + 10) };
            list.AddThemeStyleboxOverride("panel", design.List);
            root.AddChild(list);
            for (int i = 0; i < Sizes.Length; i++)
            {
                var row = new Panel { Position = V(5, 5 + i * 30), Size = V(230, 28) };
                var style = i == 2 ? design.Chosen : i == 3 ? design.Hover : null;
                row.AddThemeStyleboxOverride("panel", style ?? new StyleBoxEmpty());
                row.AddChild(At(MakeLabel((i == 2 ? "✓ " : "   ") + Sizes[i], 14, i == 3 ? Colors.White : TextColor), 8, 3));
                list.AddChild(row);
            }
            x += 310;
        }

        // D : un sélecteur à flèches (pas de liste : on passe d'une valeur à l'autre)
        root.AddChild(At(MakeLabel("D · Sélecteur à flèches", 18, BorderColor), x, 84));
        var noteD = At(MakeLabel("Pas de liste : deux boutons de pierre font défiler les choix. Tout reste visible en un coup d'œil, un clic suffit.",
            12, SoftTextColor), x, 108);
        noteD.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        noteD.CustomMinimumSize = V(280, 0);
        root.AddChild(noteD);
        foreach (var (text, y, light) in new[] { ("Normale", 150f, 1f), ("Difficile", 192f, 1f), ("2", 250f, 1f), ("Normale", 292f, 0.5f) })
            root.AddChild(Selector(text, x, y, light));

        // les trois réglages tels qu'ils seraient dans la fenêtre Nouvelle partie, avec A
        root.AddChild(At(MakeLabel("Dans la fenêtre Nouvelle partie (avec A) :", 16, BorderColor), 24, 470));
        float sx = 24;
        foreach (var (label, value) in new[] { ("Taille de la carte", "Normale (54 × 40)"), ("Adversaires", "1"), ("Difficulté", "Normale") })
        {
            root.AddChild(At(MakeLabel(label, 15, BorderColor), sx, 500));
            root.AddChild(Field(StoneButton(), f => ArrowRight(f, SilverColor), value, sx, 524, 1f, 220));
            sx += 240;
        }
    }

    private static Control Field(StyleBox style, Func<Control, Control> decorate, string text, float x, float y, float light, float width = 240)
    {
        var field = new Panel { Position = V(x, y), Size = V(width, 34), Modulate = new Color(light, light, light) };
        field.AddThemeStyleboxOverride("panel", style);
        field.AddChild(At(MakeLabel(text, 15, TextColor), 12, 6));
        return decorate(field);
    }

    /// <summary>Une flèche vers le bas au bout du champ.</summary>
    private static Control ArrowRight(Control field, Color color)
    {
        var arrow = Picture(Arrow('v', color), new Vector2(field.Size.X - Px(30), (field.Size.Y - Px(10)) / 2), Vector2.Zero);
        arrow.Size = arrow.Texture.GetSize();
        field.AddChild(arrow);
        return field;
    }

    /// <summary>Un petit bouton de pierre carré au bout du champ, avec la flèche.</summary>
    private static Control SideButton(Control field)
    {
        var button = new Panel { Position = new Vector2(field.Size.X - Px(34), 0), Size = V(34, 34) };
        button.AddThemeStyleboxOverride("panel", StoneButton());
        field.AddChild(button);
        var arrow = Picture(Arrow('v', SilverColor), (button.Size - Arrow('v', SilverColor).GetSize()) / 2, Vector2.Zero);
        arrow.Size = arrow.Texture.GetSize();
        button.AddChild(arrow);
        return field;
    }

    /// <summary>D : [◄] la valeur dans un creux [►].</summary>
    private static Control Selector(string text, float x, float y, float light)
    {
        var box = new Control { Position = V(x, y), Size = V(240, 34), Modulate = new Color(light, light, light) };
        foreach (var (bx, direction) in new[] { (0f, '<'), (206f, '>') })
        {
            var button = new Panel { Position = V(bx, 0), Size = V(34, 34) };
            button.AddThemeStyleboxOverride("panel", StoneButton());
            var arrow = Picture(Arrow(direction, SilverColor), Vector2.Zero, Vector2.Zero);
            arrow.Size = arrow.Texture.GetSize();
            arrow.Position = (button.Size - arrow.Size) / 2;
            button.AddChild(arrow);
            box.AddChild(button);
        }
        var field = new Panel { Position = V(38, 0), Size = V(164, 34) };
        field.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        var label = MakeLabel(text, 15, TextColor);
        label.Size = V(164, 34);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        field.AddChild(label);
        box.AddChild(field);
        return box;
    }
}
