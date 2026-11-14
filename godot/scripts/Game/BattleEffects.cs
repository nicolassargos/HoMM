using System;
using System.Collections.Generic;
using Godot;

namespace HoMM.Game;

/// <summary>
/// Les effets du combat, dessinés par le jeu (les animations des créatures n'en ont aucun, choix de l'utilisateur) :
/// les **projectiles** qui volent du tireur à sa cible (flèche, boule de feu…) et les **impacts** joués sur une troupe
/// touchée (un petit éclat discret à chaque coup). Tout est dessiné en « pixels d'art » de <see cref="Pixel"/> pixels
/// de l'écran, comme les créatures agrandies ×2 (et la carte), pour que les effets aient la même finesse qu'elles. Les
/// tailles et les distances sont comptées dans l'unité <see cref="U"/> (3 pixels de l'écran : l'ancien pixel d'art, du
/// temps du combat ×3), pour que les effets gardent leur taille à l'écran.
/// Les sortes d'effets sont décrites par leur nom (« fleche », « boule-feu », « choc », « piqure »…) : le nom vient des
/// réglages de la créature (<c>art/creatures/battle/animations.json</c> : « projectile », « impact »).
/// </summary>
public sealed class BattleEffects
{
    /// <summary>Taille d'un pixel d'art à l'écran (les créatures sont agrandies ×2, comme la carte).</summary>
    public const float Pixel = 2;

    /// <summary>
    /// L'unité des tailles et des distances des effets : 3 pixels de l'écran (l'ancien pixel d'art du combat ×3). Un effet
    /// garde ainsi sa taille à l'écran, dessiné avec des pixels plus fins.
    /// </summary>
    public const float U = 3;

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

    private sealed class Strike
    {
        public string Kind = "";
        /// <summary>Le sol de la case touchée (le pied de la troupe).</summary>
        public Vector2 At;
        public double Age;
        public int Seed;
        /// <summary>Figé (aperçu) : il ne vieillit pas.</summary>
        public bool Frozen;
    }

    /// <summary>
    /// Les effets joués sur une case (la foudre de l'Oiseau-tonnerre…) : ils ne partent pas de l'attaquant, ils tombent
    /// sur la case de la cible, où qu'elle soit.
    /// </summary>
    private readonly List<Strike> _strikes = new();

    private sealed class Beam
    {
        public Vector2 From, To;
        /// <summary>La seconde main du lanceur (le Mage) : un second rayon qui rejoint le premier, ou rien.</summary>
        public Vector2? From2;
        public double Age;
        public int Seed;
    }

    private readonly List<Beam> _beams = new();

    private sealed class Flow
    {
        public Vector2 From, To;
        public double Age;
        public int Seed;
    }

    /// <summary>Les flux : des gouttes qui vont d'une troupe à une autre (le Vampire boit la vie de sa victime).</summary>
    private readonly List<Flow> _flows = new();

    private const double FlowTime = 0.6;

    /// <summary>Un flux de gouttes rouges de <paramref name="from"/> (la victime) à <paramref name="to"/> (le Vampire).</summary>
    public void Drain(Vector2 from, Vector2 to) => _flows.Add(new Flow { From = from, To = to, Seed = ++_seed });

    /// <summary>Durée d'un éclair à l'écran (il touche presque aussitôt, puis s'éteint).</summary>
    private const double BeamTime = 0.3;
    private readonly List<Impact> _impacts = new();
    private int _seed;

    /// <summary>Vitesse d'un projectile (pixels de l'écran par seconde), et sa durée minimale (une cible toute proche).</summary>
    private static double Speed(string kind) => kind switch
    {
        "boule-feu" => 1100,
        "boule-poison" => 1500,
        "hache" => 1200,
        "feuilles" => 1000,
        _ => 1500,
    };

    /// <summary>Hauteur de la courbe (en part de la distance) : une flèche monte puis redescend, une boule de feu va droit.</summary>
    private static float Arc(string kind) => kind switch { "fleche" => 0.12f, "hache" => 0.08f, "feuilles" => 0.02f, _ => 0 };

    /// <summary>L'impact joué à l'arrivée d'un projectile.</summary>
    public static string ImpactOf(string projectile) => projectile switch
    {
        "fleche" => "piqure",
        "boule-feu" => "feu",
        "eclair" => "eclair",
        "boule-poison" => "poison",
        "hache" => "entaille",
        "feuilles" => "feuilles",
        _ => "choc",
    };

    public bool Busy => _projectiles.Count > 0;

    /// <summary>
    /// Vitesse des projectiles choisie par le joueur : 1 la vitesse normale, 0,5 deux fois plus lents, 2 deux fois plus
    /// rapides (réglage de l'écran de combat).
    /// </summary>
    public static double ProjectileSpeed { get; set; } = 1;

