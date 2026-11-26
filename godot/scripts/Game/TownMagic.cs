using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// La magie animée autour d'une tour de la scène d'une ville (<see cref="TownSceneMagic"/>) : des anneaux de runes qui
/// tournent, des rubans verts qui montent en spirale, des cristaux qui flottent, des livres et des crânes en orbite.
/// Deux couches par tour : celle de derrière (le côté loin de nous) est posée avant l'image de la tour, qui la cache
/// en partie, celle de devant après. Dessinée en pixels d'art, comme la scène.
/// </summary>
public partial class TownMagic : Control
{
    private static readonly Color Edge = new(0.5f, 0.82f, 0.56f), Glyph = new(0.7f, 0.97f, 0.68f),
        Band = new(0.16f, 0.4f, 0.22f, 0.75f), Ribbon = new(0.6f, 0.95f, 0.62f), RibbonTail = new(0.32f, 0.66f, 0.4f);
    private const float Back = 0.55f;  // la clarté de ce qui passe derrière la tour

    private readonly TownSceneMagic _magic;
    private readonly bool _front;
    private readonly Dictionary<string, Texture2D> _images;
    private readonly HashSet<(int, int)> _drawn = new();
    private float _time;

    /// <param name="front">la couche de devant (sinon celle de derrière)</param>
    /// <param name="image">l'image d'un nom de <see cref="TownSceneMagic"/> (cristal, livre…)</param>
    public TownMagic(TownSceneMagic magic, bool front, Func<string, Texture2D> image)
    {
        (_magic, _front) = (magic, front);
        _images = (magic.Floats ?? new()).Select(f => f.Image).Concat(magic.Orbiters ?? new()).Distinct()
            .ToDictionary(n => n, image);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        _drawn.Clear();
        foreach (var ring in _magic.Rings ?? new())
            Ring(ring[0], ring[1], ring[2], ring[3], ring[4]);
        foreach (var (swirl, n) in (_magic.Swirls ?? new()).Select((s, n) => (s, n)))
            Swirl(swirl, n);
        if (_front)
            foreach (var (f, n) in (_magic.Floats ?? new()).Select((f, n) => (f, n)))
            {
                // le cristal monte et descend d'un pixel ou deux, sa lueur respire
                float bob = MathF.Round(1.5f * MathF.Sin(_time * 1.6f + n * 2.1f));
                float glow = 0.92f + 0.14f * MathF.Sin(_time * 2.3f + n * 1.3f);
                Image(_images[f.Image], new Vector2(f.X, f.Y + bob), new Color(glow, glow, glow));
            }
        if (_magic.Orbit is { } o && _magic.Orbiters is { Count: > 0 } orbiters)
            for (int k = 0; k < orbiters.Count; k++)
            {
                float a = _time * MathF.Tau / 24f + k * MathF.Tau / orbiters.Count;
                bool front = MathF.Sin(a) > 0;
                if (front != _front)
                    continue;
                var texture = _images[orbiters[k]];
                var at = new Vector2(MathF.Round(o[0] + o[2] * MathF.Cos(a)), MathF.Round(o[1] + o[3] * MathF.Sin(a)))
                         - (texture.GetSize() / 2).Round();
                float light = front ? 1 : 0.7f;
                Image(texture, at, new Color(light, light, light));
            }
    }

    /// <summary>
    /// Un anneau de runes : une bande elliptique, un trait clair en haut et en bas, des runes entre les deux qui défilent
    /// tout autour ; la moitié du haut de l'ellipse est derrière la tour.
    /// </summary>
    private void Ring(int cx, int cy, int rx, int ry, int h)
    {
        int samples = (int)(MathF.Tau * Math.Max(rx, ry) * 2);
        float pulse = 0.85f + 0.15f * MathF.Sin(_time * 1.7f);
        for (int i = 0; i < samples; i++)
        {
            float a = i * MathF.Tau / samples;
            bool front = MathF.Sin(a) > 0;
            if (front != _front)
                continue;
            int x = (int)MathF.Round(cx + rx * MathF.Cos(a));
            int top = (int)MathF.Round(cy + ry * MathF.Sin(a) - h / 2f);
            float light = (front ? 1 : Back) * pulse;
            // les runes : des cellules de 3 pixels le long de l'anneau, allumées au hasard, qui tournent
            float along = a * (rx + ry) / 2 - _time * 6;
            int cell = (int)MathF.Floor(along / 3);
            for (int row = 0; row < h; row++)
            {
                Color c = row == 0 || row == h - 1 ? Edge
                    : Noise(cell, row * 7 + (int)MathF.Floor(along) % 3) > 0.45f ? Glyph : Band;
                Dot(x, top + row, Lit(c, light));
            }
        }
    }

    /// <summary>
    /// Des rubans de magie qui montent en spirale autour de la tour, plus larges en bas ; chacun naît en bas, monte en
    /// tournant, sa traîne pâlit derrière lui, et il s'efface en haut.
    /// </summary>
    private void Swirl(int[] s, int n)
    {
        int cx = s[0], top = s[1], bottom = s[2];
        float rTop = s[3], rBottom = s[4], height = bottom - top, length = 44;
        const int Wisps = 4;
        for (int w = 0; w < Wisps; w++)
        {
            float head = bottom - ((_time * 11 + w * (height + length) / Wisps + n * 13) % (height + length));
            float phase = w * MathF.Tau / Wisps + n;
            for (float d = 0; d < length; d += 0.5f)
            {
                float y = head + d;
                if (y < top || y > bottom)
                    continue;
                float f = (y - top) / height;
                float r = rTop + (rBottom - rTop) * f;
                float a = y * 0.16f + _time * 1.1f + phase;
                bool front = MathF.Sin(a) > 0;
                if (front != _front)
                    continue;
                // pâle aux deux bouts, et près du haut et du bas de la tour
                float fade = (1 - d / length) * MathF.Min(1, (y - top) / 8) * MathF.Min(1, (bottom - y) / 8);
                var c = Lit((d < length * 0.4f ? Ribbon : RibbonTail) with { A = fade }, front ? 1 : Back);
                // deux pixels de large : le second, plus pâle, sous le premier
                int x = (int)MathF.Round(cx + r * MathF.Cos(a)), py = (int)MathF.Round(y);
                Dot(x, py, c);
                Dot(x, py + 1, c with { A = c.A * 0.55f });
            }
        }
    }

    private static Color Lit(Color c, float light) => new(c.R * light, c.G * light, c.B * light, c.A);

    /// <summary>Un pixel d'art, une seule fois par image (deux bouts d'anneau tombés au même endroit ne s'ajoutent pas).</summary>
    private void Dot(int x, int y, Color c)
    {
        if (c.A > 0.02f && _drawn.Add((x, y)))
            DrawRect(new Rect2(new Vector2(x, y) * ArtScale, new Vector2(ArtScale, ArtScale)), c);
    }

    private void Image(Texture2D texture, Vector2 at, Color modulate) =>
        DrawTextureRect(texture, new Rect2(at * ArtScale, texture.GetSize() * ArtScale), false, modulate);

    /// <summary>Un nombre entre 0 et 1, toujours le même pour les mêmes entrées.</summary>
    private static float Noise(int seed, int n)
    {
        uint h = (uint)(seed * 374761393 + n * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }
}
