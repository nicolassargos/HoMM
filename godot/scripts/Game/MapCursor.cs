using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Ce que montre le curseur de la souris, selon ce qu'il y a dessous.</summary>
public enum CursorKind { Pointer, Move, Attack, Visit, Blocked, Hero }

/// <summary>
/// Le curseur de la souris, comme dans le premier Heroes : une flèche (un gantelet) sur l'interface, et sur la carte
/// une botte avec le nombre de jours de marche, des épées sur un monstre, une main sur un objet à visiter, une croix
/// là où on ne peut pas aller, un casque sur un de ses héros. Images : art/cursors/{pointer, move…}.png, agrandies
/// ×1,6 (le gantelet de l'interface ×2).
/// Sans image, le curseur normal reste.
/// </summary>
public static class MapCursor
{
    /// <summary>Agrandissement des images : ×2 pour le gantelet de l'interface, 20 % plus petit (×1,6) pour les autres.</summary>
    private static float ScaleOf(CursorKind kind) => (kind == CursorKind.Pointer ? 2f : 1.6f) * Ui.Scale;

    private static readonly Dictionary<(CursorKind, int), (Texture2D Texture, Vector2 Hotspot)?> Cache = new();
    private static (CursorKind, int)? _current;

    /// <summary>Ce que le curseur doit montrer sur cette case (null : on est sur l'interface).</summary>
    public static (CursorKind Kind, int Days) Choose(GameState state, Cell? hovered)
    {
        if (hovered is not { } cell)
            return (CursorKind.Pointer, 0);
        var player = state.CurrentPlayer;
        if (state.AllHeroes.Any(h => h.Owner == player && h.Position == cell))
            return (CursorKind.Hero, 0);
        // une case jamais vue : on ne dit rien de ce qu'elle cache
        if (!player.Fog.IsExplored(cell) || state.DaysTo(cell) is not { } days)
            return (CursorKind.Blocked, 0);
        if (state.EnemyHeroAt(cell, player) != null)
            return (CursorKind.Attack, days); // un héros adverse : on l'attaque
        return state.Map.OccupantAt(cell) switch
        {
            MonsterGuard => (CursorKind.Attack, days),
            { } => (CursorKind.Visit, days),
            _ => (CursorKind.Move, days),
        };
    }

    /// <summary>Change le curseur de la souris (seulement s'il change).</summary>
    public static void Show(CursorKind kind, int days = 0)
    {
        if (kind != CursorKind.Move)
            days = 0;
        var key = (kind, System.Math.Min(days, 9));
        if (_current == key)
            return;
        _current = key;
        if (!Cache.TryGetValue(key, out var cursor))
        {
            cursor = Make(kind, key.Item2);
            Cache[key] = cursor;
        }
        if (cursor is { } c)
            Input.SetCustomMouseCursor(c.Texture, Input.CursorShape.Arrow, c.Hotspot);
        else
            Input.SetCustomMouseCursor(null);
    }

    /// <summary>Développement : enregistre chaque curseur (et la botte avec 1, 2 et 3 jours) en PNG dans un dossier.</summary>
    public static void SavePreview(string folder)
    {
        foreach (var kind in System.Enum.GetValues<CursorKind>())
            foreach (int days in kind == CursorKind.Move ? new[] { 1, 2, 3 } : new[] { 0 })
                if (Make(kind, days) is { } cursor)
                    cursor.Item1.GetImage().SavePng($"{folder}/curseur-{kind.ToString().ToLowerInvariant()}{(days > 0 ? $"-{days}" : "")}.png");
    }

    private static (Texture2D, Vector2)? Make(CursorKind kind, int days)
    {
        if (Art.GetImage($"cursors/{kind.ToString().ToLowerInvariant()}") is not { } source)
            return null;
        var image = (Image)source.Duplicate();
        image.Convert(Image.Format.Rgba8);
        float scale = ScaleOf(kind);
        image.Resize((int)(image.GetWidth() * scale), (int)(image.GetHeight() * scale), Image.Interpolation.Nearest);
        if (days > 0)
            DrawNumber(image, days);
        // La flèche montre avec le bout de ses doigts (le pixel le plus haut) ; les autres, avec leur centre
        var hotspot = kind == CursorKind.Pointer ? TopPixel(image) : new Vector2(image.GetWidth(), image.GetHeight()) / 2;
        return (ImageTexture.CreateFromImage(image), hotspot);
    }

    private static Vector2 TopPixel(Image image)
    {
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
            if (image.GetPixel(x, y).A > 0.5f)
                return new Vector2(x, y);
        return Vector2.Zero;
    }

    // Chiffres en pixel art, 3 × 5 (une ligne de bits par rangée, de gauche à droite)
    private static readonly int[][] Digits =
    {
        new[] { 7, 5, 5, 5, 7 }, new[] { 2, 6, 2, 2, 7 }, new[] { 7, 1, 7, 4, 7 }, new[] { 7, 1, 7, 1, 7 },
        new[] { 5, 5, 7, 1, 1 }, new[] { 7, 4, 7, 1, 7 }, new[] { 7, 4, 7, 5, 7 }, new[] { 7, 1, 2, 2, 2 },
        new[] { 7, 5, 7, 5, 7 }, new[] { 7, 5, 7, 1, 7 },
    };

    /// <summary>Le nombre de jours, en bas à droite : un chiffre blanc bordé de noir, en gros pixels.</summary>
    private static void DrawNumber(Image image, int number)
    {
        int dot = (int)Ui.Px(3); // les gros pixels du chiffre, à l'échelle de l'écran
        var rows = Digits[number];
        int margin = (int)Ui.Px(3);
        int left = image.GetWidth() - 3 * dot - margin, top = image.GetHeight() - 5 * dot - margin;
        // le contour noir, puis le chiffre
        for (int pass = 0; pass < 2; pass++)
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 3; x++)
            {
                if ((rows[y] & (4 >> x)) == 0)
                    continue;
                var rect = new Rect2I(left + x * dot, top + y * dot, dot, dot);
                if (pass == 0)
                    image.FillRect(rect.Grow(2), Colors.Black);
                else
                    image.FillRect(rect, Colors.White);
            }
    }
}
