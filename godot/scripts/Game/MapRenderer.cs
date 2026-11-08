using System;
using System.Collections.Generic;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Dessin des tuiles, des objets et du héros. Chaque élément utilise son image quand elle existe
/// (voir <see cref="Art"/>), sinon un dessin par formes (aucune image nécessaire).
/// Toutes les méthodes s'appellent depuis le _Draw() d'un CanvasItem.
/// </summary>
public static class MapRenderer
{
    /// <summary>
    /// La taille d'une case en pixels d'art (32, comme Heroes 2 ; choix de l'utilisateur, 2026-11-07 : 24 avant), et la
    /// taille d'une case sur la carte : chaque pixel d'art y compte pour 2 (la caméra agrandit ensuite ×1 ou ×1,5).
    /// </summary>
    public const int ArtTile = 32;
    public const int ArtScale = 2;
    public const int TileSize = ArtTile * ArtScale;
    private const int T = TileSize;

    public static readonly Color PlayerColor = Rgb(200, 40, 40);

    /// <summary>Les couleurs des joueurs, dans l'ordre (Rouge, Bleu, Vert, Orange : voir les noms dans GameState).</summary>
    public static readonly Color[] PlayerColors = { PlayerColor, Rgb(50, 95, 215), Rgb(40, 160, 60), Rgb(230, 140, 30) };

    /// <summary>La couleur d'un propriétaire (drapeaux, héros, carte réduite) ; neutre s'il n'y en a pas.</summary>
    public static Color ColorOf(Player? owner) =>
        owner == null ? NeutralColor : PlayerColors[(owner.Id - 1 + PlayerColors.Length) % PlayerColors.Length];
    public static readonly Color FogColor = Rgb(12, 12, 16);

    public static Color Rgb(int r, int g, int b) => Color.Color8((byte)r, (byte)g, (byte)b);

