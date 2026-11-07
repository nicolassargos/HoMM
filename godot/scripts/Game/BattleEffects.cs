using System;
using System.Collections.Generic;
using Godot;

namespace HoMM.Game;

/// <summary>
/// Les effets du combat, dessinés par le jeu (les animations des créatures n'en ont aucun, choix de l'utilisateur) :
/// les **projectiles** qui volent du tireur à sa cible (flèche, boule de feu…) et les **impacts** joués sur une troupe
/// touchée (un petit éclat discret à chaque coup). Tout est dessiné en « pixels d'art » de <see cref="Pixel"/> pixels
/// de l'écran, comme les créatures agrandies ×3, pour que les effets aient la même finesse qu'elles.
/// Les sortes d'effets sont décrites par leur nom (« fleche », « boule-feu », « choc », « piqure »…) : le nom vient des
/// réglages de la créature (<c>art/creatures/battle/animations.json</c> : « projectile », « impact »).
/// </summary>
public sealed class BattleEffects
{
    /// <summary>Taille d'un pixel d'art à l'écran (les créatures sont agrandies ×3).</summary>
    public const float Pixel = 3;

    private sealed class Projectile
    {
        public string Kind = "";
        public Vector2 From, To;
        public double Age, Duration;
        public Action? Arrived;
        public bool Done;
    }

    private sealed class Impact
    {
        public string Kind = "";
        public Vector2 At;
        public double Age;
        public int Seed;
        /// <summary>Vers la droite (+1) ou la gauche (-1) : l'éclat part dans le sens du coup.</summary>
        public float Direction;
    }

    private readonly List<Projectile> _projectiles = new();
    private readonly List<Impact> _impacts = new();
    private int _seed;

    /// <summary>Vitesse d'un projectile (pixels de l'écran par seconde), et sa durée minimale (une cible toute proche).</summary>
    private static double Speed(string kind) => kind switch
    {
        "boule-feu" => 1100,
        _ => 1500,
    };

    /// <summary>Hauteur de la courbe (en part de la distance) : une flèche monte puis redescend, une boule de feu va droit.</summary>
    private static float Arc(string kind) => kind == "fleche" ? 0.12f : 0;

    /// <summary>L'impact joué à l'arrivée d'un projectile.</summary>
    public static string ImpactOf(string projectile) => projectile switch
    {
        "fleche" => "piqure",
        "boule-feu" => "feu",
        _ => "choc",
    };

    public bool Busy => _projectiles.Count > 0;

    /// <summary>
    /// Vitesse des projectiles choisie par le joueur : 1 la vitesse normale, 0,5 deux fois plus lents, 2 deux fois plus
    /// rapides (réglage de l'écran de combat).
    /// </summary>
    public static double ProjectileSpeed { get; set; } = 1;

    /// <summary>Lance un projectile ; <paramref name="arrived"/> est appelé quand il touche (le coup porte à ce moment).</summary>
    public void Launch(string kind, Vector2 from, Vector2 to, Action arrived)
    {
        double duration = Math.Max(0.12, from.DistanceTo(to) / (Speed(kind) * Math.Clamp(ProjectileSpeed, 0.25, 4)));
        _projectiles.Add(new Projectile { Kind = kind, From = from, To = to, Duration = duration, Arrived = arrived });
    }

    /// <summary>Un impact sur une troupe touchée, au milieu de son corps ; <paramref name="direction"/> : le sens du coup.</summary>
    public void Hit(string kind, Vector2 at, float direction) =>
        _impacts.Add(new Impact { Kind = kind, At = at, Seed = ++_seed, Direction = direction });

    public void Clear()
    {
        _projectiles.Clear();
        _impacts.Clear();
    }

    public void Update(double delta)
    {
        foreach (var p in _projectiles)
        {
            p.Age += delta;
            if (p.Age >= p.Duration && !p.Done)
            {
                p.Done = true;
                p.Arrived?.Invoke();
            }
        }
        _projectiles.RemoveAll(p => p.Done);
        foreach (var i in _impacts)
            i.Age += delta;
        _impacts.RemoveAll(i => i.Age > ImpactTime);
    }

    private const double ImpactTime = 0.32;

    public void Draw(CanvasItem canvas)
    {
        foreach (var p in _projectiles)
            DrawProjectile(canvas, p);
        foreach (var i in _impacts)
            DrawImpact(canvas, i);
    }

    // ------------------------------------------------------------------ projectiles

