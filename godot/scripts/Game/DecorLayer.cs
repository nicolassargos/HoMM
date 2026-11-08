using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le décor posé sur le sol, comme dans le premier Heroes : dans chaque case de forêt, quelques arbres seuls
/// (sapins, chênes, arbres d'automne) semés au hasard ; dans chaque case de montagne, un petit sommet derrière et un grand devant.
/// Ils débordent un peu sur les cases voisines et sont dessinés de haut en bas : chaque arbre ou sommet recouvre
/// ceux de derrière, et les forêts et les chaînes de montagnes forment des masses continues.
/// Les règles ne changent pas : seul le dessin du terrain change (le sol de ces cases est de l'herbe).
/// Dessiné une seule fois, comme le terrain. Le hasard dépend de la case : le même décor revient à chaque partie.
/// </summary>
public partial class DecorLayer : Node2D
{
    public AdventureMap Map { get; set; } = null!;

    private static Texture2D[] Trees =>
        Art.VariantsOf("decor/pine").Concat(Art.VariantsOf("decor/oak")).Concat(Art.VariantsOf("decor/autumn")).ToArray();

    private static Texture2D[] Peaks => Art.VariantsOf("decor/peak");
    /// <summary>Les petits sommets, entre les grands ; sans image, d'autres grands sommets, un peu en retrait.</summary>
    private static Texture2D[] SmallPeaks => Art.VariantsOf("decor/peak-small") is { Length: > 0 } small ? small : Peaks;

    /// <summary>True si ce terrain est dessiné par le décor (le sol de la case est alors de l'herbe).</summary>
    public static bool Covers(TerrainType terrain) => terrain switch
    {
        TerrainType.Forest => Trees.Length > 0,
        TerrainType.Mountain => Peaks.Length > 0,
        _ => false,
    };

    // Pixel art : les images agrandies restent nettes
    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    public override void _Draw()
    {
        foreach (var (foot, image) in Sprites(Map))
        {
            var size = image.GetSize() * MapRenderer.ArtScale;
            DrawTextureRect(image, new Rect2(foot - new Vector2(size.X / 2, size.Y), size), false);
        }
    }

    /// <summary>
    /// Les arbres et les sommets de la carte, repérés par leur pied (le milieu du bas de l'image), triés de haut en bas
    /// (l'ordre du dessin). Toujours les mêmes pour une carte : les ombres (<see cref="ShadowLayer"/>) les relisent.
    /// </summary>
    public static List<(Vector2 Foot, Texture2D Image)> Sprites(AdventureMap map)
    {
        var trees = Trees;
        var peaks = Peaks;
        var smallPeaks = SmallPeaks;
        const int t = MapRenderer.TileSize;
        // les placements ont été réglés pour des cases de 48 pixels de la carte : ils suivent la taille des cases
        const float k = t / 48f;
        int R(int min, int max, Random rng) => (int)MathF.Round(rng.Next(min, max) * k);

        // Chaque image est repérée par son pied (le milieu de son bas), puis tout est trié de haut en bas
        var sprites = new List<(Vector2 Foot, Texture2D Image)>();
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var o = new Vector2(x, y) * t;
            var rng = new Random(Seed(x, y));
            switch (map.GetTerrain(new Cell(x, y)))
            {
                case TerrainType.Forest when trees.Length > 0:
                    // quatre arbres en carré, un peu décalés, et parfois un cinquième n'importe où
                    for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 2; col++)
                        sprites.Add((o + new Vector2((12 + col * 24) * k + R(-6, 7, rng), (20 + row * 22) * k + R(-4, 5, rng)),
                            trees[rng.Next(trees.Length)]));
                    if (rng.Next(2) == 0)
                        sprites.Add((o + new Vector2(R(6, 42, rng), R(14, 48, rng)), trees[rng.Next(trees.Length)]));
                    break;

                case TerrainType.Mountain when peaks.Length > 0:
                    // un sommet derrière, sur un côté, puis le grand devant, qui déborde sur les cases voisines
                    sprites.Add((o + new Vector2((rng.Next(2) == 0 ? 12 : 36) * k, 26 * k + R(-3, 4, rng)),
                        smallPeaks[rng.Next(smallPeaks.Length)]));
                    sprites.Add((o + new Vector2(t / 2 + R(-6, 7, rng), t - 2 * k + R(-2, 3, rng)), peaks[rng.Next(peaks.Length)]));
                    break;
            }
        }

        sprites.Sort((a, b) => a.Foot.Y != b.Foot.Y ? a.Foot.Y.CompareTo(b.Foot.Y) : a.Foot.X.CompareTo(b.Foot.X));
        return sprites;
    }

    /// <summary>Un nombre fixe par case, mélangé pour que les cases voisines n'aient pas le même décor.</summary>
    private static int Seed(int x, int y)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return (int)(h & 0x7fffffff);
    }
}