    /// <summary>
    /// Lance un projectile ; <paramref name="arrived"/> est appelé quand il touche (le coup porte à ce moment).
    /// <paramref name="from2"/> : la seconde main d'où part aussi un éclair (il rejoint le premier).
    /// </summary>
    public void Launch(string kind, Vector2 from, Vector2 to, Action arrived, Vector2? from2 = null)
    {
        if (kind == "eclair")
        {
            // un éclair n'a pas de trajet : il frappe presque aussitôt (un projectile invisible porte le moment du coup)
            _beams.Add(new Beam { From = from, From2 = from2, To = to, Seed = ++_seed });
            _projectiles.Add(new Projectile { Kind = "", From = to, To = to, Duration = 0.06, Arrived = arrived });
            return;
        }
        double duration = Math.Max(0.12, from.DistanceTo(to) / (Speed(kind) * Math.Clamp(ProjectileSpeed, 0.25, 4)));
        _projectiles.Add(new Projectile { Kind = kind, From = from, To = to, Duration = duration, Arrived = arrived });
    }

    /// <summary>Un impact sur une troupe touchée, au milieu de son corps ; <paramref name="direction"/> : le sens du coup.</summary>
    public void Hit(string kind, Vector2 at, float direction) =>
        _impacts.Add(new Impact { Kind = kind, At = at, Seed = ++_seed, Direction = direction });

    /// <summary>Le moment où la foudre touche le sol, après son départ (les dégâts s'affichent à cet instant).</summary>
    public const double LightningHitTime = 0.12;

    /// <summary>Durée d'un effet joué sur une case (la marque brûlée au sol reste un peu après l'éclair).</summary>
    private static double StrikeTime(string kind) => kind switch { "foudre-forte" => 1.4, _ => 1.1 };

    /// <summary>
    /// Un effet joué sur la case de la cible plutôt qu'un impact sur son corps : « foudre » (le coup de l'Oiseau-tonnerre),
    /// « foudre-forte » (la foudre qu'il appelle, une attaque sur cinq).
    /// </summary>
    public static bool PlaysOnHex(string? kind) => kind is "foudre" or "foudre-forte";

    /// <summary>
    /// Joue l'effet <paramref name="kind"/> (« foudre ») sur une case ; <paramref name="ground"/> : le sol de la case.
    /// <paramref name="frozenAt"/> (aperçu) : l'effet reste figé à cet instant.
    /// </summary>
    public void OnHex(string kind, Vector2 ground, double? frozenAt = null) =>
        _strikes.Add(new Strike { Kind = kind, At = ground, Seed = ++_seed, Age = frozenAt ?? 0, Frozen = frozenAt != null });

    public void Clear()
    {
        _strikes.Clear();
        _projectiles.Clear();
        _impacts.Clear();
        _beams.Clear();
        _flows.Clear();
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
        foreach (var w in _flows)
            w.Age += delta;
        _flows.RemoveAll(w => w.Age > FlowTime);
        foreach (var s in _strikes)
            if (!s.Frozen)
                s.Age += delta;
        _strikes.RemoveAll(s => !s.Frozen && s.Age > StrikeTime(s.Kind));
    }

    /// <summary>Durée d'un impact : bref, sauf le nuage de mort qui s'étale.</summary>
    private static double ImpactTime(string kind) => kind switch
    {
        "nuage" => 0.75,
        "bulles" or "soin" or "ames" => 0.7,
        "regeneration" => 1.2,
        _ => 0.32,
    };

    /// <summary>Ce que les effets laissent au sol, sous les troupes (la lueur et la marque brûlée de la foudre).</summary>
    public void DrawGround(CanvasItem canvas)
    {
        foreach (var s in _strikes)
            if (PlaysOnHex(s.Kind))
                DrawLightningGround(canvas, s);
    }

