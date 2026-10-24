using System;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Dessin procédural des tuiles, des objets et du héros (aucune image nécessaire).
/// Plus tard, on pourra remplacer ces fonctions par de vrais sprites.
/// Toutes les méthodes s'appellent depuis le _Draw() d'un CanvasItem.
/// </summary>
public static class MapRenderer
{
    public const int TileSize = 48;
    private const int T = TileSize;

    public static readonly Color PlayerColor = Rgb(200, 40, 40);
    public static readonly Color FogColor = Rgb(12, 12, 16);

    public static Color Rgb(int r, int g, int b) => Color.Color8((byte)r, (byte)g, (byte)b);

    public static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => Rgb(86, 150, 60),
        TerrainType.Road => Rgb(170, 140, 95),
        TerrainType.Forest => Rgb(52, 105, 45),
        TerrainType.Sand => Rgb(214, 196, 128),
        TerrainType.Swamp => Rgb(74, 98, 72),
        TerrainType.Water => Rgb(40, 90, 170),
        TerrainType.Mountain => Rgb(120, 112, 104),
        _ => Colors.Magenta,
    };

    public static Color ResourceColor(ResourceType resource) => resource switch
    {
        ResourceType.Gold => Rgb(240, 200, 40),
        ResourceType.Wood => Rgb(140, 90, 40),
        ResourceType.Ore => Rgb(110, 110, 120),
        ResourceType.Gems => Rgb(200, 60, 200),
        _ => Colors.Magenta,
    };

    private static Color Vary(Color c, Random rng, int amount)
    {
        float d = rng.Next(-amount, amount + 1) / 255f;
        return new Color(Math.Clamp(c.R + d, 0, 1), Math.Clamp(c.G + d, 0, 1), Math.Clamp(c.B + d, 0, 1));
    }

    private static Vector2 V(float x, float y) => new(x, y);

    private static void Triangle(CanvasItem ci, Vector2 a, Vector2 b, Vector2 c, Color color) =>
        ci.DrawColoredPolygon(new[] { a, b, c }, color);

    private static void Ellipse(CanvasItem ci, Rect2 rect, Color color)
    {
        var points = new Vector2[16];
        var center = rect.GetCenter();
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Tau * i / points.Length;
            points[i] = center + new Vector2(Mathf.Cos(angle) * rect.Size.X / 2, Mathf.Sin(angle) * rect.Size.Y / 2);
        }
        ci.DrawColoredPolygon(points, color);
    }

    // ------------------------------------------------------------------ terrain

    public static void DrawTile(CanvasItem ci, TerrainType terrain, Vector2 o, Random rng)
    {
        var baseColor = TerrainColor(terrain);
        ci.DrawRect(new Rect2(o, T, T), Vary(baseColor, rng, 6));

        switch (terrain)
        {
            case TerrainType.Grass or TerrainType.Sand or TerrainType.Swamp:
                for (int i = 0; i < 10; i++)
                    ci.DrawCircle(o + V(rng.Next(T), rng.Next(T)), rng.Next(1, 3), Vary(baseColor, rng, 25));
                if (terrain == TerrainType.Swamp)
                    for (int i = 0; i < 2; i++)
                        Ellipse(ci, new Rect2(o + V(rng.Next(6, T - 14), rng.Next(6, T - 8)), 12, 5), Rgb(60, 90, 110));
                break;

            case TerrainType.Road:
                for (int i = 0; i < 8; i++)
                    ci.DrawCircle(o + V(rng.Next(T), rng.Next(T)), 2, Rgb(140, 115, 80));
                break;

            case TerrainType.Forest:
                for (int i = 0; i < 3; i++)
                {
                    var p = o + V(rng.Next(8, T - 8), rng.Next(14, T - 4));
                    ci.DrawRect(new Rect2(p + V(-2, 0), 4, 6), Rgb(80, 55, 30));
                    Triangle(ci, p + V(0, -18), p + V(-10, 2), p + V(10, 2), Vary(Rgb(30, 80, 35), rng, 15));
                }
                break;

            case TerrainType.Water:
                for (int i = 0; i < 3; i++)
                {
                    var p = o + V(rng.Next(10, T - 10), rng.Next(8, T - 4));
                    ci.DrawArc(p, 6, Mathf.Pi, Mathf.Tau, 8, Rgb(110, 160, 220), 2);
                }
                break;

            case TerrainType.Mountain:
                float px = o.X + T / 2f + rng.Next(-4, 5);
                Triangle(ci, V(px, o.Y + 4), V(o.X + 2, o.Y + T - 2), V(o.X + T - 2, o.Y + T - 2), Rgb(95, 88, 82));
                Triangle(ci, V(px, o.Y + 4), V(px - 14, o.Y + T - 2), V(px, o.Y + T - 2), Rgb(150, 142, 134));
                Triangle(ci, V(px, o.Y + 4), V(px - 6, o.Y + 16), V(px + 6, o.Y + 16), Rgb(240, 240, 245));
                break;
        }
    }

    // ---------------------------------------------------------------- ressources

    public static void DrawResourceIcon(CanvasItem ci, ResourceType resource, Vector2 c, float r = 7)
    {
        var color = ResourceColor(resource);
        switch (resource)
        {
            case ResourceType.Gems:
                var diamond = new[] { c + V(0, -r), c + V(r, 0), c + V(0, r), c + V(-r, 0) };
                ci.DrawColoredPolygon(diamond, color);
                ci.DrawPolyline(new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, Rgb(255, 200, 255));
                break;
            case ResourceType.Wood:
                ci.DrawRect(new Rect2(c + V(-r, -r / 2), 2 * r, r), color);
                ci.DrawCircle(c + V(r, 0), r / 2, Rgb(200, 160, 100));
                break;
            default:
                ci.DrawCircle(c, r, color);
                ci.DrawArc(c, r, 0, Mathf.Tau, 16, Rgb(40, 30, 10), 1);
                break;
        }
    }

    // -------------------------------------------------------------------- objets

    public static void DrawObject(CanvasItem ci, MapObject obj, Vector2 o, double time)
    {
        var c = o + V(T / 2f, T / 2f);
        switch (obj)
        {
            case ResourcePile pile:
                foreach (var d in new[] { V(-6, 2), V(6, 2), V(0, -5) })
                    DrawResourceIcon(ci, pile.Resource, c + d);
                break;

            case TreasureChest:
                ci.DrawRect(new Rect2(c + V(-13, -6), 26, 18), Rgb(110, 70, 30));
                ci.DrawRect(new Rect2(c + V(-13, -12), 26, 8), Rgb(150, 100, 45));
                ci.DrawRect(new Rect2(c + V(-13, -6), 26, 3), Rgb(230, 190, 60));
                ci.DrawRect(new Rect2(c + V(-3, -4), 6, 7), Rgb(230, 190, 60));
                if ((int)(time * 2) % 2 == 0) // petit scintillement
                    ci.DrawCircle(c + V(10, -13), 2, Rgb(255, 255, 200));
                break;

            case Mine mine:
                Triangle(ci, c + V(-20, 16), c + V(0, -14), c + V(20, 16), Rgb(90, 80, 70));
                Ellipse(ci, new Rect2(c + V(-7, 2), 14, 14), Rgb(20, 15, 10));
                DrawResourceIcon(ci, mine.Resource, c + V(13, 12), 6);
                ci.DrawLine(c + V(-14, -2), c + V(-14, -20), Rgb(60, 40, 20), 2);
                var flag = mine.Owner != null ? PlayerColor : Rgb(200, 200, 200);
                Triangle(ci, c + V(-13, -20), c + V(-2, -16), c + V(-13, -12), flag);
                break;

            case MonsterGuard guard:
                DrawMonster(ci, guard, c, time);
                break;

            case Town town:
                DrawTown(ci, town, c, time);
                break;
        }
    }

    public static readonly Color NeutralColor = Rgb(200, 200, 200);

    /// <summary>Un petit château : deux tours, un donjon, une porte, et le drapeau du propriétaire.</summary>
    private static void DrawTown(CanvasItem ci, Town town, Vector2 c, double time)
    {
        var wall = Rgb(185, 175, 160);
        var dark = Rgb(120, 112, 100);
        Ellipse(ci, new Rect2(c + V(-22, 14), 44, 9), new Color(0, 0, 0, 0.45f));
        ci.DrawRect(new Rect2(c + V(-18, -4), 36, 20), wall);                 // rempart
        ci.DrawRect(new Rect2(c + V(-22, -12), 10, 28), dark);                // tour gauche
        ci.DrawRect(new Rect2(c + V(12, -12), 10, 28), dark);                 // tour droite
        ci.DrawRect(new Rect2(c + V(-7, -18), 14, 22), wall);                 // donjon
        foreach (float x in new[] { -22f, -17f, 12f, 17f })
            ci.DrawRect(new Rect2(c + V(x, -15), 3, 3), dark);                // créneaux
        Ellipse(ci, new Rect2(c + V(-5, 4), 10, 14), Rgb(50, 35, 20));        // porte
        ci.DrawRect(new Rect2(c + V(-5, 10), 10, 6), Rgb(50, 35, 20));

        var flag = town.Owner != null ? PlayerColor : NeutralColor;
        float wave = (int)(time * 3) % 2 == 1 ? 2 : 0;
        ci.DrawLine(c + V(0, -18), c + V(0, -30), Rgb(80, 50, 20), 2);
        Triangle(ci, c + V(1, -30), c + V(12, -27 + wave), c + V(1, -24), flag);
    }

    /// <summary>Couleur d'un monstre selon son niveau : du vert (faible) au rouge sombre (fort).</summary>
    private static Color MonsterColor(int tier) => tier switch
    {
        1 => Rgb(120, 150, 70),
        2 => Rgb(150, 130, 60),
        3 => Rgb(170, 100, 50),
        4 => Rgb(170, 60, 40),
        5 => Rgb(140, 40, 60),
        6 => Rgb(110, 30, 90),
        _ => Rgb(70, 20, 80),
    };

    /// <summary>Une bête stylisée avec cornes et yeux, et l'initiale de la créature.</summary>
    private static void DrawMonster(CanvasItem ci, MonsterGuard guard, Vector2 c, double time)
    {
        var body = MonsterColor(guard.Type.Tier);
        float breath = (float)Math.Sin(time * 3 + guard.Position.X) * 1.0f;
        Ellipse(ci, new Rect2(c + V(-15, 10), 30, 8), new Color(0, 0, 0, 0.5f));
        Ellipse(ci, new Rect2(c + V(-14, -10 - breath), 28, 26 + breath), body);
        Triangle(ci, c + V(-12, -6), c + V(-6, -10), c + V(-14, -20), Rgb(230, 220, 190));
        Triangle(ci, c + V(12, -6), c + V(6, -10), c + V(14, -20), Rgb(230, 220, 190));
        ci.DrawCircle(c + V(-5, -2), 3, Rgb(255, 230, 80));
        ci.DrawCircle(c + V(5, -2), 3, Rgb(255, 230, 80));
        ci.DrawCircle(c + V(-5, -2), 1.2f, Colors.Black);
        ci.DrawCircle(c + V(5, -2), 1.2f, Colors.Black);

        var font = ThemeDB.FallbackFont;
        string letter = guard.Type.Name[..1];
        ci.DrawCircle(c + V(13, 13), 7, Rgb(30, 25, 20));
        ci.DrawString(font, c + V(9, 17), letter, HorizontalAlignment.Left, -1, 11, Colors.White);
    }

    // --------------------------------------------------------------------- héros

    /// <summary>Un cavalier très stylisé avec son étendard.</summary>
    public static void DrawHero(CanvasItem ci, Vector2 c, double time)
    {
        var horse = Rgb(240, 235, 225);
        Ellipse(ci, new Rect2(c + V(-14, 10), 28, 8), new Color(0, 0, 0, 0.5f));
        Ellipse(ci, new Rect2(c + V(-13, -2), 26, 14), horse);
        ci.DrawRect(new Rect2(c + V(7, -10), 6, 12), horse);
        foreach (int dx in new[] { -10, -4, 4, 9 })
            ci.DrawLine(c + V(dx, 10), c + V(dx, 14), Rgb(220, 215, 205), 2);

        ci.DrawRect(new Rect2(c + V(-5, -14), 9, 14), Rgb(60, 60, 160));
        ci.DrawCircle(c + V(0, -17), 4, Rgb(235, 200, 160));

        float wave = (int)(time * 3) % 2 == 1 ? 2 : 0; // l'étendard flotte
        ci.DrawLine(c + V(-9, 2), c + V(-9, -26), Rgb(80, 50, 20), 2);
        Triangle(ci, c + V(-8, -26), c + V(4, -23 + wave), c + V(-8, -18), PlayerColor);
    }

    /// <summary>Marque de chemin : un point, ou une croix pour la destination.</summary>
    public static void DrawPathMarker(CanvasItem ci, Vector2 c, bool reachable, bool isDestination)
    {
        var color = reachable ? Rgb(60, 220, 60) : Rgb(220, 60, 60);
        if (isDestination)
        {
            ci.DrawLine(c + V(-9, -9), c + V(9, 9), color, 4);
            ci.DrawLine(c + V(-9, 9), c + V(9, -9), color, 4);
        }
        else
        {
            ci.DrawCircle(c, 6, Colors.Black);
            ci.DrawCircle(c, 5, color);
        }
    }
}
