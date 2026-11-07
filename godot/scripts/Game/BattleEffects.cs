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

    private sealed class Beam
    {
        public Vector2 From, To;
        public double Age;
        public int Seed;
    }

    private readonly List<Beam> _beams = new();

    /// <summary>Durée d'un éclair à l'écran (il touche presque aussitôt, puis s'éteint).</summary>
    private const double BeamTime = 0.3;
    private readonly List<Impact> _impacts = new();
    private int _seed;

    /// <summary>Vitesse d'un projectile (pixels de l'écran par seconde), et sa durée minimale (une cible toute proche).</summary>
    private static double Speed(string kind) => kind switch
    {
        "boule-feu" => 1100,
        "boule-poison" => 1500,
        _ => 1500,
    };

    /// <summary>Hauteur de la courbe (en part de la distance) : une flèche monte puis redescend, une boule de feu va droit.</summary>
    private static float Arc(string kind) => kind == "fleche" ? 0.12f : 0;

    /// <summary>L'impact joué à l'arrivée d'un projectile.</summary>
    public static string ImpactOf(string projectile) => projectile switch
    {
        "fleche" => "piqure",
        "boule-feu" => "feu",
        "eclair" => "eclair",
        "boule-poison" => "poison",
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
        if (kind == "eclair")
        {
            // un éclair n'a pas de trajet : il frappe presque aussitôt (un projectile invisible porte le moment du coup)
            _beams.Add(new Beam { From = from, To = to, Seed = ++_seed });
            _projectiles.Add(new Projectile { Kind = "", From = to, To = to, Duration = 0.06, Arrived = arrived });
            return;
        }
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
        _beams.Clear();
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
        _impacts.RemoveAll(i => i.Age > ImpactTime(i.Kind));
        foreach (var b in _beams)
            b.Age += delta;
        _beams.RemoveAll(b => b.Age > BeamTime);
    }

    /// <summary>Durée d'un impact : bref, sauf le nuage de mort qui s'étale.</summary>
    private static double ImpactTime(string kind) => kind == "nuage" ? 0.75 : 0.32;

    public void Draw(CanvasItem canvas)
    {
        foreach (var b in _beams)
            DrawBeam(canvas, b);
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
                    Blob(canvas, At(back) + new Vector2(0, ((k * 37) % 5 - 2) * Pixel * 0.6f), k < 3 ? 1 : 0, c);
                }
                // environ 30 % plus grande que la première version (rayon 4 au lieu de 3)
                Blob(canvas, here, 4, new Color(0.9f, 0.35f, 0.08f));
                Blob(canvas, here, 3, new Color(1, 0.6f, 0.15f));
                Blob(canvas, here, 1, new Color(1, 0.85f + 0.15f * pulse, 0.45f));
                break;
            }
            case "boule-poison":
            {
                // une boule verte qui palpite, et des gouttes qui tombent derrière elle
                float pulse = 0.5f + 0.5f * MathF.Sin((float)p.Age * 30);
                for (int k = 1; k <= 5; k++)
                {
                    float back = Math.Max(0, t - k * 0.04f);
                    float fall = k * k * 0.6f * Pixel;
                    Dot(canvas, At(back) + new Vector2(((k * 53) % 3 - 1) * Pixel, fall),
                        new Color(0.45f, 0.8f, 0.25f, 0.95f - k * 0.16f));
                }
                Blob(canvas, here, 4, new Color(0.18f, 0.4f, 0.13f));
                Blob(canvas, here, 3, new Color(0.35f, 0.68f, 0.2f));
                Blob(canvas, here, 2, new Color(0.55f, 0.88f, 0.3f));
                Blob(canvas, here, 1, new Color(0.75f + 0.2f * pulse, 1, 0.5f));
                break;
            }
            case "":
                break; // le moment d'un coup, sans image (l'éclair)
            default:
                Blob(canvas, here, 1, Colors.White);
                break;
        }
    }

    /// <summary>
    /// Un éclair : une ligne brisée de pixels d'art, redessinée plusieurs fois par seconde (elle crépite), un cœur
    /// blanc-jaune et un halo jaune, qui s'éteint. Pas encore utilisé (gardé pour un sort ou une créature à venir).
    /// </summary>
    private static void DrawBeam(CanvasItem canvas, Beam b)
    {
        float fade = 1 - (float)(b.Age / BeamTime);
        int flicker = (int)(b.Age / 0.05);
        int segments = Math.Max(3, (int)(b.From.DistanceTo(b.To) / (Pixel * 9)));
        var dir = (b.To - b.From).Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        var points = new Vector2[segments + 1];
        for (int k = 0; k <= segments; k++)
        {
            float jitter = k == 0 || k == segments ? 0 : (Noise(b.Seed + flicker * 31, k) - 0.5f) * 2 * 4 * Pixel;
            points[k] = b.From.Lerp(b.To, (float)k / segments) + side * jitter;
        }
        // un halo jaune de part et d'autre, un cœur blanc-jaune de deux pixels d'art
        var halo = new Color(1, 0.85f, 0.25f, 0.75f * fade);
        var core = new Color(1, 1, 0.8f, fade);
        for (int k = 0; k < segments; k++)
        {
            Line(canvas, points[k] + side * Pixel * 2, points[k + 1] + side * Pixel * 2, halo);
            Line(canvas, points[k] - side * Pixel, points[k + 1] - side * Pixel, halo);
            Line(canvas, points[k], points[k + 1], core);
            Line(canvas, points[k] + side * Pixel, points[k + 1] + side * Pixel, core);
        }
    }

    // ------------------------------------------------------------------ impacts

    private static void DrawImpact(CanvasItem canvas, Impact i)
    {
        float t = (float)(i.Age / ImpactTime(i.Kind));
        if (i.Kind == "nuage")
        {
            DrawCloud(canvas, i, t);
            return;
        }
        var (count, colors, spread, flash) = i.Kind switch
        {
            // un éclat bref et discret : quelques grains qui jaillissent puis tombent et s'éteignent
            "piqure" => (5, new[] { new Color(1, 1, 0.9f), new Color(0.6f, 0.45f, 0.3f) }, 7f, 1),
            "feu" => (9, new[] { new Color(1, 0.85f, 0.4f), new Color(1, 0.5f, 0.1f), new Color(0.6f, 0.2f, 0.1f) }, 9f, 2),
            "morsure" => (6, new[] { new Color(0.85f, 0.15f, 0.15f), new Color(1, 0.9f, 0.85f) }, 6f, 1),
            "poison" => (9, new[] { new Color(0.6f, 0.95f, 0.35f), new Color(0.3f, 0.6f, 0.2f) }, 8f, 1),
            "eclair" => (6, new[] { new Color(1, 1, 0.8f), new Color(1, 0.85f, 0.25f) }, 7f, 2),
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

    /// <summary>
    /// Le nuage de mort de la Liche : des volutes vert-gris qui s'étalent autour de la cible, jusqu'aux cases voisines,
    /// puis se dissipent.
    /// </summary>
    private static void DrawCloud(CanvasItem canvas, Impact i, float t)
    {
        float reach = 110 * 1.05f; // un peu plus qu'une case : les voisines sont couvertes
        for (int k = 0; k < 22; k++)
        {
            float a = Noise(i.Seed, k) * MathF.Tau;
            float r = reach * (0.25f + 0.75f * Noise(i.Seed, k + 40)) * MathF.Sqrt(t);
            // aplati comme les cases (le champ est vu de biais)
            var at = i.At + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.55f) - new Vector2(0, 4 * Pixel * t);
            var color = k % 3 == 0 ? new Color(0.62f, 0.75f, 0.55f) : new Color(0.42f, 0.55f, 0.4f);
            Blob(canvas, at, k % 2 == 0 ? 2 : 1, new Color(color, Math.Clamp(1.2f - t * 1.2f, 0, 0.85f)));
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
