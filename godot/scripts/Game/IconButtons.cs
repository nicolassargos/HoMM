using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Les boutons carrés de pierre à icône (choix de l'utilisateur, 2026-11-09 : la barre du combat, puis le panneau de la
/// carte) : une icône d'argent (<c>art/ui/icons/{nom}.png</c>, PixelLab, 32 × 32, ×2) incrustée dans la pierre, le nom
/// et la touche en info-bulle ; sans image, les deux premières lettres du nom. L'icône d'un bouton grisé pâlit
/// (<see cref="Dim"/>).
/// </summary>
public static class IconButtons
{
    private static readonly Dictionary<string, Texture2D?> Icons = new();

    /// <summary>L'icône incrustée, réduite à sa partie visible (null : pas d'image).</summary>
    public static Texture2D? Icon(string name)
    {
        if (!Icons.TryGetValue(name, out var icon))
            Icons[name] = icon = Art.GetImage($"ui/icons/{name}") is { } image
                ? ImageTexture.CreateFromImage(Inlay(image.GetRegion(image.GetUsedRect()))) : null;
        return icon;
    }

    /// <summary>
    /// L'icône d'argent incrustée dans la pierre du bouton (choix de l'utilisateur, 2026-11-09, parmi l'argent seul, gravé
    /// et incrusté), pixel par pixel : l'argent posé dans un creux dont le bord du haut et de gauche est dans l'ombre et
    /// celui du bas et de droite prend la lumière (la lumière vient d'en haut à gauche), en contrastes doux.
    /// </summary>
    private static Image Inlay(Image icon)
    {
        icon.Convert(Image.Format.Rgba8);
        int w = icon.GetWidth() + 2, h = icon.GetHeight() + 2;
        bool Inside(int x, int y) => x >= 1 && y >= 1 && x <= w - 2 && y <= h - 2 && icon.GetPixel(x - 1, y - 1).A > 0.5f;
        var result = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        // des contrastes doux (remarque de l'utilisateur : trop brut au premier essai)
        var shadow = new Color(0, 0, 0, 0.35f);
        var light = new Color(1, 0.97f, 0.9f, 0.25f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (Inside(x, y))
            {
                var color = icon.GetPixel(x - 1, y - 1);
                // le bord du creux en haut à gauche est dans l'ombre
                if (!Inside(x - 1, y) || !Inside(x, y - 1))
                    color = color.Darkened(0.25f);
                result.SetPixel(x, y, color);
            }
            // autour du creux : la pierre prend la lumière en bas à droite, et l'ombre en haut à gauche
            else if (Inside(x - 1, y) || Inside(x, y - 1) || Inside(x - 1, y - 1))
                result.SetPixel(x, y, light);
            else if (Inside(x + 1, y) || Inside(x, y + 1))
                result.SetPixel(x, y, shadow);
        }
        return result;
    }

    /// <summary>Un bouton carré de pierre de ce côté (en pixels), avec son icône.</summary>
    public static Button Make(string icon, string text, string key, float side, Action pressed)
    {
        var button = MakeButton("", 14);
        button.CustomMinimumSize = new Vector2(side, side);
        button.Size = button.CustomMinimumSize;
        button.Pressed += pressed;
        SetIcon(button, icon, text, key);
        return button;
    }

    /// <summary>Change l'icône et l'info-bulle d'un bouton (« Se défendre » devient « Rester »).</summary>
    public static void SetIcon(Button button, string icon, string text, string key)
    {
        if (button.GetMeta("icon", "").AsString() == icon)
            return;
        button.SetMeta("icon", icon);
        button.TooltipText = Texts.KeyTooltip(text, key);
        foreach (var child in button.GetChildren())
        {
            button.RemoveChild(child);
            child.QueueFree();
        }
        if (Icon(icon) is { } image)
        {
            var size = image.GetSize() * 2;
            button.AddChild(Picture(image, ((button.CustomMinimumSize - size) / 2).Floor(), size));
            button.Text = "";
        }
        else
            button.Text = text.Length <= 2 ? text : text[..2];
    }

    /// <summary>L'icône d'un bouton grisé pâlit avec lui.</summary>
    public static void Dim(IEnumerable<Button> buttons)
    {
        foreach (var button in buttons)
            foreach (var icon in button.GetChildren().OfType<TextureRect>())
                icon.Modulate = button.Disabled ? new Color(1, 1, 1, 0.35f) : Colors.White;
    }
}
