using System;
using System.Collections.Generic;
using Godot;
using HoMM.Core;
using static HoMM.Game.BattleEffects;
using static HoMM.Game.SpellEffects;

namespace HoMM.Game;

/// <summary>
/// Les effets des sorts de la carte, dessinés par le jeu au-dessus des héros et des objets (sous le brouillard), en pixels
/// d'art comme ceux du combat : des ondes et des étincelles turquoise autour du bateau invoqué, des rafales de vent qui
/// poussent le héros, une faille violette au départ et à l'arrivée de la porte dimensionnelle (une traînée de lumière
/// saute de l'une à l'autre), une colonne de lumière d'or devant la ville du retour.
/// </summary>
public partial class MapSpellEffects : Node2D
{
    private sealed class Effect
    {
        public SpellEffect Kind;
        /// <summary>Le milieu des cases de départ et d'arrivée (le bateau : sa case ; le vent : celle du héros).</summary>
        public Vector2 From, To;
        public double Age, Duration;
        public int Seed;
        /// <summary>Aperçu (« --map-spell-preview ») : il recommence sans fin, ou reste figé à cet instant.</summary>
        public bool Loop;
        public float? Frozen;
    }

    private readonly List<Effect> _effects = new();
    private int _seed;

    /// <summary>La durée de chaque effet (le retour en ville attend la fin du sien pour ouvrir la ville).</summary>
    public static double DurationOf(SpellEffect kind) => kind switch
    {
        SpellEffect.SummonBoat => 1.6,
        SpellEffect.DoubleMovement => 1.4,
        SpellEffect.DimensionDoor => 1.2,
        _ => 1.4,
    };

    public override void _Ready() => TextureFilter = TextureFilterEnum.Nearest;

    /// <summary>Le milieu d'une case, en pixels de la carte.</summary>
    private static Vector2 Middle(Cell c) => ActorLayer.ToPixels(c) + new Vector2(MapRenderer.TileSize / 2f, MapRenderer.TileSize / 2f);

    /// <summary>Joue l'effet d'un sort lancé (le bateau invoqué apparaît sur sa case).</summary>
    public void Play(MapSpellCast cast) =>
        Play(cast.Spell.Effect, cast.Boat ?? cast.From, cast.Boat ?? cast.To);

    public void Play(SpellEffect kind, Cell from, Cell to, bool loop = false, float? frozen = null) =>
        _effects.Add(new Effect
        {
            Kind = kind, From = Middle(from), To = Middle(to), Duration = DurationOf(kind), Seed = ++_seed,
            Loop = loop, Frozen = frozen,
        });

