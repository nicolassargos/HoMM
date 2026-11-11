using System;
using Godot;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le fond d'une scène du livre de sorts (choix de l'utilisateur, 2026-11-11) : l'étude des héros (onglet Magie) ou
/// l'académie d'une faction (la guilde des mages). L'image <c>art/ui/scenes/{Name}.png</c> (sans le lutrin, posé
/// dessus), ×2, centrée et coupée à la place ; sans image, une scène d'attente dessinée aux couleurs de la faction : un
/// mur, un sol, une fenêtre de nuit, une étagère de livres, une bougie qui vacille.
/// </summary>
public partial class SceneBackdrop : Control
{
    /// <summary>Le nom de l'image : « etude », ou « academie-chateau »…</summary>
    public string SceneName { get; init; } = "etude";

    /// <summary>La faction de la scène d'attente (ses couleurs) ; null : l'étude.</summary>
    public string? Faction { get; init; }

    /// <summary>Les tailles des scènes, en pixels d'art (celles des images à faire) : l'étude remplit l'onglet Magie de
    /// l'écran du héros, l'académie la fenêtre de la guilde, sous son titre.</summary>
    public static readonly Vector2 StudySize = new(504, 296), AcademySize = new(480, 232);

    private Texture2D? _image;
    private double _time;

    public SceneBackdrop()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready() => _image = Art.Get($"ui/scenes/{SceneName}");

    public override void _Process(double delta)
    {
        _time += delta;
        if (_image == null)
            QueueRedraw(); // la bougie vacille
    }

    /// <summary>Les couleurs de la scène d'attente : le mur, le sol, le bois, la lumière.</summary>
    private (Color Wall, Color Floor, Color Wood, Color Light) Palette => Faction switch
    {
        "morts-vivants" => (MapRenderer.Rgb(52, 60, 56), MapRenderer.Rgb(34, 38, 36), MapRenderer.Rgb(70, 66, 58), MapRenderer.Rgb(150, 220, 170)),
        "barbares" => (MapRenderer.Rgb(92, 64, 44), MapRenderer.Rgb(60, 42, 30), MapRenderer.Rgb(110, 72, 40), MapRenderer.Rgb(255, 190, 110)),
        "foret" => (MapRenderer.Rgb(58, 78, 48), MapRenderer.Rgb(40, 52, 32), MapRenderer.Rgb(96, 70, 40), MapRenderer.Rgb(220, 240, 150)),
        "tour" => (MapRenderer.Rgb(58, 64, 96), MapRenderer.Rgb(38, 42, 66), MapRenderer.Rgb(92, 80, 70), MapRenderer.Rgb(170, 200, 255)),
        "donjon" => (MapRenderer.Rgb(60, 44, 76), MapRenderer.Rgb(36, 26, 46), MapRenderer.Rgb(70, 52, 60), MapRenderer.Rgb(230, 150, 255)),
        "enfer" => (MapRenderer.Rgb(84, 36, 30), MapRenderer.Rgb(52, 22, 20), MapRenderer.Rgb(70, 40, 34), MapRenderer.Rgb(255, 140, 70)),
        null => (MapRenderer.Rgb(70, 54, 40), MapRenderer.Rgb(46, 34, 26), MapRenderer.Rgb(104, 70, 42), MapRenderer.Rgb(255, 214, 140)),
        _ => (MapRenderer.Rgb(84, 80, 74), MapRenderer.Rgb(54, 50, 46), MapRenderer.Rgb(104, 70, 42), MapRenderer.Rgb(255, 214, 140)),
    };

    public override void _Draw()
    {
        if (_image != null)
        {
            var size = _image.GetSize() * ArtScale;
            DrawTextureRect(_image, new Rect2(((Size - size) / 2).Floor(), size), false);
            return;
        }
        var (wall, floor, wood, light) = Palette;
        float w = Size.X, h = Size.Y, floorY = SnapDown(h * 0.72f);
        DrawRect(new Rect2(0, 0, w, floorY), wall);
        // des pierres sur le mur : des rangées décalées, un peu plus sombres
        for (float y = Grid * 8; y < floorY; y += Grid * 16)
            for (float x = ((int)(y / (Grid * 16)) % 2) * Grid * 16; x < w; x += Grid * 32)
                DrawRect(new Rect2(x, y, Grid * 30, Grid * 14), wall.Darkened(0.08f), false, Grid);
        DrawRect(new Rect2(0, floorY, w, h - floorY), floor);
        DrawRect(new Rect2(0, floorY, w, Grid * 2), floor.Darkened(0.3f));
        // une fenêtre de nuit à gauche, une lune
        var window = new Rect2(SnapDown(w * 0.1f), SnapDown(h * 0.14f), ArtPx(40), ArtPx(56));
        DrawRect(window, MapRenderer.Rgb(24, 32, 64));
        DrawRect(new Rect2(window.Position + new Vector2(ArtPx(26), ArtPx(8)), new Vector2(ArtPx(6), ArtPx(6))), MapRenderer.Rgb(230, 230, 200));
        DrawRect(window, wood.Darkened(0.2f), false, ArtPx(3));
        DrawRect(new Rect2(window.Position.X + window.Size.X / 2 - Grid, window.Position.Y, ArtPx(2), window.Size.Y), wood.Darkened(0.2f));
        // une étagère de livres à droite
        var shelf = new Rect2(SnapDown(w * 0.74f), SnapDown(h * 0.12f), ArtPx(64), floorY - SnapDown(h * 0.12f));
        DrawRect(shelf, wood.Darkened(0.35f));
        Color[] books = { MapRenderer.Rgb(140, 50, 40), MapRenderer.Rgb(50, 80, 130), MapRenderer.Rgb(70, 110, 60), MapRenderer.Rgb(150, 120, 50) };
        for (int row = 0; row < 4; row++)
        {
            float y = shelf.Position.Y + ArtPx(4 + row * 24);
            if (y + ArtPx(20) > shelf.End.Y)
                break;
            for (int b = 0; b < 9; b++)
                DrawRect(new Rect2(shelf.Position.X + ArtPx(4 + b * 6), y + ArtPx(b % 3 == 0 ? 4 : 2), ArtPx(5), ArtPx(b % 3 == 0 ? 16 : 18)), books[(row + b) % books.Length]);
            DrawRect(new Rect2(shelf.Position.X, y + ArtPx(20), shelf.Size.X, ArtPx(3)), wood);
        }
        // une bougie près du lutrin, et sa lumière qui vacille
        var candle = new Vector2(SnapDown(w * 0.3f), floorY - ArtPx(22));
        float flicker = 0.85f + 0.15f * MathF.Sin((float)_time * 9) * MathF.Sin((float)_time * 4.3f);
        for (int ring = 5; ring >= 1; ring--)
        {
            float r = ArtPx(10 + ring * 10) * flicker;
            DrawRect(new Rect2(SnapDown(candle.X - r), SnapDown(candle.Y - r), Snap(2 * r), Snap(2 * r)), new Color(light, 0.05f));
        }
        DrawRect(new Rect2(candle.X - ArtPx(3), candle.Y, ArtPx(6), ArtPx(16)), MapRenderer.Rgb(230, 220, 190));
        DrawRect(new Rect2(candle.X - Grid, candle.Y - ArtPx(5), ArtPx(2), ArtPx(4)), light);
        DrawRect(new Rect2(candle.X - ArtPx(6), candle.Y + ArtPx(16), ArtPx(12), ArtPx(4)), wood);
    }
}
