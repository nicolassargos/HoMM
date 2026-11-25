using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Les feuilles dans le vent (2026-11-25) : sous une rafale (<see cref="Wind"/>), des feuilles se détachent des arbres
/// feuillus visibles (<see cref="DecorLayer.SheddingLeaves"/>), d'un pixel du feuillage et de sa couleur. Portées dans le
/// sens du vent, elles tombent en voletant (une face plus sombre, l'autre plus claire), se posent au sol un instant, puis
/// s'effacent. Seulement le dessin, au-dessus des héros et des objets, sous le brouillard.
/// </summary>
public partial class LeafLayer : Node2D
{
    public AdventureMap Map { get; set; } = null!;

    private struct Leaf
    {
        public Vector2 At;
        public float Age, Life, Phase, Ground, Lift;
        public Color Light, Dark;
        public bool Landed;
    }

    /// <summary>Un pixel du feuillage d'un arbre : sa place depuis le pied (pixels de la carte) et sa couleur.</summary>
    private readonly record struct Spot(Vector2 Offset, Color Color);

    private (Vector2 Foot, Spot[] Spots)[] _trees = Array.Empty<(Vector2, Spot[])>();
    private readonly List<Leaf> _leaves = new();
    private readonly Random _rng = new();

    /// <summary>Le plus de feuilles à la fois.</summary>
    private const int MaxLeaves = 300;

    public override void _Ready()
    {
        var spots = new Dictionary<Texture2D, Spot[]>();
        _trees = DecorLayer.Sprites(Map).Where(s => DecorLayer.SheddingLeaves(s.Image))
            .Select(s => (s.Foot, spots.TryGetValue(s.Image, out var known) ? known : spots[s.Image] = Foliage(s.Image)))
            .Where(t => t.Item2.Length > 0).ToArray();
    }

    /// <summary>Les pixels du feuillage d'une image : opaques, dans ses trois cinquièmes du haut, ni trop sombres.</summary>
    private static Spot[] Foliage(Texture2D texture)
    {
        var image = texture.GetImage();
        if (image.IsCompressed())
            image.Decompress();
        int w = image.GetWidth(), h = image.GetHeight();
        var found = new List<Spot>();
        for (int y = 0; y < h * 3 / 5; y++)
        for (int x = 0; x < w; x++)
        {
            var c = image.GetPixel(x, y);
            if (c.A > 0.9f && c.Luminance > 0.22f)
                found.Add(new Spot(new Vector2(x + 0.5f - w / 2f, y + 0.5f - h) * MapRenderer.ArtScale, new Color(c.R, c.G, c.B)));
        }
        return found.ToArray();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta, t = Wind.Now;
        // les arbres à l'écran (avec une marge), selon la caméra
        var view = (GetGlobalTransformWithCanvas().AffineInverse() * GetViewportRect()).Grow(MapRenderer.TileSize);
        if (Wind.LeafRate > 0 && _leaves.Count < MaxLeaves)
            foreach (var (foot, spots) in _trees)
            {
                if (!view.HasPoint(foot))
                    continue;
                float gust = Wind.Gust(foot, t);
                if (gust <= 0 || _rng.NextDouble() >= Wind.LeafRate * gust * dt)
                    continue;
                var spot = spots[_rng.Next(spots.Length)];
                _leaves.Add(new Leaf
                {
                    At = foot + spot.Offset,
                    Life = 5f + (float)_rng.NextDouble() * 3f,
                    Phase = (float)_rng.NextDouble() * MathF.Tau,
                    // elle se pose un peu au-dessous ou au-dessus du pied de son arbre
                    Ground = foot.Y + ((float)_rng.NextDouble() * 10f - 3f) * MapRenderer.ArtScale,
                    Lift = (float)_rng.NextDouble(),
                    Light = spot.Color.Lightened(0.15f),
                    Dark = spot.Color.Darkened(0.3f),
                });
                if (_leaves.Count >= MaxLeaves)
                    break;
            }

        for (int i = _leaves.Count - 1; i >= 0; i--)
        {
            var leaf = _leaves[i];
            leaf.Age += dt;
            if (!leaf.Landed)
            {
                // portée par le vent (plus vite dans une rafale), elle tombe en se balançant
                float gust = Wind.Gust(leaf.At, t);
                float swing = MathF.Sin(leaf.Age * 3.1f + leaf.Phase);
                var push = Wind.Direction * Wind.LeafSpeed * (0.35f + gust);
                float fall = (0.35f + 0.25f * leaf.Lift) * MapRenderer.TileSize * (0.6f + 0.4f * swing * swing);
                leaf.At += (push + new Vector2(swing * 0.25f * MapRenderer.TileSize, fall - gust * 0.2f * MapRenderer.TileSize)) * dt;
                if (leaf.At.Y >= leaf.Ground && leaf.Age > 0.8f)
                {
                    // posée : elle reste un instant, puis s'efface
                    leaf.Landed = true;
                    leaf.Life = Math.Min(leaf.Life, leaf.Age + 1.5f);
                }
            }
            if (leaf.Age >= leaf.Life)
                _leaves.RemoveAt(i);
            else
                _leaves[i] = leaf;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        const float px = MapRenderer.ArtScale;
        foreach (var leaf in _leaves)
        {
            float fade = Math.Clamp((leaf.Life - leaf.Age) / 0.8f, 0f, 1f) * Math.Clamp(leaf.Age / 0.2f, 0f, 1f);
            float turn = MathF.Sin(leaf.Age * 5.3f + leaf.Phase);
            var color = (leaf.Landed || turn > 0 ? leaf.Light : leaf.Dark) with { A = fade };
            // deux pixels d'art de large, un seul quand elle se retourne (vue par la tranche) ; un pixel plus sombre
            // dessous, son ombre sur elle-même
            var at = (leaf.At / px).Floor() * px;
            bool flat = leaf.Landed || MathF.Abs(turn) > 0.3f;
            DrawRect(new Rect2(at, new Vector2(flat ? 2 * px : px, px)), color);
            if (flat)
                DrawRect(new Rect2(at + new Vector2(turn > 0 ? px : 0, px), new Vector2(px, px)), leaf.Dark with { A = fade });
        }
    }
}
