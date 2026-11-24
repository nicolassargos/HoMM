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
/// Bleu : dans l'eau des lacs, de la mer et des rivières, la distance à la terre la plus proche, en pixels d'art (de
/// quoi foncer l'eau loin des rives ; 0 sur la terre, <see cref="Deep"/> au plus).
/// </summary>
public static class RiverField
{
    public const int PerCell = 8;

    /// <summary>La distance la plus grande gardée pour les ruisseaux (pixels d'art) : au-delà, « loin ».</summary>
    private const float Far = 32f;

    /// <summary>La distance à la terre la plus grande gardée (pixels d'art) : au-delà, l'eau est aussi profonde.</summary>
    public const float Deep = 96f;

    private const int Channels = 3;

    /// <summary>Le flou de la bande des rivières, en cases : plus il est fort, plus les virages sont larges.</summary>
    private const double Blur = 0.8;

    /// <summary>Les lacs et la mer comptent dans la bande à cette distance d'une rivière (en cases), pour l'embouchure.</summary>
    private const int MouthReach = 3;

    /// <summary>La mare d'une source : de combien elle dépasse la largeur du ruisseau (pixels d'art).</summary>
    private const float Pond = 3f;

    /// <summary>Un ruisseau qui naît au pied d'une montagne ou d'un marais : de combien il est plus étroit à sa naissance (pixels d'art).</summary>
    private const float Thin = 1.8f;

    /// <summary>Les bras du delta : de combien ils sont plus étroits que le ruisseau (pixels d'art).</summary>
    private const float Arm = 1.5f;

    private static readonly Cell[] Sides = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    /// <summary>L'image du champ pour <paramref name="width"/> × <paramref name="height"/> cases de <paramref name="art"/> pixels d'art.</summary>
    /// <paramref name="courses"/> : les tracés des ruisseaux gardés dans la carte, dessinés tels quels (sans eux, le tracé
    /// suit les cases des ruisseaux).
    public static Image Build(int width, int height, Func<Cell, (TerrainType Terrain, RiverKind River)> cellAt, float art,
        IReadOnlyList<StreamCourse>? courses = null)
    {
        int sw = width * PerCell, sh = height * PerCell;
        var terrain = new TerrainType[width, height];
        var river = new RiverKind[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            (terrain[x, y], river[x, y]) = cellAt(new Cell(x, y));
        bool In(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < width && c.Y < height;
        bool Band(Cell c) => In(c) && river[c.X, c.Y] is RiverKind.River or RiverKind.Bridge;

        var data = new float[sw * sh * Channels];
        Bank(data, sw, sh, width, height, terrain, Band);
        Func<Cell, bool> sink = c => In(c) && (terrain[c.X, c.Y] == TerrainType.Water || Band(c));
        if (courses is { Count: > 0 })
            Courses(data, sw, sh, art, courses, sink);
        else
            Streams(data, sw, sh, art, c => In(c) && river[c.X, c.Y] == RiverKind.Stream, sink);
        Depth(data, sw, sh, art, (x, y) => terrain[x, y] == TerrainType.Water || Band(new Cell(x, y)));

        var halves = new Half[data.Length];
        for (int i = 0; i < data.Length; i++)
            halves[i] = (Half)data[i];
        return Image.CreateFromData(sw, sh, false, Image.Format.Rgbh, MemoryMarshal.AsBytes(halves.AsSpan()).ToArray());
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
                data[(sy * sw + sx) * Channels] = sum;
            }
        }
    }