    public void Draw(CanvasItem canvas)
    {
        foreach (var s in _strikes)
            if (PlaysOnHex(s.Kind))
                DrawLightning(canvas, s);
        foreach (var b in _beams)
            DrawBeam(canvas, b);
        foreach (var w in _flows)
            DrawFlow(canvas, w);
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
                Line(canvas, here - dir * U * 9, here - dir * U * 1, new Color(0.55f, 0.36f, 0.2f));
                Line(canvas, here - dir * U * 1, here + dir * U * 1, new Color(0.85f, 0.85f, 0.9f));
                Dot(canvas, here - dir * U * 9 + new Vector2(-dir.Y, dir.X) * U, new Color(0.95f, 0.95f, 0.95f));
                Dot(canvas, here - dir * U * 9 - new Vector2(-dir.Y, dir.X) * U, new Color(0.95f, 0.95f, 0.95f));
                break;
            case "boule-feu":
            {
                // une boule qui palpite, et une traînée d'étincelles qui s'éteignent derrière elle
                float pulse = 0.5f + 0.5f * MathF.Sin((float)p.Age * 40);
                for (int k = 1; k <= 6; k++)
                {
                    float back = Math.Max(0, t - k * 0.035f);
                    var c = k < 3 ? new Color(1, 0.6f, 0.15f, 1 - k * 0.14f) : new Color(0.75f, 0.25f, 0.1f, 0.9f - k * 0.13f);
                    Blob(canvas, At(back) + new Vector2(0, ((k * 37) % 5 - 2) * U * 0.6f), k < 3 ? 1 : 0, c);
                }
                // environ 30 % plus grande que la première version (rayon 4 au lieu de 3)
                Blob(canvas, here, 4, new Color(0.9f, 0.35f, 0.08f));
                Blob(canvas, here, 3, new Color(1, 0.6f, 0.15f));
                Blob(canvas, here, 1, new Color(1, 0.85f + 0.15f * pulse, 0.45f));
                break;
            }
            case "feuilles":
            {
                // le sort du Druide : un tourbillon de feuilles vertes qui tourne sur lui-même, et des feuilles qui
                // tombent derrière lui en se balançant
                float spin = (float)p.Age * 14;
                for (int k = 1; k <= 8; k++)
                {
                    float back = Math.Max(0, t - k * 0.04f);
                    var fall = new Vector2(MathF.Sin(k * 1.7f + (float)p.Age * 9) * U * 2, k * k * 0.3f * U);
                    Leaf(canvas, At(back) + fall, (float)p.Age * 6 + k, LeafColor(k), 1 - k * 0.1f);
                }
                for (int k = 0; k < 7; k++)
                {
                    float a = spin + k * MathF.Tau / 7;
                    var at = here + new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.75f) * U * 5;
                    Leaf(canvas, at, a + MathF.PI / 2, LeafColor(k), 1);
                }
                Blob(canvas, here, 1, new Color(0.7f, 0.95f, 0.45f));
                break;
            }
            case "boule-poison":
            {
                // une boule verte qui palpite, et des gouttes qui tombent derrière elle
                float pulse = 0.5f + 0.5f * MathF.Sin((float)p.Age * 30);
                for (int k = 1; k <= 5; k++)
                {
                    float back = Math.Max(0, t - k * 0.04f);
                    float fall = k * k * 0.6f * U;
                    Dot(canvas, At(back) + new Vector2(((k * 53) % 3 - 1) * U, fall),
                        new Color(0.45f, 0.8f, 0.25f, 0.95f - k * 0.16f));
                }
                Blob(canvas, here, 4, new Color(0.18f, 0.4f, 0.13f));
                Blob(canvas, here, 3, new Color(0.35f, 0.68f, 0.2f));
                Blob(canvas, here, 2, new Color(0.55f, 0.88f, 0.3f));
                Blob(canvas, here, 1, new Color(0.75f + 0.2f * pulse, 1, 0.5f));
                break;
            }
            case "hache":
            {
                // une hache qui tourne sur elle-même : un manche épais (brun, bordé de sombre) et un grand fer (gris clair au
                // tranchant, plus sombre vers le manche)
                float angle = (float)p.Age * 20;
                var turn = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var across = new Vector2(-turn.Y, turn.X);
                var dark = new Color(0.18f, 0.12f, 0.08f);
                Line(canvas, here - turn * U * 6 + across * Pixel * 2, here + turn * U * 3 + across * Pixel * 2, dark);
                Line(canvas, here - turn * U * 6 + across * Pixel, here + turn * U * 3 + across * Pixel, new Color(0.55f, 0.37f, 0.2f));
                Line(canvas, here - turn * U * 6, here + turn * U * 3, new Color(0.55f, 0.37f, 0.2f));
                // le fer : des traits en travers du manche, plus clairs vers le tranchant
                for (float s = U * 3; s < U * 6; s += Pixel)
                {
                    var color = s < U * 4 ? new Color(0.45f, 0.46f, 0.5f) : s < U * 5 ? new Color(0.68f, 0.7f, 0.74f) : new Color(0.92f, 0.93f, 0.96f);
                    Line(canvas, here + turn * s - across * U * 2, here + turn * s + across * U * 3, color);
                }
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
    /// blanc bleuté et un halo bleu (comme la foudre de l'Oiseau-tonnerre), qui s'éteint. Le tir du Mage de la Tour.
    /// </summary>
    private static void DrawBeam(CanvasItem canvas, Beam b)
    {
        float fade = 1 - (float)(b.Age / BeamTime);
        int flicker = (int)(b.Age / 0.05);
        int seed = b.Seed + flicker * 31;
        var start = b.From;
        if (b.From2 is { } from2)
        {
            // deux mains : un éclair fin part de chacune et ils se rejoignent un peu devant le lanceur, d'où part le grand
            var dir = (b.To - b.From).Normalized();
            start = b.From + dir * U * 18 + new Vector2(0, (from2.Y - b.From.Y) * 0.5f);
            Bolt(canvas, b.From, start, seed + 7, fade, 2, 3 * U);
            Bolt(canvas, from2, start, seed + 13, fade, 2, 3 * U);
        }
        var points = Bolt(canvas, start, b.To, seed, fade, 3, 6 * U);
        // de petites branches le long du grand éclair : quelques traits fins qui partent de côté et s'arrêtent
        for (int k = 1; k < points.Length - 1; k++)
        {
            if (Noise(seed + 101, k) > 0.45f)
                continue;
            var along = (points[k + 1] - points[k]).Normalized();
            float turn = (Noise(seed + 103, k) > 0.5f ? 1 : -1) * (0.5f + 0.6f * Noise(seed + 107, k));
            var branch = along.Rotated(turn) * U * (6 + 6 * Noise(seed + 109, k));
            Bolt(canvas, points[k], points[k] + branch, seed + 211 * k, fade * 0.85f, 1, 2 * U);
        }
    }

    /// <summary>
    /// Un éclair brisé de <paramref name="from"/> à <paramref name="to"/>, redessiné à chaque scintillement : un cœur blanc
    /// bleuté de <paramref name="core"/> pixels d'art, un halo bleu vif et une lueur pâle autour ; <paramref name="jag"/> :
    /// l'écart des zigzags. Rend les points du tracé (pour y accrocher des branches).
    /// </summary>
    private static Vector2[] Bolt(CanvasItem canvas, Vector2 from, Vector2 to, int seed, float fade, int core, float jag)
    {
        int segments = Math.Max(2, (int)(from.DistanceTo(to) / (U * 6)));
        var dir = (to - from).Normalized();
        var side = new Vector2(-dir.Y, dir.X);
        var points = new Vector2[segments + 1];
        for (int k = 0; k <= segments; k++)
        {
            float jitter = k == 0 || k == segments ? 0 : (Noise(seed, k) - 0.5f) * 2 * jag;
            points[k] = from.Lerp(to, (float)k / segments) + side * jitter;
        }
        var glow = new Color(0.3f, 0.5f, 1, 0.35f * fade);
        var halo = new Color(0.3f, 0.6f, 1, 0.95f * fade);
        var heart = new Color(0.92f, 0.97f, 1, fade);
        // des traits côte à côte d'un pixel d'art : le cœur, le halo (deux de chaque côté pour le grand éclair et ceux
        // des mains, un pour une branche), la lueur (deux, le grand seulement)
        int haloWidth = core >= 2 ? 2 : 1, glowWidth = core >= 3 ? 2 : 0;
        for (int k = 0; k < segments; k++)
            for (int x = -haloWidth - glowWidth; x < core + haloWidth + glowWidth; x++)
            {
                var color = x >= 0 && x < core ? heart : x >= -haloWidth && x < core + haloWidth ? halo : glow;
                Line(canvas, points[k] + side * Pixel * x, points[k + 1] + side * Pixel * x, color);
            }
        return points;
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
        if (i.Kind is "bulles" or "soin" or "ames" or "regeneration")
        {
            DrawRising(canvas, i, t);
            return;
        }
        var (count, colors, spread, flash) = i.Kind switch
        {
            // un éclat bref et discret : quelques grains qui jaillissent puis tombent et s'éteignent
            "piqure" => (5, new[] { new Color(1, 1, 0.9f), new Color(0.6f, 0.45f, 0.3f) }, 7f, 1),
            "feu" => (9, new[] { new Color(1, 0.85f, 0.4f), new Color(1, 0.5f, 0.1f), new Color(0.6f, 0.2f, 0.1f) }, 9f, 2),
            "morsure" => (6, new[] { new Color(0.85f, 0.15f, 0.15f), new Color(1, 0.9f, 0.85f) }, 6f, 1),
            "poison" => (9, new[] { new Color(0.6f, 0.95f, 0.35f), new Color(0.3f, 0.6f, 0.2f) }, 8f, 1),
            "feuilles" => (12, new[] { new Color(0.55f, 0.85f, 0.3f), new Color(0.3f, 0.6f, 0.2f), new Color(0.8f, 0.9f, 0.35f) }, 10f, 1),
            "eclair" => (6, new[] { new Color(0.88f, 0.95f, 1), new Color(0.35f, 0.6f, 1) }, 7f, 2),
            "entaille" => (6, new[] { new Color(1, 1, 0.9f), new Color(0.7f, 0.15f, 0.1f) }, 7f, 1),
            _ => (7, new[] { new Color(1, 1, 0.85f), new Color(0.75f, 0.72f, 0.65f) }, 8f, 1), // « choc »
        };
        // le premier instant : une petite croix claire au point du coup
        if (t < 0.25f && flash > 0)
        {
            var c = new Color(1, 1, 0.92f, 1 - t * 3);
            Cross(canvas, i.At, flash, c);
        }
        // les grains : chacun sa direction (surtout dans le sens du coup), sa vitesse et sa couleur, toujours les mêmes
        // pour un même impact (pas de clignotement d'une image à l'autre)
        for (int k = 0; k < count; k++)
        {
            float a = Noise(i.Seed, k) * MathF.PI - MathF.PI / 2; // de -90° à +90° : vers l'avant
            float speed = 0.5f + Noise(i.Seed, k + 50);
            var dir = new Vector2(MathF.Cos(a) * i.Direction, MathF.Sin(a) - 0.4f);
            var at = i.At + dir * spread * U * speed * t + new Vector2(0, 6 * U * t * t); // ils retombent
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
            var at = i.At + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.55f) - new Vector2(0, 4 * U * t);
            var color = k % 3 == 0 ? new Color(0.62f, 0.75f, 0.55f) : new Color(0.42f, 0.55f, 0.4f);
            Blob(canvas, at, k % 2 == 0 ? 2 : 1, new Color(color, Math.Clamp(1.2f - t * 1.2f, 0, 0.85f)));
        }
    }