    private static void DrawProjectile(CanvasItem canvas, Projectile p)
    {
        float t = (float)Math.Clamp(p.Age / p.Duration, 0, 1);
        Vector2 At(float k)
        {
            var straight = p.From.Lerp(p.To, k);
            // une courbe : la flèche monte puis redescend (parabole), d'autant plus haut que le tir est long
            return straight - new Vector2(0, 4 * Arc(p.Kind) * p.From.DistanceTo(p.To) * k * (1 - k));
        }
        var here = At(t);
        var ahead = At(Math.Min(1, t + 0.02f));
        var dir = (ahead - here).LengthSquared() > 0.01f ? (ahead - here).Normalized() : (p.To - p.From).Normalized();
        switch (p.Kind)
        {
            case "fleche":
                // la hampe (brune), la pointe (gris clair) et l'empennage (blanc), en pixels d'art le long du trajet
                Line(canvas, here - dir * Pixel * 9, here - dir * Pixel * 1, new Color(0.55f, 0.36f, 0.2f));
                Line(canvas, here - dir * Pixel * 1, here + dir * Pixel * 1, new Color(0.85f, 0.85f, 0.9f));
                Dot(canvas, here - dir * Pixel * 9 + new Vector2(-dir.Y, dir.X) * Pixel, new Color(0.95f, 0.95f, 0.95f));
                Dot(canvas, here - dir * Pixel * 9 - new Vector2(-dir.Y, dir.X) * Pixel, new Color(0.95f, 0.95f, 0.95f));
                break;
            case "boule-feu":
            {
                // une boule qui palpite, et une traînée d'étincelles qui s'éteignent derrière elle
                float pulse = 0.5f + 0.5f * MathF.Sin((float)p.Age * 40);
                for (int k = 1; k <= 6; k++)
                {
                    float back = Math.Max(0, t - k * 0.035f);
                    var c = k < 3 ? new Color(1, 0.6f, 0.15f, 1 - k * 0.14f) : new Color(0.75f, 0.25f, 0.1f, 0.9f - k * 0.13f);
                    Dot(canvas, At(back) + new Vector2(0, ((k * 37) % 5 - 2) * Pixel * 0.6f), c);
                }
                Blob(canvas, here, 3, new Color(0.9f, 0.35f, 0.08f));
                Blob(canvas, here, 2, new Color(1, 0.6f, 0.15f));
                Blob(canvas, here, 1, new Color(1, 0.85f + 0.15f * pulse, 0.45f));
                break;
            }
            default:
                Blob(canvas, here, 1, Colors.White);
                break;
        }
    }

    // ------------------------------------------------------------------ impacts

    private static void DrawImpact(CanvasItem canvas, Impact i)
    {
        float t = (float)(i.Age / ImpactTime);
        var (count, colors, spread, flash) = i.Kind switch
        {
            // un éclat bref et discret : quelques grains qui jaillissent puis tombent et s'éteignent
            "piqure" => (5, new[] { new Color(1, 1, 0.9f), new Color(0.6f, 0.45f, 0.3f) }, 7f, 1),
            "feu" => (9, new[] { new Color(1, 0.85f, 0.4f), new Color(1, 0.5f, 0.1f), new Color(0.6f, 0.2f, 0.1f) }, 9f, 2),
            "morsure" => (6, new[] { new Color(0.85f, 0.15f, 0.15f), new Color(1, 0.9f, 0.85f) }, 6f, 1),
            "poison" => (7, new[] { new Color(0.5f, 0.9f, 0.3f), new Color(0.3f, 0.6f, 0.2f) }, 6f, 0),
            _ => (7, new[] { new Color(1, 1, 0.85f), new Color(0.75f, 0.72f, 0.65f) }, 8f, 1), // « choc »
        };
        // le premier instant : une petite croix claire au point du coup
        if (t < 0.25f && flash > 0)
        {
            var c = new Color(1, 1, 0.92f, 1 - t * 3);
            Dot(canvas, i.At, c);
            for (int k = 1; k <= flash; k++)
            {
                Dot(canvas, i.At + new Vector2(k * Pixel, 0), c);
                Dot(canvas, i.At - new Vector2(k * Pixel, 0), c);
                Dot(canvas, i.At + new Vector2(0, k * Pixel), c);
                Dot(canvas, i.At - new Vector2(0, k * Pixel), c);
            }
        }
        // les grains : chacun sa direction (surtout dans le sens du coup), sa vitesse et sa couleur, toujours les mêmes
        // pour un même impact (pas de clignotement d'une image à l'autre)
        for (int k = 0; k < count; k++)
        {
            float a = Noise(i.Seed, k) * MathF.PI - MathF.PI / 2; // de -90° à +90° : vers l'avant
            float speed = 0.5f + Noise(i.Seed, k + 50);
            var dir = new Vector2(MathF.Cos(a) * i.Direction, MathF.Sin(a) - 0.4f);
            var at = i.At + dir * spread * Pixel * speed * t + new Vector2(0, 6 * Pixel * t * t); // ils retombent
            var color = colors[k % colors.Length];
            Dot(canvas, at, new Color(color, Math.Clamp(1.3f - t * 1.3f, 0, 1)));
        }
    }

    // ------------------------------------------------------------------ pixels d'art

    private static float Noise(int seed, int k) => Math.Abs(MathF.Sin(seed * 12.9898f + k * 78.233f) * 43758.547f) % 1;

    /// <summary>Un pixel d'art, posé sur la grille des pixels d'art (net, comme les créatures).</summary>
    private static void Dot(CanvasItem canvas, Vector2 at, Color color)
    {
        var snapped = new Vector2(MathF.Floor(at.X / Pixel) * Pixel, MathF.Floor(at.Y / Pixel) * Pixel);
        canvas.DrawRect(new Rect2(snapped, new Vector2(Pixel, Pixel)), color);
    }

    /// <summary>Une ligne de pixels d'art (comme tracée à la main, sans flou), quel que soit son angle.</summary>
    private static void Line(CanvasItem canvas, Vector2 from, Vector2 to, Color color)
    {
        int steps = Math.Max(1, (int)(from.DistanceTo(to) / Pixel));
        for (int k = 0; k <= steps; k++)
            Dot(canvas, from.Lerp(to, (float)k / steps), color);
    }

    /// <summary>Une petite boule de pixels d'art (rayon en pixels d'art).</summary>
    private static void Blob(CanvasItem canvas, Vector2 at, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
            if (x * x + y * y <= radius * radius + radius)
                Dot(canvas, at + new Vector2(x, y) * Pixel, color);
    }
}
