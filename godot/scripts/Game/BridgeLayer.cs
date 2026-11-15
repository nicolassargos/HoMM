using System.Collections.Generic;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Les ponts des rivières en images (objects/bridge-{wood|stone}-h.png : un pont d'ouest en est, -v.png : du nord au
/// sud ; -h-small et -v-small : le petit pont d'une route sur un ruisseau), posés sur le sol et sous les ombres et les objets. Chaque image est centrée sur les deux cases du pont et déborde
/// sur les berges. Un pont est de pierre là où la route qui le passe est pavée (comme les routes, la sorte suit la faction
/// de la zone : choix de l'utilisateur, 2026-11-15), de bois ailleurs ; sans image d'une sorte, l'autre la remplace. Sans
/// image pour un sens, le shader du sol dessine encore les planches (<see cref="ShaderFlags"/>).
/// </summary>
public partial class BridgeLayer : Node2D
{
    public AdventureMap Map { get; set; } = null!;

    // Pixel art : les images agrandies restent nettes
    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    public override void _Draw()
    {
        foreach (var (center, image) in Sprites(Map))
        {
            var size = image.GetSize() * MapRenderer.ArtScale;
            DrawTextureRect(image, new Rect2(center - size / 2, size), false);
        }
    }

    /// <summary>Le bit du shader pour les ponts d'ouest en est (1) et du nord au sud (2) qui ont leur image.</summary>
    public static int ShaderFlags => (Image("h", false) != null ? 1 : 0) | (Image("v", false) != null ? 2 : 0)
        | (Image("h-small", false) != null ? 4 : 0) | (Image("v-small", false) != null ? 8 : 0);

    /// <summary>
    /// L'image d'un pont dans ce sens (« h » ou « v », et « -small » pour le petit pont d'une route sur un ruisseau), de
    /// pierre ou de bois, sinon celle de l'autre sorte.
    /// </summary>
    private static Texture2D? Image(string direction, bool stone) =>
        Art.Get($"objects/bridge-{(stone ? "stone" : "wood")}-{direction}") ?? Art.Get($"objects/bridge-{(stone ? "wood" : "stone")}-{direction}");

    /// <summary>Les ponts qui ont une image : le milieu de leurs cases (en pixels de la carte) et l'image.</summary>
    public static List<(Vector2 Center, Texture2D Image)> Sprites(AdventureMap map)
    {
        var list = new List<(Vector2, Texture2D)>();
        bool Bridge(int x, int y) => map.InBounds(new Cell(x, y)) && map.GetRiver(new Cell(x, y)) == RiverKind.Bridge;
        bool River(int x, int y) => map.InBounds(new Cell(x, y)) && map.GetRiver(new Cell(x, y)) >= RiverKind.River;
        bool Stream(int x, int y) => map.InBounds(new Cell(x, y)) && map.GetRiver(new Cell(x, y)) == RiverKind.Stream;
        bool Paved(int x, int y) => map.InBounds(new Cell(x, y)) && map.GetRoadKind(new Cell(x, y)) == RoadKind.Cobbles;
        // de pierre si une case du pont ou de ses abords a une route pavée
        bool Stone(int x0, int y0, int x1, int y1)
        {
            for (int y = y0 - 1; y <= y1 + 1; y++)
            for (int x = x0 - 1; x <= x1 + 1; x++)
                if (Paved(x, y))
                    return true;
            return false;
        }
        const float t = MapRenderer.TileSize;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var middle = new Vector2(x + 0.5f, y + 0.5f) * t;
            if (map.GetRiver(new Cell(x, y)) == RiverKind.Stream && map.HasRoad(new Cell(x, y)))
            {
                // le petit pont d'une route sur un ruisseau : en travers du ruisseau (comme le shader)
                bool down = Stream(x + 1, y) || Stream(x - 1, y);
                if (Image(down ? "v-small" : "h-small", Stone(x, y, x, y)) is { } small)
                    list.Add((middle, small));
                continue;
            }
            if (!Bridge(x, y))
                continue;
            if (Bridge(x + 1, y))
            {
                if (Image("h", Stone(x, y, x + 1, y)) is { } across)
                    list.Add((middle + new Vector2(t / 2, 0), across));
            }
            else if (Bridge(x, y + 1))
            {
                if (Image("v", Stone(x, y, x, y + 1)) is { } down)
                    list.Add((middle + new Vector2(0, t / 2), down));
            }
            else if (!Bridge(x - 1, y) && !Bridge(x, y - 1))
            {
                // un pont d'une seule case : en travers de la rivière (comme le shader)
                bool vertical = River(x + 1, y) || River(x - 1, y);
                if (Image(vertical ? "v" : "h", Stone(x, y, x, y)) is { } image)
                    list.Add((middle, image));
            }
        }
        return list;
    }
}
