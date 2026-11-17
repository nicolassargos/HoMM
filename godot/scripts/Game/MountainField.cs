using System;
using System.Runtime.InteropServices;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le pied des montagnes pour le shader du sol (2026-11-16) : la distance de chaque point à la case de montagne la plus
/// proche, en cases (0 dans une case de montagne, <see cref="Far"/> au plus), <see cref="PerCell"/> × <see cref="PerCell"/>
/// points par case, lue en douceur par le shader ; dans le vert, la profondeur dans la montagne (la distance à la case
/// libre la plus proche, 2026-11-17 : le sol sombre entre les massifs, loin du bord). Seulement pour le dessin.
/// </summary>
public static class MountainField
{
    public const int PerCell = 4;

    /// <summary>La distance la plus grande gardée, en cases : au-delà, « loin ».</summary>
    private const int Far = 3;

    public static Image Build(int width, int height, Func<Cell, bool> mountain)
    {
        var rock = new bool[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            rock[x, y] = mountain(new Cell(x, y));
        int sw = width * PerCell, sh = height * PerCell;
        var halves = new Half[sw * sh * 2];
        for (int sy = 0; sy < sh; sy++)
        for (int sx = 0; sx < sw; sx++)
        {
            // le point en cases ; la distance au carré de chaque case de montagne proche (pas à son centre)
            float px = (sx + 0.5f) / PerCell, py = (sy + 0.5f) / PerCell;
            int cx = (int)px, cy = (int)py;
            float best = Far, depth = Far;
            for (int y = Math.Max(0, cy - Far); y <= Math.Min(height - 1, cy + Far); y++)
            for (int x = Math.Max(0, cx - Far); x <= Math.Min(width - 1, cx + Far); x++)
            {
                float dx = MathF.Max(MathF.Abs(px - (x + 0.5f)) - 0.5f, 0f), dy = MathF.Max(MathF.Abs(py - (y + 0.5f)) - 0.5f, 0f);
                if (rock[x, y])
                    best = MathF.Min(best, MathF.Sqrt(dx * dx + dy * dy));
                else
                    depth = MathF.Min(depth, MathF.Sqrt(dx * dx + dy * dy));
            }
            halves[(sy * sw + sx) * 2] = (Half)best;
            halves[(sy * sw + sx) * 2 + 1] = (Half)depth;
        }
        return Image.CreateFromData(sw, sh, false, Image.Format.Rgh, MemoryMarshal.AsBytes(halves.AsSpan()).ToArray());
    }
}