    public static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => Rgb(86, 150, 60),
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
        ResourceType.Mercury => Rgb(190, 200, 215),
        ResourceType.Crystal => Rgb(120, 220, 240),
        ResourceType.Sulfur => Rgb(230, 210, 60),
        _ => Colors.Magenta,
    };

    private static Color Vary(Color c, Random rng, int amount)
    {
        float d = rng.Next(-amount, amount + 1) / 255f;
        return new Color(Math.Clamp(c.R + d, 0, 1), Math.Clamp(c.G + d, 0, 1), Math.Clamp(c.B + d, 0, 1));
    }

    private static Vector2 V(float x, float y) => new(x, y);

    /// <summary>Les tailles en pixels fixes (anneau, marques du chemin) ont été réglées pour des cases de 48 : elles suivent la case.</summary>
    private const float K = T / 48f;

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

    /// <summary>La couleur des routes (minicarte, dessin sans images).</summary>
    public static readonly Color RoadColor = Rgb(170, 140, 95);

    /// <summary>Une route sur une case, dessinée sans images (quand le shader du sol n'est pas disponible).</summary>
    public static void DrawRoad(CanvasItem ci, Vector2 o, Random rng)
    {
        ci.DrawRect(new Rect2(o + V(T / 4, T / 4), T / 2, T / 2), RoadColor);
        for (int i = 0; i < 4; i++)
            ci.DrawCircle(o + V(rng.Next(T / 4, 3 * T / 4), rng.Next(T / 4, 3 * T / 4)), 2, Rgb(140, 115, 80));
    }

    public static void DrawTile(CanvasItem ci, TerrainType terrain, Vector2 o, Random rng)
    {
        // sous les arbres et les montagnes, de l'herbe (le décor est dessiné par-dessus, par DecorLayer)
        if (DecorLayer.Covers(terrain))
            terrain = TerrainType.Grass;
        if (Art.Terrain(terrain, rng.Next()) is { } image)
        {
            ci.DrawTextureRect(image, new Rect2(o, T, T), false);
            // en attendant des forêts dessinées comme objets : des sapins par-dessus l'image, pour qu'on les reconnaisse
            if (terrain == TerrainType.Forest)
                DrawTrees(ci, o, rng, outlined: true);
            return;
        }
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

            case TerrainType.Forest:
                DrawTrees(ci, o, rng, outlined: false);
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

    /// <summary>Trois sapins sur une case ; <paramref name="outlined"/> les détoure de sombre (sur une image de forêt).</summary>
    private static void DrawTrees(CanvasItem ci, Vector2 o, Random rng, bool outlined)
    {
        for (int i = 0; i < 3; i++)
        {
            var p = o + V(rng.Next(8, T - 8), rng.Next(14, T - 4));
            if (outlined)
            {
                ci.DrawRect(new Rect2(p + V(-3, -1), 6, 8), Rgb(25, 20, 12));
                Triangle(ci, p + V(0, -21), p + V(-12, 3), p + V(12, 3), Rgb(12, 30, 15));
            }
            ci.DrawRect(new Rect2(p + V(-2, 0), 4, 6), Rgb(80, 55, 30));
            var green = outlined ? Rgb(50, 115, 50) : Rgb(30, 80, 35);
            Triangle(ci, p + V(0, -18), p + V(-10, 2), p + V(10, 2), Vary(green, rng, 15));
        }
    }

    // ---------------------------------------------------------------- ressources

    public static void DrawResourceIcon(CanvasItem ci, ResourceType resource, Vector2 c, float r = 7)
    {
        if (Art.Get($"icons/{Art.Name(resource)}") is { } image)
        {
            Art.DrawSprite(ci, image, new Rect2(c - V(2 * r, 2 * r), 4 * r, 4 * r));
            return;
        }
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

    /// <summary>L'image d'un objet de la carte, s'il en a une.</summary>
    private static Texture2D? ObjectImage(MapObject obj) => obj switch
    {
        ResourcePile pile => Art.Get($"objects/pile-{Art.Name(pile.Resource)}"),
        TreasureChest => Art.Get("objects/chest"),
        Obelisk => Art.Get("objects/obelisk"),
        Shrine shrine => Art.Get($"objects/shrine-{shrine.Stat.ToString().ToLowerInvariant()}"),
        SageHut => Art.Get("objects/sage"),
        WeeklyProducer { IsWaterWheel: true } => Art.Get("objects/water-wheel"),
        WeeklyProducer => Art.Get("objects/windmill"),
        ObservationTower => Art.Get("objects/tower"),
        Mine mine => Art.Get($"objects/mine-{Art.Name(mine.Resource)}"),
        MonsterGuard guard => Art.Get($"creatures/{guard.Type.Id}"),
        Town town => Art.Town(town.Faction),
        _ => null,
    };

    public static void DrawObject(CanvasItem ci, MapObject obj, Vector2 o, double time)
    {
        var c = o + V(T / 2f, T / 2f);
        if (ObjectImage(obj) is { } image)
        {
            var area = ObjectArea(obj, o);
            var drawn = DrawArt(ci, image, area);
            // Le drapeau du propriétaire reste dessiné par-dessus (en haut à gauche de la mine)
            if (obj is Mine mine)
                DrawFlag(ci, area.Position + new Vector2(0.3f * T, 0.45f * T), ColorOf(mine.Owner), 0);
            else if (obj is Town town)
            {
                // planté sur le dessin (sa tour ou son rempart de gauche), pas dans le fond transparent au-dessus
                float top = Art.OpaqueTop(image, TownFlagColumn);
                DrawFlag(ci, drawn.Position + new Vector2(TownFlagColumn * drawn.Size.X, top * drawn.Size.Y + 3), ColorOf(town.Owner), time);
            }
            return;
        }
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

            case Shrine or SageHut or WeeklyProducer or ObservationTower:
                DrawSite(ci, obj, c, time);
                break;

            case Obelisk:
                // une colonne de pierre avec une rune bleue
                ci.DrawColoredPolygon(new[] { c + V(-5, -20), c + V(5, -20), c + V(8, 14), c + V(-8, 14) }, Rgb(130, 125, 120));
                ci.DrawColoredPolygon(new[] { c + V(-5, -20), c + V(0, -26), c + V(5, -20) }, Rgb(160, 155, 150));
                ci.DrawRect(new Rect2(c + V(-11, 14), 22, 5), Rgb(100, 95, 90));
                ci.DrawRect(new Rect2(c + V(-2, -10), 4, 12), Rgb(90, 170, 230));
                break;

            case Mine { Large: true } mine:
                // sans image : une colline de 3×2 cases, l'entrée sombre au milieu en bas (dessinée en fractions de case)
                var hill = o + new Vector2(-T, -T);
                Triangle(ci, hill + new Vector2(0.1f * T, 2 * T), hill + new Vector2(1.5f * T, 0.15f * T), hill + new Vector2(2.9f * T, 2 * T), Rgb(90, 80, 70));
                Ellipse(ci, new Rect2(o + new Vector2(0.3f * T, 0.35f * T), 0.4f * T, 0.55f * T), Rgb(20, 15, 10));
                DrawResourceIcon(ci, mine.Resource, hill + new Vector2(2.3f * T, 1.6f * T), 0.15f * T);
                DrawFlag(ci, hill + new Vector2(0.3f * T, 0.45f * T), ColorOf(mine.Owner), 0);
                break;

            case Mine mine:
                Triangle(ci, c + V(-20, 16), c + V(0, -14), c + V(20, 16), Rgb(90, 80, 70));
                Ellipse(ci, new Rect2(c + V(-7, 2), 14, 14), Rgb(20, 15, 10));
                DrawResourceIcon(ci, mine.Resource, c + V(13, 12), 6);
                DrawFlag(ci, c + V(-14, -2), ColorOf(mine.Owner), 0);
                break;

            case MonsterGuard guard:
                DrawMonster(ci, guard, c, time);
                break;

            case Town town:
                // sans image : un château de 3×3 cases, posé en bas de la ville, sur l'entrée (o, au milieu de la rangée du bas)
                DrawTown(ci, town, o + V(T / 2f, -T / 2f), time);
                break;
        }
    }

    /// <summary>La surface d'un objet dont l'image se pose en bas (voir <see cref="DrawArt"/>), l'entrée étant en <paramref name="o"/>.</summary>
    private static Rect2 ObjectArea(MapObject obj, Vector2 o)
    {
        // Une ville occupe 5×4 cases (3×3 dans les anciennes sauvegardes), une mine 3×2 : l'entrée (o) est au milieu de la
        // rangée du bas. L'image est posée en bas de cette surface, centrée, à ×2 (une ville de 160 × 128 la couvre juste).
        return obj switch
        {
            Town { Large: true } => new Rect2(o + V(-2 * T, -3 * T), 5 * T, 4 * T),
            Town => new Rect2(o + V(-T, -2 * T), 3 * T, 3 * T),
            Mine { Large: true } => new Rect2(o + V(-T, -T), 3 * T, 2 * T),
            _ => new Rect2(o, T, T),
        };
    }

    /// <summary>L'image d'un objet et l'endroit où elle est dessinée (pour son ombre) ; null s'il est dessiné par formes.</summary>
    public static (Texture2D Image, Rect2 Rect)? ObjectArt(MapObject obj, Vector2 o) =>
        ObjectImage(obj) is { } image ? (image, ArtRect(image, ObjectArea(obj, o))) : null;

    /// <summary>L'image du héros posé sur la case <paramref name="o"/> et l'endroit où elle est dessinée (pour son ombre).</summary>
    public static (Texture2D Image, Rect2 Rect)? HeroArt(Player? owner, Vector2 o) =>
        HeroImage(owner) is { } image ? (image, ArtRect(image, HeroArea(o + V(T / 2f, T / 2f + 4)))) : null;

    /// <summary>La case où se pose l'image du héros, <paramref name="c"/> étant le point où il est dessiné.</summary>
    private static Rect2 HeroArea(Vector2 c) => new(c - V(T / 2f, T / 2f + 4), T, T);

    /// <summary>
    /// Dessine une image de la carte à sa taille : un pixel d'art y vaut <see cref="ArtScale"/> pixels de la carte (les
    /// pixels ont la même taille partout). Elle est posée sur le bas de <paramref name="area"/> et centrée ; une image plus
    /// grande que la zone en déborde (vers le haut et sur les côtés).
    /// </summary>
    private static Rect2 DrawArt(CanvasItem ci, Texture2D image, Rect2 area)
    {
        var drawn = ArtRect(image, area);
        ci.DrawTextureRect(image, drawn, false);
        return drawn;
    }

    /// <summary>Où <see cref="DrawArt"/> dessine une image dans <paramref name="area"/>.</summary>
    private static Rect2 ArtRect(Texture2D image, Rect2 area)
    {
        var size = image.GetSize() * ArtScale;
        var position = new Vector2(MathF.Floor(area.Position.X + (area.Size.X - size.X) / 2), area.End.Y - size.Y);
        return new Rect2(position, size);
    }

    /// <summary>
    /// Les lieux sans image (en attendant celles de la session images) : un dessin par formes, réglé pour des cases de 48
    /// pixels et mis à l'échelle de la case.
    /// </summary>
    private static void DrawSite(CanvasItem ci, MapObject obj, Vector2 c, double time)
    {
        float k = T / 48f;
        Vector2 P(float x, float y) => c + k * V(x, y);
        Rect2 R(float x, float y, float w, float h) => new(P(x, y), k * w, k * h);
        var stone = Rgb(135, 130, 122);
        var darkStone = Rgb(95, 90, 85);
        var wood = Rgb(120, 80, 40);
        switch (obj)
        {
            case Shrine shrine:
                // un autel de pierre et sa flamme : rouge (attaque), bleue (défense), violette (puissance), verte (savoir)
                var flame = shrine.Stat switch
                {
                    HeroStat.Attack => Rgb(230, 70, 40),
                    HeroStat.Defense => Rgb(70, 130, 230),
                    HeroStat.Power => Rgb(170, 80, 220),
                    _ => Rgb(80, 200, 90),
                };
                ci.DrawRect(R(-14, 6, 28, 10), darkStone);
                ci.DrawRect(R(-10, -4, 20, 11), stone);
                float flicker = (int)(time * 4) % 2 == 0 ? 0 : 2;
                Triangle(ci, P(-7, -4), P(0, -20 - flicker), P(7, -4), flame);
                Triangle(ci, P(-3, -4), P(0, -12), P(3, -4), Rgb(255, 240, 180));
                break;
            case SageHut:
                // une hutte de bois au toit de chaume, une fenêtre éclairée
                ci.DrawRect(R(-13, -2, 26, 18), wood);
                Triangle(ci, P(-18, -1), P(0, -20), P(18, -1), Rgb(200, 170, 80));
                ci.DrawRect(R(-4, 6, 8, 10), Rgb(50, 30, 15));
                ci.DrawRect(R(6, 2, 5, 5), Rgb(250, 220, 120));
                break;
            case WeeklyProducer { IsWaterWheel: true }:
                // une maisonnette et sa roue
                ci.DrawRect(R(-2, -6, 18, 22), stone);
                Triangle(ci, P(-4, -5), P(7, -16), P(18, -5), Rgb(150, 60, 40));
                ci.DrawCircle(P(-8, 6), 11 * k, wood);
                ci.DrawCircle(P(-8, 6), 6 * k, Rgb(80, 140, 200));
                float turn = (float)(time * 1.5);
                for (int i = 0; i < 4; i++)
                {
                    var d = new Vector2(Mathf.Cos(turn + i * Mathf.Pi / 4), Mathf.Sin(turn + i * Mathf.Pi / 4)) * 11 * k;
                    ci.DrawLine(P(-8, 6) - d, P(-8, 6) + d, Rgb(70, 45, 20), 2 * k);
                }
                break;
            case WeeklyProducer:
                // un moulin : une tour claire et ses ailes qui tournent
                ci.DrawColoredPolygon(new[] { P(-8, -10), P(8, -10), P(11, 16), P(-11, 16) }, Rgb(215, 200, 170));
                Triangle(ci, P(-10, -10), P(0, -19), P(10, -10), Rgb(150, 60, 40));
                ci.DrawRect(R(-3, 8, 6, 8), Rgb(70, 45, 20));
                float spin = (float)(time * 1.2);
                for (int i = 0; i < 4; i++)
                {
                    var d = new Vector2(Mathf.Cos(spin + i * Mathf.Pi / 2), Mathf.Sin(spin + i * Mathf.Pi / 2)) * 17 * k;
                    ci.DrawLine(P(0, -8), P(0, -8) + d, Rgb(240, 235, 220), 3 * k);
                }
                break;
            case ObservationTower:
                // une haute tour de pierre crénelée, plus haute que sa case
                ci.DrawRect(R(-9, -26, 18, 42), stone);
                for (int i = 0; i < 3; i++)
                    ci.DrawRect(R(-11 + i * 8, -32, 6, 6), stone);
                ci.DrawRect(R(-11, -27, 22, 3), darkStone);
                ci.DrawRect(R(-2, -18, 4, 7), Rgb(40, 35, 30));
                ci.DrawRect(R(-4, 6, 8, 10), Rgb(50, 30, 15));
                break;
        }
    }

    public static readonly Color NeutralColor = Rgb(200, 200, 200);

    /// <summary>Où se plante le drapeau du propriétaire sur l'image d'une ville : à 30 % de sa largeur.</summary>
    private const float TownFlagColumn = 0.3f;

    /// <summary>Un drapeau sur son mât, planté en <paramref name="foot"/> (il flotte si <paramref name="time"/> avance).</summary>
    private static void DrawFlag(CanvasItem ci, Vector2 foot, Color color, double time)
    {
        // réglé pour des cases de 48 : à l'échelle de la case
        float k = T / 48f;
        float wave = (int)(time * 3) % 2 == 1 ? 3 : 0;
        ci.DrawLine(foot, foot + k * V(0, -18), Rgb(60, 40, 20), 2 * k);
        Triangle(ci, foot + k * V(1, -18), foot + k * V(12, -14 + wave), foot + k * V(1, -10), color);
    }

    /// <summary>
    /// Un château sur 3×3 cases (<paramref name="c"/> en est le centre) : remparts, quatre tours,
    /// un donjon, la porte sur la case d'entrée (milieu du bas), et le drapeau du propriétaire.
    /// </summary>
    private static void DrawTown(CanvasItem ci, Town town, Vector2 c, double time)
    {
        var wall = Rgb(185, 175, 160);
        var dark = Rgb(120, 112, 100);
        var roof = Rgb(150, 50, 45);
        const float h = 1.5f * T; // demi-côté du carré de 3×3 cases

        Ellipse(ci, new Rect2(c + V(-h + 2, h - 18), 2 * h - 4, 22), new Color(0, 0, 0, 0.4f));
        ci.DrawRect(new Rect2(c + V(-h + 10, -h + 30), 2 * h - 20, 2 * h - 44), wall);       // cour et remparts
        ci.DrawRect(new Rect2(c + V(-h + 10, -h + 30), 2 * h - 20, 2 * h - 44), dark, false, 2);

        // Quatre tours aux coins, avec un toit pointu
        foreach (var corner in new[] { V(-h + 6, -h + 22), V(h - 26, -h + 22), V(-h + 6, h - 50), V(h - 26, h - 50) })
        {
            ci.DrawRect(new Rect2(c + corner, 20, 34), dark);
            Triangle(ci, c + corner + V(-3, 0), c + corner + V(10, -16), c + corner + V(23, 0), roof);
        }

        // Le donjon au centre
        ci.DrawRect(new Rect2(c + V(-20, -h + 20), 40, 64), wall);
        ci.DrawRect(new Rect2(c + V(-20, -h + 20), 40, 64), dark, false, 2);
        Triangle(ci, c + V(-24, -h + 20), c + V(0, -h - 4), c + V(24, -h + 20), roof);
        foreach (float x in new[] { -12f, 4f })
            ci.DrawRect(new Rect2(c + V(x, -h + 34), 8, 12), Rgb(40, 30, 25));             // fenêtres

        // La porte, sur la case d'entrée (milieu de la rangée du bas)
        Ellipse(ci, new Rect2(c + V(-14, h - 44), 28, 26), Rgb(50, 35, 20));
        ci.DrawRect(new Rect2(c + V(-14, h - 31), 28, 17), Rgb(50, 35, 20));
        ci.DrawRect(new Rect2(c + V(-10, h - 14), 20, 14), Rgb(150, 125, 85));             // chemin d'accès

        var flag = ColorOf(town.Owner);
        float wave = (int)(time * 3) % 2 == 1 ? 3 : 0;
        ci.DrawLine(c + V(0, -h - 4), c + V(0, -h - 26), Rgb(80, 50, 20), 2);
        Triangle(ci, c + V(1, -h - 26), c + V(20, -h - 21 + wave), c + V(1, -h - 15), flag);
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
        ci.DrawString(font, c + V(9, 17), letter, HorizontalAlignment.Left, -1, Ui.TextSize(11), Colors.White);
    }

    // --------------------------------------------------------------------- héros

    private static readonly Dictionary<int, Texture2D?> HeroImages = new();

    /// <summary>
    /// L'image du héros aux couleurs de son joueur : son étendard rouge (et les rouges de sa selle) prend la teinte du
    /// joueur ; le joueur rouge garde l'image telle quelle.
    /// </summary>
    public static Texture2D? HeroImage(Player? owner)
    {
        int id = owner?.Id ?? 1;
        if (HeroImages.TryGetValue(id, out var cached))
            return cached;
        Texture2D? texture = null;
        if (id == 1 || owner == null)
            texture = Art.Get("heroes/hero");
        else if (Art.GetImage("heroes/hero") is { } source)
        {
            var image = (Image)source.Duplicate();
            image.Convert(Image.Format.Rgba8);
            float hue = ColorOf(owner).H;
            for (int y = 0; y < image.GetHeight(); y++)
            for (int x = 0; x < image.GetWidth(); x++)
            {
                var p = image.GetPixel(x, y);
                if (p.A > 0.5f && p.S > 0.45f && (p.H < 0.05f || p.H > 0.93f))
                    image.SetPixel(x, y, Color.FromHsv(hue, p.S, p.V, p.A));
            }
            texture = ImageTexture.CreateFromImage(image);
        }
        HeroImages[id] = texture;
        return texture;
    }

    /// <summary>Un cavalier très stylisé avec son étendard, aux couleurs de son joueur.</summary>
    public static void DrawHero(CanvasItem ci, Vector2 c, double time, Player? owner = null)
    {
        if (HeroImage(owner) is { } image)
        {
            DrawArt(ci, image, HeroArea(c));
            return;
        }
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
        Triangle(ci, c + V(-8, -26), c + V(4, -23 + wave), c + V(-8, -18), ColorOf(owner));
    }

    /// <summary>L'anneau doré (légèrement pulsant) sous le héros sélectionné.</summary>
    public static void DrawSelection(CanvasItem ci, Vector2 c, double time)
    {
        float pulse = 0.75f + 0.25f * (float)Math.Sin(time * 4);
        var gold = new Color(1f, 0.85f, 0.3f, pulse);
        var points = new Vector2[25];
        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Tau * i / (points.Length - 1);
            points[i] = c + new Vector2(Mathf.Cos(angle) * 19 * K, Mathf.Sin(angle) * 7 * K);
        }
        ci.DrawPolyline(points, gold, 2.5f * K);
    }

    /// <summary>Marque de chemin : un point, ou une croix pour la destination.</summary>
    public static void DrawPathMarker(CanvasItem ci, Vector2 c, bool reachable, bool isDestination)
    {
        var color = reachable ? Rgb(60, 220, 60) : Rgb(220, 60, 60);
        if (isDestination)
        {
            ci.DrawLine(c + V(-9, -9) * K, c + V(9, 9) * K, color, 4 * K);
            ci.DrawLine(c + V(-9, 9) * K, c + V(9, -9) * K, color, 4 * K);
        }
        else
        {
            ci.DrawCircle(c, 6 * K, Colors.Black);
            ci.DrawCircle(c, 5 * K, color);
        }
    }
}
