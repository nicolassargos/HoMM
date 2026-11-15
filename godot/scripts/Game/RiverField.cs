using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Les formes des rivières et des ruisseaux pour le shader du sol, calculées une fois par carte : une image de
/// <see cref="PerCell"/> × <see cref="PerCell"/> points par case, lue en douceur par le shader.
/// Rouge : la part d'eau autour du point, la bande des rivières (et l'eau des lacs ou de la mer où elles se jettent)
/// floutée : son bord, là où elle vaut 0,5, suit des courbes douces au lieu des escaliers de cases.
/// Vert : la distance au milieu du ruisseau le plus proche, en pixels d'art, le long d'une ligne lissée qui passe par les
/// centres de ses cases (des virages arrondis au lieu d'angles droits).
/// </summary>
public static class RiverField
{
    public const int PerCell = 8;

    /// <summary>La distance la plus grande gardée pour les ruisseaux (pixels d'art) : au-delà, « loin ».</summary>
    private const float Far = 32f;

    /// <summary>Le flou de la bande des rivières, en cases : plus il est fort, plus les virages sont larges.</summary>
    private const double Blur = 0.8;

    /// <summary>Les lacs et la mer comptent dans la bande à cette distance d'une rivière (en cases), pour l'embouchure.</summary>
    private const int MouthReach = 3;

    /// <summary>La mare d'une source : de combien elle dépasse la largeur du ruisseau (pixels d'art).</summary>
    private const float Pond = 3f;