    /// <summary>
    /// La distance aux ruisseaux (vert). Chaque ruisseau est une ligne de cases : on la suit d'un bout à l'autre, on la
    /// prolonge jusqu'au centre de l'eau où elle se jette, on l'arrondit (Chaikin), et une source sans embouchure a sa mare.
    /// </summary>
    private static void Streams(float[] data, int sw, int sh, float art, Func<Cell, bool> stream, Func<Cell, bool> sink)
    {
        for (int i = 1; i < data.Length; i += Channels)
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
                    var before = line.Count > 1 ? (head ? line[1] : line[^2]) : end;
                    Delta(data, sw, sh, art, Center(before), Center(end), mouth, sink);
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

    /// <summary>
    /// La distance aux ruisseaux (vert), d'après leurs tracés : chacun arrondi (Chaikin), une mare à sa source, et au
    /// bout une mare, un delta (là où il se jette dans l'eau) ou rien (un affluent, qui rejoint un autre ruisseau).
    /// </summary>
    private static void Courses(float[] data, int sw, int sh, float art, IReadOnlyList<StreamCourse> courses, Func<Cell, bool> sink)
    {
        for (int i = 1; i < data.Length; i += Channels)
            data[i] = Far;
        foreach (var course in courses)
        {
            var points = course.Points.Select(p => new Vector2((float)p.X * art, (float)p.Y * art)).ToList();
            if (course.End == StreamEnd.Mouth && points.Count >= 3)
            {
                // le delta part de la dernière case de terre, vers la case d'eau du dernier point
                var tip = course.Points[^1];
                var mouth = new Cell((int)Math.Floor(tip.X), (int)Math.Floor(tip.Y));
                var land = course.Points[^2];
                var end = new Vector2(((int)Math.Floor(land.X) + 0.5f) * art, ((int)Math.Floor(land.Y) + 0.5f) * art);
                var before = points[Math.Max(0, points.Count - 2 - RiverFieldGrid)];
                Delta(data, sw, sh, art, before, end, mouth, sink);
            }
            for (int pass = 0; pass < 3; pass++)
                points = Chaikin(points);
            // une source : sa mare ; le pied d'une montagne ou un marais : un filet d'eau qui s'élargit sur une case
            float trickle = art * 1.2f, run = 0;
            for (int i = 0; i + 1 < points.Count; i++)
            {
                float thin = course.Source == StreamSource.Spring ? 0 : Thin * Math.Max(0, 1 - run / trickle);
                Stamp(data, sw, sh, art, points[i], points[i + 1], -thin);
                run += points[i].DistanceTo(points[i + 1]);
            }
            if (course.Source == StreamSource.Spring)
                Stamp(data, sw, sh, art, points[0], points[0], Pond);
            if (course.End == StreamEnd.Pond)
                Stamp(data, sw, sh, art, points[^1], points[^1], Pond);
        }
    }

    /// <summary>Les points d'un tracé par case (environ) : pour reculer d'une case depuis son bout.</summary>
    private const int RiverFieldGrid = 5;

    /// <summary>
    /// Le delta d'un ruisseau : un ou deux bras plus étroits quittent le ruisseau avant la rive et s'ouvrent en éventail
    /// jusqu'à l'eau où il se jette, de part et d'autre de son embouchure (seulement là où l'eau les attend).
    /// </summary>
    private static void Delta(float[] data, int sw, int sh, float art, Vector2 before, Vector2 end, Cell mouth, Func<Cell, bool> sink)
    {
        var target = new Vector2((mouth.X + 0.5f) * art, (mouth.Y + 0.5f) * art);
        var dir = (target - end).Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        var start = before.Lerp(end, 0.15f);
        var shore = end + dir * art * 0.5f;
        uint seed = (uint)(mouth.X * 73856093) ^ (uint)(mouth.Y * 19349663);
        // un bras d'un côté, de l'autre, ou des deux
        int pick = (int)(seed % 3);
        foreach (float sign in new[] { -1f, 1f })
        {
            if (pick == 0 && sign > 0 || pick == 1 && sign < 0)
                continue;
            // pas du côté d'où vient un ruisseau qui tourne : le bras se confondrait avec lui
            if ((before - end).Dot(side * sign) > art * 0.3f)
                continue;
            float spread = art * (0.55f + (seed >> 4) % 5 * 0.04f);
            var tip = shore + dir * art * 0.35f + side * sign * spread;
            var cell = new Cell((int)MathF.Floor(tip.X / art), (int)MathF.Floor(tip.Y / art));
            if (!sink(cell))
                continue;
            var bend = start.Lerp(tip, 0.45f) - side * sign * art * 0.1f;
            var arm = new List<Vector2> { start, bend, tip };
            for (int pass = 0; pass < 2; pass++)
                arm = Chaikin(arm);
            for (int i = 0; i + 1 < arm.Count; i++)
                Stamp(data, sw, sh, art, arm[i], arm[i + 1], -Arm);
        }
    }

    /// <summary>
    /// La distance à la terre (bleu), pour chaque point dans l'eau : deux passes sur la grille des points (en avant puis en
    /// arrière, pas droits et en biais), assez juste pour une eau qui fonce en douceur.
    /// </summary>
    private static void Depth(float[] data, int sw, int sh, float art, Func<int, int, bool> wet)
    {
        float step = art / PerCell, diagonal = step * MathF.Sqrt(2);
        var d = new float[sw * sh];
        for (int sy = 0; sy < sh; sy++)
        for (int sx = 0; sx < sw; sx++)
            d[sy * sw + sx] = wet(sx / PerCell, sy / PerCell) ? Deep : 0;
        float At(int x, int y) => x < 0 || y < 0 || x >= sw || y >= sh ? Deep : d[y * sw + x];
        for (int sy = 0; sy < sh; sy++)
        for (int sx = 0; sx < sw; sx++)
            if (d[sy * sw + sx] > 0)
                d[sy * sw + sx] = Math.Min(d[sy * sw + sx], Math.Min(Math.Min(At(sx - 1, sy) + step, At(sx, sy - 1) + step),
                    Math.Min(At(sx - 1, sy - 1) + diagonal, At(sx + 1, sy - 1) + diagonal)));
        for (int sy = sh - 1; sy >= 0; sy--)
        for (int sx = sw - 1; sx >= 0; sx--)
            if (d[sy * sw + sx] > 0)
                d[sy * sw + sx] = Math.Min(d[sy * sw + sx], Math.Min(Math.Min(At(sx + 1, sy) + step, At(sx, sy + 1) + step),
                    Math.Min(At(sx + 1, sy + 1) + diagonal, At(sx - 1, sy + 1) + diagonal)));
        for (int i = 0; i < d.Length; i++)
            data[i * Channels + 2] = d[i];
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
            int i = (sy * sw + sx) * Channels + 1;
            if (d < data[i])
                data[i] = d;
        }
    }
}