    public override void _Process(double delta)
    {
        foreach (var e in _effects)
            if (e.Frozen == null)
                e.Age = e.Loop ? (e.Age + delta) % e.Duration : e.Age + delta;
        // (une dernière fois quand le dernier s'achève : il s'efface)
        if (_effects.RemoveAll(e => e.Age >= e.Duration) > 0 || _effects.Count > 0)
            QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var e in _effects)
        {
            float t = e.Frozen ?? (float)(e.Age / e.Duration);
            switch (e.Kind)
            {
                case SpellEffect.SummonBoat: DrawBoat(e, t); break;
                case SpellEffect.DoubleMovement: DrawWind(e, t); break;
                case SpellEffect.DimensionDoor: DrawDoor(e, t); break;
                case SpellEffect.TownPortal: DrawTownPortal(e, t); break;
            }
        }
    }

    private static readonly Color White = new(1, 1, 1), Teal = new(0.35f, 0.95f, 1), TealDeep = new(0.1f, 0.5f, 0.85f),
        Wind = new(0.85f, 0.95f, 1), Sky = new(0.55f, 0.78f, 1), Leaf = new(0.45f, 0.8f, 0.25f), LeafDark = new(0.25f, 0.5f, 0.12f),
        Violet = new(0.72f, 0.4f, 1), Magenta = new(1, 0.45f, 0.92f), Void = new(0.1f, 0.02f, 0.2f),
        Gold = new(1, 0.82f, 0.35f), GoldPale = new(1, 0.95f, 0.7f);

    /// <summary>
    /// L'invocation de bateau : l'eau s'illumine sous lui, deux ondes s'écartent, des étincelles montent en tournant, un
    /// éclair blanc au début (le bateau apparaît).
    /// </summary>
    private void DrawBoat(Effect e, float t)
    {
        float open = Hold(t);
        var water = e.To + new Vector2(0, 10);
        Glow(this, water, 46, 20, Teal, 0.6f * open);
        for (int k = 0; k < 2; k++)
        {
            float u = (t * 1.5f + k * 0.5f) % 1;
            Ring(this, water, 12 + 46 * u, (12 + 46 * u) * 0.42f, 3, Teal, 0.9f * (1 - u) * open);
        }
        for (int n = 0; n < 16; n++)
        {
            float u = (t * 1.3f + Noise(e.Seed, n)) % 1;
            float a = n * MathF.Tau / 16 + t * 7;
            float r = 34 * (1 - u * 0.6f);
            var at = water + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.4f - u * 64);
            if (n % 3 == 0)
                Star(this, at, 3, Teal, Bell(u) * open);
            else
                Dot(this, at, A(n % 3 == 1 ? White : Teal, Bell(u) * open));
        }
        float flash = Math.Clamp(1 - MathF.Abs(t - 0.12f) * 7, 0, 1);
        if (flash > 0)
        {
            Glow(this, e.To, 34 * flash, 26 * flash, White, 0.8f * flash);
            Star(this, e.To + new Vector2(0, -6), 7, Teal, flash);
        }
    }

    /// <summary>
    /// Le vent porteur : des rafales blanches qui ondulent et passent sur le héros de gauche à droite, une qui s'enroule
    /// autour de lui, des feuilles emportées.
    /// </summary>
    private void DrawWind(Effect e, float t)
    {
        float open = Hold(t);
        Glow(this, e.To, 44, 32, Wind, 0.22f * open);
        for (int k = 0; k < 8; k++)
        {
            float u = (t * 1.3f + Noise(e.Seed, k)) % 1;
            float y = -36 + k * 9 + (Noise(e.Seed, k + 10) - 0.5f) * 6;
            float head = -90 + u * 190;
            float length = 34 + 24 * Noise(e.Seed, k + 20);
            for (float j = 0; j <= length; j += Pixel)
            {
                float x = head - j;
                float wave = MathF.Sin(x * 0.06f + k * 1.7f) * 5;
                float fade = 1 - j / length;
                var at = e.To + new Vector2(x, y + wave);
                // un trait de deux pixels : blanc dessus, bleu pâle dessous
                Dot(this, at, A(fade > 0.5f ? White : Wind, Bell(u) * open * fade));
                Dot(this, at + new Vector2(0, Pixel), A(Sky, 0.7f * Bell(u) * open * fade));
            }
        }
        // une rafale qui s'enroule autour du héros
        for (int j = 0; j < 24; j++)
        {
            float a = t * 9 - j * 0.16f;
            var at = e.To + new Vector2(MathF.Cos(a) * 32, MathF.Sin(a) * 11 + 16 - t * 36);
            Dot(this, at, A(j < 8 ? White : Sky, open * (1 - j / 24f)));
            Dot(this, at + new Vector2(Pixel, 0), A(Wind, 0.6f * open * (1 - j / 24f)));
        }
        for (int n = 0; n < 6; n++)
        {
            float u = (t * 1.1f + Noise(e.Seed, n + 30)) % 1;
            var at = e.To + new Vector2(-60 + u * 130, -20 + n * 8 + MathF.Sin(u * 12 + n) * 8);
            var c = A(n % 2 == 0 ? Leaf : LeafDark, Bell(u) * open);
            Dot(this, at, c);
            Dot(this, at + new Vector2(((int)(u * 10) & 1) == 0 ? Pixel : 0, Pixel), c);
        }
    }

    /// <summary>
    /// La porte dimensionnelle : une faille violette s'ouvre debout sur la case de départ, avale des étincelles et se
    /// referme ; une traînée de lumière saute en arc jusqu'à l'arrivée, où une autre faille s'ouvre et les recrache.
    /// </summary>
    private void DrawDoor(Effect e, float t)
    {
        Rift(e.From, Math.Clamp(t / 0.6f, 0, 1), true, e.Seed);
        Rift(e.To, Math.Clamp((t - 0.35f) / 0.65f, 0, 1), false, e.Seed + 7);
        // l'arc de lumière, d'une faille à l'autre (au-dessus des cases qu'il survole)
        float u = Math.Clamp((t - 0.2f) / 0.3f, 0, 1);
        if (u <= 0 || u >= 1)
            return;
        float height = 30 + e.From.DistanceTo(e.To) * 0.25f;
        Vector2 Arc(float v) => e.From.Lerp(e.To, v) - new Vector2(0, MathF.Sin(v * MathF.PI) * height);
        for (int k = 0; k < 14; k++)
        {
            float v = u - k * 0.025f;
            if (v < 0)
                break;
            Dot(this, Arc(v), A(k < 3 ? White : k < 7 ? Magenta : Violet, 1 - k / 14f));
        }
        Star(this, Arc(u), 3, Magenta, 1);
    }

    /// <summary>Une faille debout : un ovale sombre semé d'étoiles, cerclé de violet, qui s'ouvre puis se referme.</summary>
    private void Rift(Vector2 at, float s, bool leaving, int seed)
    {
        if (s <= 0 || s >= 1)
            return;
        float open = Bell(s);
        float w = 20 * open, h = 34;
        var middle = at + new Vector2(0, -4);
        Glow(this, middle, w + 12, h + 10, Violet, 0.4f * open);
        Glow(this, middle, Math.Max(2, w - 2), h - 4, Void, 1.2f * open);
        Ring(this, middle, Math.Max(2, w), h, 2, Magenta, open);
        for (int n = 0; n < 5; n++)
            if (Noise(seed, n + (int)(s * 12)) > 0.4f)
                Dot(this, middle + new Vector2((Noise(seed, n + 40) - 0.5f) * w, (Noise(seed, n + 50) - 0.5f) * h * 1.4f), A(White, open));
        // des étincelles aspirées (départ) ou recrachées (arrivée)
        for (int n = 0; n < 10; n++)
        {
            float a = Noise(seed, n + 60) * MathF.Tau;
            float r = (leaving ? 1 - s : s) * 40 + 6;
            var spark = middle + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.8f);
            Dot(this, spark, A(n % 2 == 0 ? Magenta : White, open));
        }
    }

    /// <summary>Le retour en ville : une colonne de lumière d'or tombe devant la ville, une flaque de lumière, des grains qui descendent.</summary>
    private void DrawTownPortal(Effect e, float t)
    {
        float open = Hold(t);
        var ground = e.To + new Vector2(0, 20);
        Glow(this, ground, 40, 15, Gold, 0.7f * open);
        float u0 = (t * 1.4f) % 1;
        Ring(this, ground, 10 + 34 * u0, (10 + 34 * u0) * 0.38f, 3, GoldPale, 0.8f * (1 - u0) * open);
        float half = 20 * open, top = ground.Y - 200;
        for (float x = -half; x <= half; x += Pixel)
        {
            float side = MathF.Abs(x) / Math.Max(1, half);
            for (float y = top; y < ground.Y; y += Pixel)
            {
                float up = (ground.Y - y) / (ground.Y - top);
                float alpha = open * MathF.Ceiling(0.8f * (1 - side * side) * (1 - up) * 6) / 6;
                if (alpha > 0.03f && (((int)(y / Pixel) + (int)(x / Pixel)) & 1) == 0 | side < 0.4f)
                    Dot(this, new Vector2(ground.X + x, y), A(side < 0.35f ? GoldPale : Gold, alpha));
            }
        }
        for (int n = 0; n < 24; n++)
        {
            float u = (t * 1.6f + Noise(e.Seed, n)) % 1;
            var at = new Vector2(ground.X + (Noise(e.Seed, n + 20) - 0.5f) * 50, top + 40 + u * (ground.Y - top - 40));
            if (n % 4 == 0)
                Star(this, at, 3, Gold, Bell(u) * open);
            else
                Dot(this, at, A(n % 2 == 0 ? White : GoldPale, Bell(u) * open));
        }
        float flash = Math.Clamp(1 - MathF.Abs(t - 0.3f) * 6, 0, 1);
        if (flash > 0)
            Glow(this, e.To, 30 * flash, 36 * flash, White, 0.8f * flash);
    }
}