    /// <summary>
    /// Ce qui monte d'une troupe : des bulles vertes (le poison qui la ronge), des étincelles vertes en croix (des soins, la
    /// régénération du Troll), des âmes bleu pâle (le Fantôme grossit).
    /// </summary>
    private static void DrawRising(CanvasItem canvas, Impact i, float t)
    {
        var (color, count) = i.Kind switch
        {
            "bulles" => (new Color(0.6f, 1, 0.3f), 10),
            "soin" => (new Color(0.6f, 1, 0.55f), 6),
            "regeneration" => (new Color(0.6f, 1, 0.55f), 14),
            _ => (new Color(0.75f, 0.85f, 1), 7), // « ames »
        };
        // la régénération part des pieds et monte plus haut, le long du corps
        float rise = i.Kind == "regeneration" ? 28 : 16;
        for (int k = 0; k < count; k++)
        {
            float start = Noise(i.Seed, k) * 0.35f;
            float u = Math.Clamp((t - start) / 0.65f, 0, 1);
            if (u <= 0)
                continue;
            float x = (Noise(i.Seed, k + 20) - 0.5f) * 14 * U + MathF.Sin(u * 6 + k) * U;
            var at = i.At + new Vector2(x, (i.Kind == "regeneration" ? 0 : 6 * U) - u * rise * U);
            var c = new Color(color, Math.Clamp(1.4f - u * 1.4f, 0, 1));
            if (i.Kind is "soin" or "regeneration")
            {
                // une petite croix (une sur trois plus grande pour la régénération)
                Cross(canvas, at, i.Kind == "regeneration" && k % 3 == 0 ? 2 : 1, c);
            }
            else if (i.Kind == "bulles")
                Bubble(canvas, at, k % 3 == 0 ? 2 : 1, c);
            else
            {
                // une âme : un point clair avec une petite traîne
                Dot(canvas, at, c);
                Dot(canvas, at + new Vector2(0, Pixel), new Color(c, c.A * 0.75f));
                Dot(canvas, at + new Vector2(0, 2 * Pixel), new Color(c, c.A * 0.5f));
                Dot(canvas, at + new Vector2(0, 3 * Pixel), new Color(c, c.A * 0.25f));
            }
        }
    }