    private static readonly Cell[] Sides = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    /// <summary>L'image du champ pour <paramref name="width"/> × <paramref name="height"/> cases de <paramref name="art"/> pixels d'art.</summary>
    public static Image Build(int width, int height, Func<Cell, (TerrainType Terrain, RiverKind River)> cellAt, float art)
    {
        int sw = width * PerCell, sh = height * PerCell;
        var terrain = new TerrainType[width, height];
        var river = new RiverKind[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            (terrain[x, y], river[x, y]) = cellAt(new Cell(x, y));
        bool In(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < width && c.Y < height;
        bool Band(Cell c) => In(c) && river[c.X, c.Y] is RiverKind.River or RiverKind.Bridge;

        var data = new float[sw * sh * 2];
        Bank(data, sw, sh, width, height, terrain, Band);
        Streams(data, sw, sh, art, c => In(c) && river[c.X, c.Y] == RiverKind.Stream,
            c => In(c) && (terrain[c.X, c.Y] == TerrainType.Water || Band(c)));

        var halves = new Half[data.Length];
        for (int i = 0; i < data.Length; i++)
            halves[i] = (Half)data[i];
        return Image.CreateFromData(sw, sh, false, Image.Format.Rgh, MemoryMarshal.AsBytes(halves.AsSpan()).ToArray());
    }

    /// <summary>La bande des rivières, floutée (rouge) : un flou gaussien en deux passes, d'abord par rangée de cases.</summary>
    private static void Bank(float[] data, int sw, int sh, int width, int height, TerrainType[,] terrain, Func<Cell, bool> band)
    {
        var mask = new float[width, height];
        bool any = false;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var c = new Cell(x, y);
            bool wet = band(c);
            if (!wet && terrain[x, y] == TerrainType.Water)
                for (int dy = -MouthReach; dy <= MouthReach && !wet; dy++)
                for (int dx = -MouthReach; dx <= MouthReach && !wet; dx++)
                    wet = band(c.Offset(dx, dy));
            mask[x, y] = wet ? 1 : 0;
            any |= wet;
        }
        if (!any)
            return;

        double sigma = Blur * PerCell;
        int radius = (int)Math.Ceiling(3 * sigma);
        var kernel = new float[2 * radius + 1];
        for (int k = -radius; k <= radius; k++)
            kernel[k + radius] = (float)Math.Exp(-k * k / (2 * sigma * sigma));
        float total = kernel.Sum();
        for (int k = 0; k < kernel.Length; k++)
            kernel[k] /= total;

        // en travers : une rangée de cases donne toutes ses rangées de points (hors de la carte : la case du bord)
        var rows = new float[height, sw];
        var rowWet = new bool[height];
        for (int y = 0; y < height; y++)
        for (int sx = 0; sx < sw; sx++)
        {
            float sum = 0;
            for (int k = -radius; k <= radius; k++)
                sum += kernel[k + radius] * mask[Math.Clamp(sx + k, 0, sw - 1) / PerCell, y];
            rows[y, sx] = sum;
            rowWet[y] |= sum > 0;
        }
        // de haut en bas
        int reach = radius / PerCell + 1;
        for (int sy = 0; sy < sh; sy++)
        {
            int cy = sy / PerCell;
            bool near = false;
            for (int y = Math.Max(0, cy - reach); y <= Math.Min(height - 1, cy + reach) && !near; y++)
                near = rowWet[y];
            if (!near)
                continue;
            for (int sx = 0; sx < sw; sx++)
            {
                float sum = 0;
                for (int k = -radius; k <= radius; k++)
                    sum += kernel[k + radius] * rows[Math.Clamp(sy + k, 0, sh - 1) / PerCell, sx];
                data[(sy * sw + sx) * 2] = sum;
            }
        }
    }

    /// <summary>
    /// La distance aux ruisseaux (vert). Chaque ruisseau est une ligne de cases : on la suit d'un bout à l'autre, on la
    /// prolonge jusqu'au centre de l'eau où elle se jette, on l'arrondit (Chaikin), et une source sans embouchure a sa mare.
    /// </summary>
    private static void Streams(float[] data, int sw, int sh, float art, Func<Cell, bool> stream, Func<Cell, bool> sink)
    {
        for (int i = 1; i < data.Length; i += 2)
            data[i] = Far;
        var cells = new List<Cell>();
        for (int y = 0; y < sh / PerCell; y++)
        for (int x = 0; x < sw / PerCell; x++)
            if (stream(new Cell(x, y)))
                cells.Add(new Cell(x, y));
        var done = new HashSet<Cell>();
        int Links(Cell c) => Sides.Count(d => stream(c.Offset(d.X, d.Y)));
        Vector2 Center(Cell c) => new((c.X + 0.5f) * art, (c.Y + 0.5f) * art);

        // d'abord depuis les bouts, puis les boucles qui restent
        foreach (var first in cells.Where(c => Links(c) <= 1).Concat(cells))
        {
            if (done.Contains(first))
                continue;
            var line = new List<Cell> { first };
            done.Add(first);
            for (var cell = first; ;)
            {
                var next = Sides.Select(d => cell.Offset(d.X, d.Y)).FirstOrDefault(n => stream(n) && !done.Contains(n), new Cell(-1, -1));
                if (next.X < 0)
                    break;
                line.Add(next);
                done.Add(next);
                cell = next;
            }
            var points = line.Select(Center).ToList();
            var ponds = new List<Vector2>();
            // chaque bout : son embouchure, sinon une mare (la source)
            foreach (bool head in new[] { true, false })
            {
                var end = head ? line[0] : line[^1];
                var mouth = Sides.Select(d => end.Offset(d.X, d.Y)).FirstOrDefault(sink, new Cell(-1, -1));
                if (mouth.X >= 0)
                {
                    if (head)
                        points.Insert(0, Center(mouth));
                    else
                        points.Add(Center(mouth));
                }
                else
                    ponds.Add(Center(end));
                if (line.Count == 1)
                    break;
            }
            for (int pass = 0; pass < 3; pass++)
                points = Chaikin(points);
            for (int i = 0; i + 1 < points.Count; i++)
                Stamp(data, sw, sh, art, points[i], points[i + 1], 0);
            if (points.Count == 1)
                Stamp(data, sw, sh, art, points[0], points[0], 0);
            foreach (var pond in ponds)
                Stamp(data, sw, sh, art, pond, pond, Pond);
        }
    }

    /// <summary>Une ligne aux coins coupés (Chaikin) : chaque segment garde son premier et son dernier quart, les bouts restent.</summary>
    private static List<Vector2> Chaikin(List<Vector2> points)
    {
        if (points.Count < 3)
            return points;
        var result = new List<Vector2> { points[0] };
        for (int i = 0; i + 1 < points.Count; i++)
        {
            var (a, b) = (points[i], points[i + 1]);
            if (i > 0)
                result.Add(a.Lerp(b, 0.25f));
            if (i + 2 < points.Count)
                result.Add(a.Lerp(b, 0.75f));
        }
        result.Add(points[^1]);
        return result;
    }

    /// <summary>La distance des points proches au segment [a, b], moins <paramref name="grow"/>, si elle est plus petite.</summary>
    private static void Stamp(float[] data, int sw, int sh, float art, Vector2 a, Vector2 b, float grow)
    {
        float step = art / PerCell;
        int x0 = Math.Max(0, (int)((Math.Min(a.X, b.X) - Far) / step)), x1 = Math.Min(sw - 1, (int)((Math.Max(a.X, b.X) + Far) / step));
        int y0 = Math.Max(0, (int)((Math.Min(a.Y, b.Y) - Far) / step)), y1 = Math.Min(sh - 1, (int)((Math.Max(a.Y, b.Y) + Far) / step));
        var ab = b - a;
        float length2 = Math.Max(ab.LengthSquared(), 1e-6f);
        for (int sy = y0; sy <= y1; sy++)
        for (int sx = x0; sx <= x1; sx++)
        {
            var q = new Vector2((sx + 0.5f) * step, (sy + 0.5f) * step);
            float t = Math.Clamp((q - a).Dot(ab) / length2, 0, 1);
            float d = (q - (a + ab * t)).Length() - grow;
            int i = (sy * sw + sx) * 2 + 1;
            if (d < data[i])
                data[i] = d;
        }
    }
}