    /// <summary>Une bulle : un disque de pixels d'art (rayon 1 ou 2), avec un reflet clair.</summary>
    private static void Bubble(CanvasItem canvas, Vector2 at, int radius, Color color)
    {
        // un disque plein, un peu plus sombre au bord, avec un reflet clair en haut à gauche ; le rayon est donné dans
        // l'unité U (l'ancien pixel d'art), compté ici en pixels d'art
        radius = Scaled(radius);
        var edge = new Color(color.R * 0.6f, color.G * 0.75f, color.B * 0.6f, color.A);
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            int d = x * x + y * y;
            if (d <= radius * radius + radius / 2)
                Dot(canvas, at + new Vector2(x, y) * Pixel, d >= radius * radius - radius + 1 && radius > 2 ? edge : color);
        }
        if (radius > 2)
            Dot(canvas, at + new Vector2(-1, -1) * Pixel, new Color(0.95f, 1, 0.85f, color.A));
    }

    /// <summary>Des gouttes rouges qui vont de la victime au Vampire, le long d'une courbe, l'une après l'autre.</summary>
    private static void DrawFlow(CanvasItem canvas, Flow w)
    {
        float t = (float)(w.Age / FlowTime);
        for (int k = 0; k < 9; k++)
        {
            float u = t * 1.6f - k * 0.08f;
            if (u <= 0 || u >= 1)
                continue;
            var straight = w.From.Lerp(w.To, u);
            var at = straight - new Vector2(0, MathF.Sin(u * MathF.PI) * 10 * U)
                     + new Vector2(0, (Noise(w.Seed, k) - 0.5f) * 4 * U);
            Dot(canvas, at, k % 3 == 0 ? new Color(1, 0.45f, 0.45f) : new Color(0.75f, 0.1f, 0.12f));
        }
    }

    /// <summary>
    /// Une aura discrète sur une troupe tant qu'un état dure : des bulles vertes (empoisonnée), des étincelles dorées (un
    /// sort utile), des points violets (un sort néfaste). <paramref name="time"/> : le temps du combat ;
    /// <paramref name="seed"/> : propre à la troupe (les auras ne battent pas ensemble).
    /// </summary>
    public static void DrawAura(CanvasItem canvas, string kind, Vector2 center, double time, int seed)
    {
        var color = kind switch
        {
            "poison" => new Color(0.6f, 1, 0.3f),
            "benefique" => new Color(1, 0.88f, 0.45f),
            _ => new Color(0.8f, 0.55f, 1), // « nefaste »
        };
        // des grains (des bulles pour le poison) qui montent en boucle, chacun à son rythme, autour du corps
        int count = kind == "poison" ? 4 : 3;
        for (int k = 0; k < count; k++)
        {
            float u = (float)((time * 0.6 + Noise(seed, k)) % 1);
            float x = (Noise(seed, k + 10) - 0.5f) * 12 * U;
            var at = center + new Vector2(x, 5 * U - u * 12 * U);
            var c = new Color(color, MathF.Sin(u * MathF.PI) * 0.9f);
            if (kind == "poison")
                Bubble(canvas, at, k % 2 == 0 ? 2 : 1, c);
            else
                Dot(canvas, at, c);
        }
    }

    // ------------------------------------------------------------------ effets sur une case

    /// <summary>
    /// Les couleurs de la foudre (bleue, choix de l'utilisateur) : un cœur blanc bleuté, un halo bleu vif, une lueur bleu
    /// sombre autour, et le bleu pâle du traceur.
    /// </summary>
    private static readonly Color BoltCore = new(0.92f, 0.97f, 1), BoltHalo = new(0.45f, 0.7f, 1),
        BoltOuter = new(0.25f, 0.4f, 1), BoltGlow = new(0.75f, 0.88f, 1);

    /// <summary>
    /// La foudre qui tombe du ciel sur la case : un fin traceur descend du haut du champ, puis l'éclair éclate (un cœur blanc,
    /// un halo bleu et des branches), s'éteint un instant et éclate une seconde fois, plus faible. Au point d'impact, des
    /// étincelles jaillissent sur le sol. « foudre-forte » (la foudre appelée) : plus large, plus de branches, et tout le
    /// champ s'éclaire un instant.
    /// </summary>
    private static void DrawLightning(CanvasItem canvas, Strike s)
    {
        bool strong = s.Kind == "foudre-forte";
        double t = s.Age;
        var top = new Vector2(s.At.X + (Noise(s.Seed, 1) - 0.5f) * 30 * U, 0);
        if (t < LightningHitTime)
        {
            // le traceur : une ligne pâle et brisée qui descend jusqu'au sol
            float reach = (float)(t / LightningHitTime);
            var path = BoltPath(top, s.At, s.Seed, 0);
            int shown = Math.Max(1, (int)(reach * (path.Length - 1)));
            for (int k = 0; k < shown; k++)
            {
                Line(canvas, path[k], path[k + 1], new Color(BoltGlow, 0.65f));
                Line(canvas, path[k] + new Vector2(Pixel, 0), path[k + 1] + new Vector2(Pixel, 0), new Color(BoltHalo, 0.4f));
            }
            return;
        }
        // deux éclats : le premier plein, puis une courte nuit, puis un second plus faible qui s'éteint
        double u = t - LightningHitTime;
        float strength = u < 0.12 ? 1 : u < 0.16 ? 0 : u < 0.36 ? 0.75f * (1 - (float)((u - 0.16) / 0.2)) : 0;
        // la foudre appelée éclaire tout le champ, un court instant
        if (strong && u < 0.1 && canvas is Control field)
            canvas.DrawRect(new Rect2(Vector2.Zero, field.Size), new Color(BoltGlow, 0.22f * (1 - (float)(u / 0.1))));
        if (strength > 0)
        {
            int flicker = u < 0.12 ? (int)(u / 0.06) : 2; // le premier éclat crépite (la ligne change une fois)
            var path = BoltPath(top, s.At, s.Seed, flicker);
            // largeurs en pixels d'art, de part et d'autre du milieu : le cœur, le halo, la lueur
            // (affinée à la demande de l'utilisateur : 7 pixels d'art en tout, 13 pour la foudre appelée)
            int coreHalf = strong ? 2 : 1, haloHalf = coreHalf + (strong ? 2 : 1), outerHalf = haloHalf + (strong ? 2 : 1);
            var outer = new Color(BoltOuter, 0.35f * strength);
            var halo = new Color(BoltHalo, 0.85f * strength);
            var core = new Color(BoltCore, strength);
            for (int k = 0; k < path.Length - 1; k++)
            {
                var a = path[k];
                var b = path[k + 1];
                // les largeurs sont comptées dans l'unité U, tracées en pixels d'art
                int c1 = Scaled(coreHalf), c2 = Scaled(haloHalf), c3 = Scaled(outerHalf);
                for (int x = -c3; x <= c3; x++)
                {
                    int d = Math.Abs(x);
                    var color = d <= c1 ? core : d <= c2 ? halo : outer;
                    Line(canvas, a + new Vector2(x * Pixel, 0), b + new Vector2(x * Pixel, 0), color);
                }
            }
            // les branches : elles partent du tronc et s'en vont de biais, plus fines (un cœur et un peu de halo)
            int branches = strong ? 6 : 4;
            for (int n = 0; n < branches; n++)
            {
                int from = 2 + (int)(Noise(s.Seed + flicker, 10 + n) * (path.Length - 5));
                float side = Noise(s.Seed + flicker, 20 + n) < 0.5f ? -1 : 1;
                var at = path[from];
                int length = (strong ? 5 : 4) + (int)(Noise(s.Seed + flicker, 30 + n) * 5);
                for (int k = 0; k < length; k++)
                {
                    var next = at + new Vector2(side * (4 + Noise(s.Seed + flicker, 40 + n * 9 + k) * 7) * U,
                        (5 + Noise(s.Seed + flicker, 70 + n * 9 + k) * 7) * U);
                    float fade = strength * (1 - (float)k / length);
                    Line(canvas, at + new Vector2(Pixel, 0), next + new Vector2(Pixel, 0), new Color(BoltHalo, 0.8f * fade));
                    Line(canvas, at - new Vector2(Pixel, 0), next - new Vector2(Pixel, 0), new Color(BoltHalo, 0.8f * fade));
                    Line(canvas, at, next, new Color(BoltCore, fade));
                    at = next;
                }
            }
        }
        // les étincelles : elles jaillissent du point d'impact, s'écartent sur le sol (aplati comme les cases) et retombent
        float e = (float)(u / 0.6);
        int sparks = strong ? 28 : 20;
        if (e < 1)
            for (int k = 0; k < sparks; k++)
            {
                float a = Noise(s.Seed, 100 + k) * MathF.Tau;
                float speed = 0.5f + Noise(s.Seed, 120 + k);
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.5f - 0.6f);
                var at = s.At + dir * (strong ? 26 : 20) * U * speed * e + new Vector2(0, 16 * U * e * e);
                var color = new Color(k % 3 == 0 ? BoltHalo : BoltCore, Math.Clamp(1.2f - e * 1.2f, 0, 1));
                Dot(canvas, at, color);
                if (k % 4 == 0)
                    Dot(canvas, at + new Vector2(Pixel, 0), color);
            }
    }

    /// <summary>
    /// La foudre au sol, sous les troupes : une lueur bleu-blanc qui inonde la case au moment du choc, puis une marque brûlée
    /// (sombre, aplatie comme la case) qui s'efface doucement.
    /// </summary>
    private static void DrawLightningGround(CanvasItem canvas, Strike s)
    {
        double u = s.Age - LightningHitTime;
        if (u < 0)
            return;
        // presque une case (plus pour la foudre appelée), aplatie comme elle
        float rx = (s.Kind == "foudre-forte" ? 22 : 18) * U, ry = (s.Kind == "foudre-forte" ? 10 : 8) * U;
        // la marque brûlée : un disque sombre plus net au milieu, qui s'efface à la fin
        float fade = (float)Math.Clamp((StrikeTime(s.Kind) - s.Age) / 0.5, 0, 1) * 0.55f;
        Ellipse(canvas, s.At, rx * 0.65f, ry * 0.65f, new Color(0.08f, 0.06f, 0.05f, fade * 0.6f), s.Seed);
        Ellipse(canvas, s.At, rx * 0.35f, ry * 0.35f, new Color(0.05f, 0.04f, 0.03f, fade), s.Seed);
        // la lueur du choc : forte et large, puis elle se resserre et s'éteint
        float glow = (float)Math.Clamp(1 - u / 0.4, 0, 1);
        if (glow > 0)
        {
            Ellipse(canvas, s.At, rx * (0.7f + 0.3f * glow), ry * (0.7f + 0.3f * glow), new Color(BoltHalo, 0.45f * glow), s.Seed);
            Ellipse(canvas, s.At, rx * 0.45f, ry * 0.45f, new Color(BoltCore, 0.85f * glow), s.Seed);
        }
    }

    /// <summary>
    /// Le chemin brisé d'un éclair, de <paramref name="from"/> (le ciel) à <paramref name="to"/> (le sol) : des pas de
    /// quelques pixels d'art qui s'écartent au hasard et reviennent vers la cible. Toujours le même pour une même foudre et
    /// un même <paramref name="flicker"/> (l'éclair change de forme quand il crépite).
    /// </summary>
    private static Vector2[] BoltPath(Vector2 from, Vector2 to, int seed, int flicker)
    {
        // des pas de hauteur inégale, chacun part franchement d'un côté ou de l'autre (un zigzag, pas une corde)
        int steps = Math.Max(4, (int)((to.Y - from.Y) / (U * 14)));
        var points = new Vector2[steps + 1];
        float x = 0, side = Noise(seed, 5) < 0.5f ? -1 : 1;
        for (int k = 0; k <= steps; k++)
        {
            float f = (k + (k > 0 && k < steps ? (Noise(seed * 3 + flicker * 17, k) - 0.5f) * 0.6f : 0)) / steps;
            if (k > 0 && k < steps)
            {
                if (Noise(seed * 7 + flicker * 31, k + 200) < 0.7f)
                    side = -side;
                x += side * (3 + Noise(seed * 7 + flicker * 31, k) * 7) * U;
                x *= 1 - f * 0.35f; // plus près du sol, l'éclair se resserre sur la cible
            }
            else
                x = 0;
            points[k] = from.Lerp(to, f) + new Vector2(x, 0);
        }
        return points;
    }

    /// <summary>Une ellipse pleine de pixels d'art, au bord un peu irrégulier.</summary>
    internal static void Ellipse(CanvasItem canvas, Vector2 at, float rx, float ry, Color color, int seed)
    {
        int nx = (int)(rx / Pixel), ny = (int)(ry / Pixel);
        for (int y = -ny; y <= ny; y++)
        for (int x = -nx; x <= nx; x++)
        {
            float d = (float)(x * x) / (nx * nx + 0.01f) + (float)(y * y) / (ny * ny + 0.01f);
            if (d <= 0.85f + 0.3f * Noise(seed, x * 31 + y))
                Dot(canvas, at + new Vector2(x, y) * Pixel, color);
        }
    }

    // ------------------------------------------------------------------ pixels d'art

    internal static float Noise(int seed, int k) => Math.Abs(MathF.Sin(seed * 12.9898f + k * 78.233f) * 43758.547f) % 1;

    /// <summary>Un pixel d'art, posé sur la grille des pixels d'art (net, comme les créatures).</summary>
    internal static void Dot(CanvasItem canvas, Vector2 at, Color color)
    {
        var snapped = new Vector2(MathF.Floor(at.X / Pixel) * Pixel, MathF.Floor(at.Y / Pixel) * Pixel);
        canvas.DrawRect(new Rect2(snapped, new Vector2(Pixel, Pixel)), color);
    }

    /// <summary>Une ligne de pixels d'art (comme tracée à la main, sans flou), quel que soit son angle.</summary>
    internal static void Line(CanvasItem canvas, Vector2 from, Vector2 to, Color color)
    {
        int steps = Math.Max(1, (int)(from.DistanceTo(to) / Pixel));
        for (int k = 0; k <= steps; k++)
            Dot(canvas, from.Lerp(to, (float)k / steps), color);
    }

    /// <summary>Une petite boule de pixels d'art (rayon en pixels d'art).</summary>
    internal static void Blob(CanvasItem canvas, Vector2 at, int radius, Color color)
    {
        // le rayon est donné dans l'unité U (l'ancien pixel d'art) ; un rayon 0 reste un seul pixel
        radius = Scaled(radius);
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
            if (x * x + y * y <= radius * radius + radius)
                Dot(canvas, at + new Vector2(x, y) * Pixel, color);
    }

    /// <summary>Une petite croix claire : un pixel au milieu et quatre bras de <paramref name="arm"/> unités U.</summary>
    internal static void Cross(CanvasItem canvas, Vector2 at, int arm, Color color)
    {
        Dot(canvas, at, color);
        if (arm <= 0)
            return;
        Line(canvas, at - new Vector2(arm * U, 0), at + new Vector2(arm * U, 0), color);
        Line(canvas, at - new Vector2(0, arm * U), at + new Vector2(0, arm * U), color);
    }

    /// <summary>
    /// Une feuille : un losange allongé de pixels d'art dans la direction <paramref name="angle"/> (cinq de long, trois de
    /// large au milieu, une nervure plus claire), à l'opacité <paramref name="alpha"/>.
    /// </summary>
    private static void Leaf(CanvasItem canvas, Vector2 at, float angle, Color color, float alpha)
    {
        var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * Pixel;
        var side = new Vector2(-dir.Y, dir.X);
        float a = Math.Clamp(alpha, 0, 1);
        var c = new Color(color, a);
        var nervure = new Color(color.Lightened(0.35f), a);
        // un bord vert sombre autour de la feuille : elle se détache de l'herbe
        var bord = new Color(0.12f, 0.25f, 0.08f, a * 0.9f);
        Dot(canvas, at + dir * 3, bord);
        Dot(canvas, at - dir * 3, bord);
        for (int k = -2; k <= 2; k++)
        {
            Dot(canvas, at + dir * k + side * (Math.Abs(k) <= 1 ? 2 : 1), bord);
            Dot(canvas, at + dir * k - side * (Math.Abs(k) <= 1 ? 2 : 1), bord);
        }
        for (int k = -2; k <= 2; k++)
            Dot(canvas, at + dir * k, k == 0 ? nervure : c);
        for (int k = -1; k <= 1; k++)
        {
            Dot(canvas, at + dir * k + side, c);
            Dot(canvas, at + dir * k - side, c);
        }
    }

    /// <summary>Les couleurs des feuilles du Druide, l'une après l'autre : vert vif, or, vert pâle (elles se voient sur l'herbe).</summary>
    private static Color LeafColor(int k) => (k % 3) switch
    {
        0 => new Color(0.62f, 0.95f, 0.25f),
        1 => new Color(0.98f, 0.82f, 0.25f),
        _ => new Color(0.82f, 1f, 0.55f),
    };

    /// <summary>Une longueur comptée dans l'unité U, en pixels d'art (arrondie).</summary>
    private static int Scaled(int units) => (int)MathF.Round(units * U / Pixel);
}
