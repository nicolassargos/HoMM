using System;
using System.Collections.Generic;
using HoMM.Core;
using Godot;
using static HoMM.Game.BattleEffects;

namespace HoMM.Game;

/// <summary>
/// Les animations des sorts sur le champ de bataille, dessinées par le jeu comme les autres effets du combat
/// (<see cref="BattleEffects"/> : pixels d'art de <see cref="BattleEffects.Pixel"/>, tailles dans l'unité
/// <see cref="BattleEffects.U"/>). Deux sortes :
/// <list type="bullet">
/// <item>le **lancer** d'un sort : ce qui part du héros ou tombe du ciel (la flèche magique, la boule de feu, les
/// météores, l'arc de la chaîne d'éclairs) et ce qui se joue sur une troupe (la lumière de la bénédiction, les pierres
/// de la peau de pierre…), une fois ;</item>
/// <item>l'**aura** d'un sort qui dure, dessinée tant qu'il dure (<see cref="DrawLasting"/>).</item>
/// </list>
/// Ce qui tourne autour d'une troupe a de la profondeur : la part de derrière se dessine avant elle (son corps la cache,
/// <see cref="DrawBehind"/>), le reste après. Pendant un sort, la troupe elle-même change aussi (<see cref="Look"/> : elle
/// grisaille sous la peau de pierre, rougit de rage, disparaît dans la téléportation…).
/// Chaque effet est nommé (« fleche-magique », « explosion », « benediction »…).
/// </summary>
public sealed class SpellEffects
{
    private sealed class Effect
    {
        public string Kind = "";
        /// <summary>D'où part l'effet (le héros, la troupe précédente d'une chaîne) et où il arrive (le corps de la cible).</summary>
        public Vector2 From, To;
        /// <summary>Le sol sous la cible (là où se posent les lueurs et les marques).</summary>
        public Vector2 Ground;
        /// <summary>Les sols des cases touchées (la boule de feu, les météores).</summary>
        public Vector2[] Area = Array.Empty<Vector2>();
        /// <summary>La hauteur du corps de la troupe (la tête est à <see cref="Ground"/> moins cette hauteur) ; 0 : un effet
        /// qui n'est pas joué sur une troupe.</summary>
        public float Height;
        /// <summary>Le côté où regarde la troupe (+1 : la droite) : le bouclier se lève de ce côté.</summary>
        public float Facing = 1;
        public double Age, Duration;
        public int Seed;
        /// <summary>Appelé une fois, quand l'effet touche (<see cref="HitTime"/>) : le jeu montre alors les dégâts.</summary>
        public Action? Hit;
        public double HitTime;
        public bool HitDone;
        /// <summary>Figé (aperçu) : il ne vieillit pas.</summary>
        public bool Frozen;
        /// <summary>Le premier arc d'une chaîne d'éclairs (du héros à la troupe visée) : le plus épais.</summary>
        public bool First;
    }

    private readonly List<Effect> _effects = new();
    private int _seed;

    public bool Busy => _effects.Exists(e => !e.HitDone);

    /// <summary>Vitesse de la flèche magique et de la boule de feu (pixels de l'écran par seconde).</summary>
    private const float BoltSpeed = 1800, FireballSpeed = 1500;

    /// <summary>Le temps entre deux météores, et le temps de la chute de chacun.</summary>
    private const double MeteorGap = 0.05, MeteorFall = 0.32;

    /// <summary>
    /// La flèche magique (une flèche de lumière violette qui file du héros à la cible) ou la boule de feu (qui file jusqu'au
    /// milieu de la zone, puis explose sur toutes ses cases). <paramref name="hit"/> : au moment où elle touche.
    /// </summary>
    public void Launch(string kind, Vector2 from, Vector2 to, Vector2 ground, Vector2[] area, Action hit)
    {
        float speed = kind == "boule-de-feu" ? FireballSpeed : BoltSpeed;
        double flight = Math.Max(0.15, from.DistanceTo(to) / speed);
        _effects.Add(new Effect { Kind = kind, From = from, To = to, Ground = ground, Area = area, Duration = flight, HitTime = flight,
            Hit = () => { hit(); Burst(kind == "boule-de-feu" ? "explosion" : "eclat-magique", from, to, ground, area); }, Seed = ++_seed });
    }

    /// <summary>La pluie de météores : un météore par case, l'un après l'autre ; <paramref name="hit"/> quand le dernier tombe.</summary>
    public void Meteors(Vector2[] area, Action hit)
    {
        double fall = MeteorGap * (area.Length * MeteorsPerHex - 1) + MeteorFall;
        _effects.Add(new Effect { Kind = "meteores", Area = area, Duration = fall + FlameLife + 0.2, HitTime = fall, Hit = hit, Seed = ++_seed });
    }

    /// <summary>
    /// L'arc d'une chaîne d'éclairs, d'une troupe (ou du héros) à la suivante ; <paramref name="first"/> : le premier, du
    /// héros à la troupe visée (le plus épais).
    /// </summary>
    public void Arc(Vector2 from, Vector2 to, bool first, Action hit) =>
        _effects.Add(new Effect { Kind = "arc", From = from, To = to, First = first, Duration = 0.45, HitTime = 0.05, Hit = hit, Seed = ++_seed });

    /// <summary>Un effet joué une fois sur une troupe (sur son corps et son sol) : « benediction », « soins »…</summary>
    public void OnUnit(string kind, Vector2 body, Vector2 ground, float facing, double? frozenAt = null)
    {
        var effect = new Effect { Kind = kind, To = body, Ground = ground, Height = 2 * (ground.Y - body.Y), Facing = facing,
            Duration = UnitTime(kind), Seed = ++_seed, Age = frozenAt ?? 0, Frozen = frozenAt != null };
        effect.HitDone = true;
        _effects.Add(effect);
    }

    /// <summary>La durée d'un effet sur une troupe.</summary>
    public static double UnitTime(string kind) => kind switch
    {
        "resurrection" or "lenteur" => 1.3,
        "benediction" => 0.9,
        "hate" => 0.8,
        "teleport-depart" or "teleport-arrivee" => 0.45,
        _ => 1.0,
    };

    /// <summary>L'effet d'un sort qui se joue sur la troupe touchée (null : ce sort n'en a pas).</summary>
    public static string? UnitEffectOf(SpellType spell) => spell.Effect switch
    {
        SpellEffect.Bless => "benediction",
        SpellEffect.Curse => "malediction",
        SpellEffect.StoneSkin => "peau-de-pierre",
        SpellEffect.Bloodlust => "soif-de-sang",
        SpellEffect.Haste => "hate",
        SpellEffect.Slow => "lenteur",
        SpellEffect.ArrowShield => "bouclier",
        SpellEffect.Weakness => "faiblesse",
        SpellEffect.Blind => "aveuglement",
        SpellEffect.Cure => "soins",
        SpellEffect.Resurrection => "resurrection",
        SpellEffect.Dispel => "dissipation",
        _ => null,
    };

    private void Burst(string kind, Vector2 from, Vector2 at, Vector2 ground, Vector2[] area) =>
        _effects.Add(new Effect { Kind = kind, From = from, To = at, Ground = ground, Area = area,
            Duration = kind == "explosion" ? ExplosionTime : 0.45, HitDone = true, Seed = ++_seed });

    /// <summary>La durée de l'explosion de la boule de feu (le choc dure un peu plus d'une seconde, les flammes plus longtemps).</summary>
    private const double ExplosionTime = 1.9;

    /// <summary>La vie d'une flamme de l'explosion et des météores (secondes).</summary>
    private const float FlameLife = 1.45f;

    public void Clear() => _effects.Clear();

    public void Update(double delta)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            var e = _effects[i];
            if (!e.Frozen)
                e.Age += delta;
            if (!e.HitDone && e.Age >= e.HitTime)
            {
                e.HitDone = true;
                e.Hit?.Invoke(); // peut ajouter un effet (l'explosion)
            }
        }
        _effects.RemoveAll(e => !e.Frozen && e.HitDone && e.Age > e.Duration);
    }

    /// <summary>Un effet joué sur la troupe dont le sol est <paramref name="ground"/>.</summary>
    private static bool On(Effect e, Vector2 ground) => e.Height > 0 && e.Ground.DistanceSquaredTo(ground) <= 16;

    /// <summary>
    /// Comment se montre la troupe dont le sol est <paramref name="ground"/> pendant un sort joué sur elle : une teinte
    /// (multipliée ; au-delà de 1, elle éclaire), son opacité (la téléportation) et la force de ses images fantômes (la
    /// hâte : elle laisse des traces derrière elle).
    /// </summary>
    public (Color Tint, float Alpha, float Ghost) Look(Vector2 ground)
    {
        var tint = Colors.White;
        float alpha = 1, ghost = 0;
        foreach (var e in _effects)
        {
            if (!On(e, ground))
                continue;
            float t = (float)Math.Clamp(e.Age / e.Duration, 0, 1);
            switch (e.Kind)
            {
                case "malediction": tint *= Mix(new Color(0.72f, 0.52f, 0.9f), Hold(t)); break;
                case "faiblesse": tint *= Mix(new Color(0.66f, 0.7f, 0.62f), Hold(t)); break;
                // la troupe prend la couleur de la pierre quand les éclats s'y fondent, puis la perd
                case "peau-de-pierre": tint *= Mix(new Color(0.74f, 0.72f, 0.7f), Math.Clamp((t - 0.3f) * 4, 0, 1) * Math.Clamp((1 - t) * 3, 0, 1)); break;
                case "soif-de-sang": tint *= Mix(new Color(1.45f, 0.55f, 0.5f), Beat(t)); break;
                case "soins": tint *= Mix(new Color(1.2f, 1.45f, 1.15f), Bell(t)); break;
                case "resurrection": tint *= Mix(new Color(1.5f, 1.38f, 1.05f), Bell(t)); break;
                case "lenteur": tint *= Mix(new Color(1, 0.88f, 0.66f), Hold(t) * 0.7f); break;
                case "aveuglement":
                    tint *= t < 0.3f ? Mix(new Color(1.9f, 1.9f, 1.9f), 1 - t / 0.3f) : Mix(new Color(0.78f, 0.76f, 0.85f), Hold(t));
                    break;
                case "dissipation": tint *= Mix(new Color(1.7f, 1.7f, 1.8f), Math.Clamp(1 - MathF.Abs(t - 0.15f) * 6, 0, 1)); break;
                case "hate": ghost = Math.Max(ghost, Bell(t)); break;
                case "teleport-depart":
                    alpha *= Math.Clamp(1 - t / 0.75f, 0, 1);
                    tint *= Mix(new Color(1.3f, 1.5f, 1.9f), t * 1.3f);
                    break;
                case "teleport-arrivee":
                    alpha *= Math.Clamp((t - 0.2f) / 0.6f, 0, 1);
                    tint *= Mix(new Color(1.3f, 1.5f, 1.9f), 1 - t);
                    break;
            }
        }
        return (tint, alpha, ghost);
    }

    /// <summary>Ce qui se dessine au sol, sous les troupes : lueurs, marques brûlées, cercles.</summary>
    public void DrawGround(CanvasItem canvas)
    {
        foreach (var e in _effects)
        {
            float t = (float)(e.Age / e.Duration);
            switch (e.Kind)
            {
                case "explosion": DrawExplosionGround(canvas, e, t); break;
                case "meteores": DrawMeteorsGround(canvas, e); break;
                case "benediction": DrawLightPool(canvas, e); break;
                case "soins": DrawHealPool(canvas, e, t, Green, GreenPale); break;
                case "resurrection": DrawHealPool(canvas, e, t, Gold, GoldPale); break;
                case "malediction" or "faiblesse": DrawShadowPool(canvas, e, t); break;
                case "soif-de-sang": DrawBloodPool(canvas, e, t); break;
                case "lenteur": DrawSlowDial(canvas, e, t); break;
                case "bouclier": Glow(canvas, e.Ground + new Vector2(e.Facing * 10 * U, 0), 14 * U, 6 * U, Sky, 0.4f * Hold(t)); break;
                case "dissipation": DrawDispelCircle(canvas, e, t); break;
                case "teleport-depart" or "teleport-arrivee": DrawTeleportGround(canvas, e, t); break;
            }
        }
    }

    /// <summary>Ce qui se dessine derrière la troupe dont le sol est <paramref name="ground"/> (avant elle : son corps le cache).</summary>
    public void DrawBehind(CanvasItem canvas, Vector2 ground)
    {
        _back = true;
        foreach (var e in _effects)
            if (On(e, ground))
                DrawOnUnit(canvas, e, (float)(e.Age / e.Duration));
        _back = false;
    }

    /// <summary>Ce qui se dessine par-dessus les troupes.</summary>
    public void Draw(CanvasItem canvas)
    {
        foreach (var e in _effects)
        {
            float t = (float)(e.Age / e.Duration);
            switch (e.Kind)
            {
                case "fleche-magique": if (!e.HitDone) DrawMagicBolt(canvas, e); break;
                case "boule-de-feu": if (!e.HitDone) DrawFireball(canvas, e); break;
                case "eclat-magique": DrawMagicBurst(canvas, e, t); break;
                case "explosion": DrawExplosion(canvas, e, t); break;
                case "meteores": DrawMeteors(canvas, e); break;
                case "arc": DrawArc(canvas, e, t); break;
                default: DrawOnUnit(canvas, e, t); break;
            }
        }
    }

    /// <summary>Un effet joué sur une troupe : la part de derrière ou celle de devant, selon <see cref="_back"/>.</summary>
    private static void DrawOnUnit(CanvasItem canvas, Effect e, float t)
    {
        switch (e.Kind)
        {
            case "benediction": if (!_back) DrawShaft(canvas, e, t, Gold, GoldPale, true); break;
            case "soins": DrawHealing(canvas, e, t); break;
            case "resurrection": DrawResurrection(canvas, e, t); break;
            case "malediction": DrawCurse(canvas, e, t); break;
            case "faiblesse": DrawWeakness(canvas, e, t); break;
            case "peau-de-pierre": DrawStoneSkin(canvas, e, t); break;
            case "soif-de-sang": DrawBloodlust(canvas, e, t); break;
            case "hate": DrawHaste(canvas, e, t); break;
            case "lenteur": DrawSlow(canvas, e, t); break;
            case "bouclier": if (!_back) Shield(canvas, e.To, e.Height, e.Facing, (float)e.Age, Math.Clamp(t * 2.5f, 0, 1), Math.Clamp((1 - t) * 3, 0, 1), e.Seed); break;
            case "aveuglement": DrawBlind(canvas, e, t); break;
            case "dissipation": DrawDispel(canvas, e, t); break;
            case "teleport-depart": DrawTeleport(canvas, e, t, true); break;
            case "teleport-arrivee": DrawTeleport(canvas, e, t, false); break;
        }
    }

    // ------------------------------------------------------------------ couleurs

    private static readonly Color White = new(1, 1, 1), Gold = new(1, 0.82f, 0.35f), GoldPale = new(1, 0.95f, 0.7f),
        GoldDeep = new(0.8f, 0.5f, 0.1f), Green = new(0.45f, 0.95f, 0.45f), GreenPale = new(0.85f, 1, 0.8f),
        GreenDeep = new(0.1f, 0.45f, 0.15f), Purple = new(0.7f, 0.4f, 0.95f), PurpleDark = new(0.25f, 0.1f, 0.35f),
        Violet = new(0.75f, 0.55f, 1), VioletCore = new(0.95f, 0.9f, 1), VioletDeep = new(0.34f, 0.14f, 0.62f),
        FireCore = new(1, 0.97f, 0.7f), FireYellow = new(1, 0.82f, 0.25f), FireOrange = new(1, 0.5f, 0.12f),
        FireRed = new(0.8f, 0.18f, 0.08f), Ember = new(0.42f, 0.08f, 0.05f), Smoke = new(0.24f, 0.22f, 0.22f),
        Scorch = new(0.07f, 0.05f, 0.03f), Dust = new(0.58f, 0.5f, 0.4f), Stone = new(0.62f, 0.6f, 0.56f),
        StoneLight = new(0.88f, 0.87f, 0.83f), Blood = new(0.85f, 0.1f, 0.1f), BloodLight = new(1, 0.45f, 0.4f),
        BloodDark = new(0.4f, 0.02f, 0.05f), Wind = new(0.75f, 0.9f, 1), Sky = new(0.45f, 0.7f, 1),
        SkyDeep = new(0.2f, 0.4f, 0.9f), EarthDark = new(0.3f, 0.27f, 0.2f), Grey = new(0.6f, 0.65f, 0.55f),
        Sand = new(0.98f, 0.85f, 0.5f), SandDark = new(0.6f, 0.4f, 0.16f), Wood = new(0.4f, 0.24f, 0.1f),
        ArcCore = new(0.92f, 0.97f, 1), ArcHalo = new(0.45f, 0.7f, 1), ArcOuter = new(0.25f, 0.4f, 1),
        Night = new(0.09f, 0.07f, 0.13f), NightGrey = new(0.3f, 0.27f, 0.36f);

    /// <summary>Les tons d'un feu, du plus chaud au plus froid (voir <see cref="Heat"/>).</summary>
    private static readonly Color[] FireColors = { FireCore, FireYellow, FireOrange, FireRed, Ember };

    /// <summary>Les tons des flammes de rage de la soif de sang.</summary>
    private static readonly Color[] BloodColors = { new(1, 0.8f, 0.75f), BloodLight, Blood, new(0.55f, 0.04f, 0.06f), BloodDark };

    /// <summary>Les tons d'une roche : bord, ombre, milieu, lumière (voir <see cref="Rock"/>).</summary>
    private static readonly Color[] MeteorRock = { new(0.1f, 0.07f, 0.06f), new(0.22f, 0.15f, 0.12f), new(0.38f, 0.28f, 0.22f), new(0.56f, 0.45f, 0.36f) },
        GreyRock = { new(0.16f, 0.16f, 0.17f), new(0.38f, 0.37f, 0.36f), new(0.57f, 0.56f, 0.53f), new(0.8f, 0.79f, 0.75f) };

    private static Color A(Color c, float alpha) => new(c, Math.Clamp(alpha, 0, 1));

    /// <summary>Monte puis redescend : 0 au début et à la fin, 1 au milieu (la vie d'une étincelle).</summary>
    private static float Bell(float t) => t <= 0 || t >= 1 ? 0 : MathF.Sin(t * MathF.PI);

    /// <summary>Monte vite, tient, puis s'éteint à la fin.</summary>
    private static float Hold(float t) => Math.Clamp(t * 5, 0, 1) * Math.Clamp((1 - t) * 3, 0, 1);

    /// <summary>Deux battements de cœur (la soif de sang).</summary>
    private static float Beat(float t) => Math.Max(Bell(Math.Clamp((t - 0.05f) / 0.3f, 0, 1)), Bell(Math.Clamp((t - 0.4f) / 0.35f, 0, 1)));

    private static Color Mix(Color c, float k) => Colors.White.Lerp(c, Math.Clamp(k, 0, 1));

    /// <summary>Une couleur prise le long de <paramref name="stops"/> (0 : la première, 1 : la dernière).</summary>
    private static Color Ramp(float u, params Color[] stops)
    {
        u = Math.Clamp(u, 0, 1) * (stops.Length - 1);
        int i = Math.Min((int)u, stops.Length - 2);
        return stops[i].Lerp(stops[i + 1], u - i);
    }

    /// <summary>Le ton d'un feu selon sa chaleur (1 : le cœur), par paliers comme une palette de pixel art.</summary>
    private static Color Heat(Color[] p, float h) => h > 0.72f ? p[0] : h > 0.52f ? p[1] : h > 0.33f ? p[2] : h > 0.17f ? p[3] : p[4];

    // ------------------------------------------------------------------ formes

    /// <summary>Vrai pendant la passe de derrière (<see cref="DrawBehind"/>).</summary>
    private static bool _back;

    /// <summary>Ce qui est derrière la troupe (<paramref name="z"/> négatif) se dessine avant elle, le reste après.</summary>
    private static bool Shows(float z) => (z < 0) == _back;

    /// <summary>Une lueur douce : une ellipse dont l'opacité baisse du milieu vers le bord, par paliers.</summary>
    private static void Glow(CanvasItem canvas, Vector2 at, float rx, float ry, Color color, float alpha)
    {
        if (alpha <= 0.01f)
            return;
        int nx = Math.Max(1, (int)(rx / Pixel)), ny = Math.Max(1, (int)(ry / Pixel));
        for (int y = -ny; y <= ny; y++)
        for (int x = -nx; x <= nx; x++)
        {
            float d = (float)(x * x) / (nx * nx) + (float)(y * y) / (ny * ny);
            if (d >= 1)
                continue;
            float k = MathF.Ceiling((1 - d) * (1 - d) * 5) / 5;
            Dot(canvas, at + new Vector2(x, y) * Pixel, A(color, alpha * k));
        }
    }

    /// <summary>
    /// Un anneau aplati de <paramref name="width"/> d'épaisseur, plus net au milieu. <paramref name="depth"/> : un anneau
    /// autour d'une troupe (sa moitié du haut passe derrière elle).
    /// </summary>
    private static void Ring(CanvasItem canvas, Vector2 at, float rx, float ry, float width, Color color, float alpha, bool depth = false)
    {
        if (alpha <= 0.01f || rx <= 0 || ry <= 0)
            return;
        int nx = (int)((rx + width) / Pixel) + 1, ny = (int)((ry + width * ry / rx) / Pixel) + 1;
        for (int y = -ny; y <= ny; y++)
        {
            if (depth && !Shows(y))
                continue;
            for (int x = -nx; x <= nx; x++)
            {
                float d = MathF.Sqrt(x * x * Pixel * Pixel / (rx * rx) + y * y * Pixel * Pixel / (ry * ry));
                float off = MathF.Abs(d - 1) * rx / width;
                if (off < 1)
                    Dot(canvas, at + new Vector2(x, y) * Pixel, A(color, alpha * (off < 0.45f ? 1 : 0.45f)));
            }
        }
    }

    /// <summary>Un disque de pixels d'art, de rayon <paramref name="r"/> pixels d'art (0 : un seul pixel).</summary>
    private static void Disc(CanvasItem canvas, Vector2 at, float r, Color color)
    {
        int n = (int)MathF.Ceiling(r);
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
            if (x * x + y * y <= r * r + 0.5f)
                Dot(canvas, at + new Vector2(x, y) * Pixel, color);
    }

    /// <summary>Une étincelle : un point blanc et quatre branches qui s'effacent vers leur bout.</summary>
    private static void Star(CanvasItem canvas, Vector2 at, int arm, Color color, float alpha)
    {
        if (alpha <= 0.01f)
            return;
        for (int k = arm; k >= 1; k--)
        {
            var c = A(k == 1 ? color.Lightened(0.5f) : color, alpha * (1 - (float)(k - 1) / arm * 0.7f));
            Dot(canvas, at + new Vector2(k * Pixel, 0), c);
            Dot(canvas, at - new Vector2(k * Pixel, 0), c);
            Dot(canvas, at + new Vector2(0, k * Pixel), c);
            Dot(canvas, at - new Vector2(0, k * Pixel), c);
        }
        if (arm >= 3)
            foreach (var d in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                Dot(canvas, at + d * Pixel, A(color, alpha * 0.5f));
        Dot(canvas, at, A(White, alpha));
    }

    /// <summary>Une traînée : une tête claire en <paramref name="head"/>, une queue qui pâlit en arrière de <paramref name="dir"/>.</summary>
    private static void Streak(CanvasItem canvas, Vector2 head, Vector2 dir, float length, Color front, Color back, float alpha)
    {
        int steps = Math.Max(1, (int)(length / Pixel));
        for (int k = steps; k >= 0; k--)
        {
            float f = (float)k / steps;
            Dot(canvas, head - dir * k * Pixel, A(front.Lerp(back, f), alpha * (1 - f * f)));
        }
    }

    /// <summary>Une bouffée de fumée ou de poussière : bosselée, plus claire en haut, plus sombre en bas, le bord léger.</summary>
    private static void Puff(CanvasItem canvas, Vector2 at, float r, Color color, float alpha, int seed)
    {
        if (alpha <= 0.01f)
            return;
        int n = Math.Max(1, (int)(r / Pixel));
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            float d = (float)(x * x + y * y) / (n * n) + (Noise(seed, x * 17 + y * 5) - 0.5f) * 0.3f;
            if (d >= 1)
                continue;
            var c = y < -n * 0.3f ? color.Lightened(0.2f) : y > n * 0.35f ? color.Darkened(0.25f) : color;
            Dot(canvas, at + new Vector2(x, y) * Pixel, A(c, alpha * (d > 0.65f ? 0.5f : 1)));
        }
    }

    /// <summary>
    /// Une boule de feu qui bouillonne : un bord qui ondule, le cœur blanc, puis jaune, orange, rouge, braise ; qui file
    /// vers <paramref name="dir"/> (zéro : immobile), elle s'étire en arrière en une traîne de <paramref name="stretch"/>
    /// fois son rayon. <paramref name="cool"/> : la refroidir (une explosion qui retombe en fumée).
    /// </summary>
    private static void Blaze(CanvasItem canvas, Vector2 at, float radius, Vector2 dir, float stretch, int seed, float time, float alpha,
        Color[] palette, float cool = 0)
    {
        if (alpha <= 0.01f)
            return;
        float reach = radius * (1.25f + stretch);
        int n = (int)(reach / Pixel) + 1;
        int frame = (int)(time * 16);
        bool moving = dir != Vector2.Zero;
        float da = moving ? dir.Angle() : 0;
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            var rel = new Vector2(x, y) * Pixel;
            float dist = rel.Length();
            if (dist > reach)
                continue;
            float a = MathF.Atan2(rel.Y, rel.X);
            float r = radius * (0.92f + 0.13f * MathF.Sin(3 * a + time * 9 + seed) + 0.08f * MathF.Sin(5 * a - time * 14 + seed * 2)
                + 0.05f * MathF.Sin(9 * a + time * 21 + seed));
            float back = 0;
            if (moving)
            {
                back = Math.Max(0, -MathF.Cos(a - da));
                r *= 1 + stretch * back * back * back;
            }
            float d = dist / r;
            if (d >= 1)
                continue;
            float heat = 1 - d * 0.9f - back * d * 0.4f - cool + (Noise(seed + frame, x * 31 + y * 7) - 0.5f) * 0.16f;
            if (moving)
                heat += Math.Max(0, MathF.Cos(a - da)) * (1 - d) * 0.12f; // le cœur, un peu en avant
            if (heat < 0.05f)
                continue;
            Dot(canvas, at + rel, A(Heat(palette, heat), alpha * Math.Min(1, heat * 5)));
        }
    }

    /// <summary>
    /// Une langue de flamme posée en <paramref name="foot"/> : large en bas, pointue en haut, qui ondule, plus chaude au cœur
    /// et en bas ; son bord crépite.
    /// </summary>
    private static void Tongue(CanvasItem canvas, Vector2 foot, float width, float height, int seed, double time, float alpha, Color[] palette,
        float lean = 0)
    {
        if (alpha <= 0.01f || height < Pixel)
            return;
        int rows = (int)(height / Pixel);
        int frame = (int)(time * 16);
        for (int k = 0; k < rows; k++)
        {
            float f = (float)k / rows;
            float half = width * MathF.Pow(1 - f, 0.9f) * Math.Min(1, 0.6f + f * 3);
            float sway = MathF.Sin((float)time * 11 + seed + f * 3.5f) * f * f * 3 * U + MathF.Sin((float)time * 23 + seed * 2 + f * 9) * f * U * 0.6f
                + lean * f * height * 0.5f;
            int n = (int)(half / Pixel);
            for (int x = -n; x <= n; x++)
            {
                float g = n == 0 ? 0 : MathF.Abs(x) / (float)n;
                float heat = 1 - MathF.Max(f * 1.05f, g * 0.95f) + (Noise(seed + frame, k * 31 + x) - 0.5f) * 0.18f;
                if (heat < 0.06f)
                    continue;
                Dot(canvas, foot + new Vector2(sway + x * Pixel, -k * Pixel), A(Heat(palette, heat), alpha * Math.Min(1, heat * 4)));
            }
        }
    }

    /// <summary>
    /// Une pierre : une forme bosselée qui tourne (<paramref name="spin"/>), éclairée d'en haut à gauche, cernée de sombre.
    /// <paramref name="hot"/> : des fissures de lave, et le côté qui fend l'air (<paramref name="heading"/>) rougeoie.
    /// </summary>
    private static void Rock(CanvasItem canvas, Vector2 at, float radius, int seed, float spin, float hot, Vector2 heading, Color[] tones, float alpha = 1)
    {
        if (radius < Pixel * 0.5f || alpha <= 0.01f)
            return;
        int n = (int)(radius * 1.3f / Pixel) + 1;
        var light = new Vector2(-0.6f, -0.8f);
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            var rel = new Vector2(x, y) * Pixel;
            var turned = rel.Rotated(-spin);
            float a = MathF.Atan2(turned.Y, turned.X);
            float r = radius * (0.86f + 0.12f * MathF.Sin(3 * a + seed) + 0.08f * MathF.Sin(5 * a + seed * 2.3f) + 0.05f * MathF.Sin(7 * a + seed * 0.7f));
            float d = rel.Length() / r;
            if (d >= 1)
                continue;
            var normal = rel / r;
            float lit = 0.5f + 0.6f * normal.Dot(light) + (Noise(seed, x * 13 + y * 7) - 0.5f) * 0.25f;
            var c = d > 0.8f && radius > 2 * Pixel ? tones[0] : lit > 0.85f ? tones[3] : lit > 0.45f ? tones[2] : tones[1];
            if (hot > 0)
            {
                float crack = MathF.Abs(MathF.Sin((turned.X * 0.9f + turned.Y * 0.5f) / (Pixel * 1.7f) + seed + MathF.Sin(turned.Y / (Pixel * 2.3f) + seed) * 1.6f));
                if (crack < 0.2f && d < 0.8f)
                    c = c.Lerp(crack < 0.08f ? FireYellow : FireOrange, hot);
                float front = normal.Dot(heading);
                if (front > 0.25f)
                    c = c.Lerp(front > 0.6f ? FireYellow : FireOrange, hot * Math.Min(1, (front - 0.25f) * 1.6f));
            }
            Dot(canvas, at + rel, A(c, alpha));
        }
    }

    /// <summary>
    /// Une volute qui tourne autour d'une troupe : sa tête au moment <paramref name="u"/> (de 0 à 1), sa queue derrière elle ;
    /// <paramref name="place"/> donne, pour un moment, sa position et sa profondeur (z &lt; 0 : derrière la troupe).
    /// La tête est la plus grosse et la plus claire, la queue s'amincit et fonce le long de <paramref name="tones"/>.
    /// </summary>
    private static void Ribbon(CanvasItem canvas, float u, int samples, float spacing, float size, Func<float, (Vector2 At, float Z)> place,
        float alpha, params Color[] tones)
    {
        for (int j = samples; j >= 0; j--)
        {
            float v = u - j * spacing;
            if (v <= 0 || v >= 1)
                continue;
            var (at, z) = place(v);
            if (!Shows(z))
                continue;
            float f = (float)j / samples;
            // derrière la troupe, un peu plus pâle (dans l'ombre de son corps, là où il ne la cache pas tout à fait)
            Disc(canvas, at, (1 - f) * size, A(Ramp(f, tones), alpha * Bell(v) * (1 - f * 0.6f) * (z < 0 ? 0.8f : 1)));
        }
    }

    // ------------------------------------------------------------------ ce qui file du héros à la cible

    /// <summary>Le point du vol au moment <paramref name="k"/> : la flèche magique monte un peu en arc, la boule de feu file droit.</summary>
    private static Vector2 Flight(Effect e, float k) => e.Kind == "boule-de-feu" ? e.From.Lerp(e.To, k)
        : e.From.Lerp(e.To, k) - new Vector2(0, 0.08f * e.From.DistanceTo(e.To) * 4 * k * (1 - k));

    private static Vector2 Heading(Effect e, float k) =>
        (Flight(e, Math.Min(1, k + 0.01f)) - Flight(e, Math.Max(0, k - 0.01f))).Normalized();

    /// <summary>
    /// La flèche magique : un fer de lumière (blanc au cœur, violet, cerné de violet profond) et sa hampe, une lueur
    /// autour, un sillage qui ondule et s'amincit, des étincelles qui s'en détachent.
    /// </summary>
    private static void DrawMagicBolt(CanvasItem canvas, Effect e)
    {
        float k = (float)Math.Clamp(e.Age / e.Duration, 0, 1);
        float dist = Math.Max(1, e.From.DistanceTo(e.To));
        float time = (float)e.Age;
        var here = Flight(e, k);
        var dir = Heading(e, k);
        var side = new Vector2(-dir.Y, dir.X);
        Glow(canvas, here - dir * 4 * U, 16 * U, 11 * U, Violet, 0.3f);
        // le sillage : un ruban qui part de la hampe, du blanc au violet profond
        float trail = 50 * U;
        for (float d = 0; d < trail; d += Pixel)
        {
            float back = k - (d + 10 * U) / dist;
            if (back < 0)
                break;
            float f = d / trail;
            var p = Flight(e, back);
            float w = (1 - f) * (1.6f + 0.8f * MathF.Sin(d * 0.12f - time * 40)) * U;
            for (float s = -w; s <= w; s += Pixel)
            {
                float g = MathF.Abs(s) / Math.Max(w, 1);
                Dot(canvas, p + side * s, A(Ramp(Math.Max(f, g), VioletCore, Violet, VioletDeep), (1 - f) * (1 - g * 0.5f)));
            }
        }
        // des étincelles qui se détachent du sillage et s'éteignent
        for (int n = 0; n < 10; n++)
        {
            float life = (time * 3 + Noise(e.Seed, n)) % 1;
            float back = k - (10 + life * 30 + Noise(e.Seed, n + 5) * 10) * U / dist;
            if (back < 0)
                continue;
            var p = Flight(e, back) + side * (Noise(e.Seed, n + 20) - 0.5f) * 14 * U * life;
            Star(canvas, p, n % 3 == 0 ? 2 : 1, Violet, 1 - life);
        }
        ArrowHead(canvas, here - dir * 6 * U, dir, side);
    }

    /// <summary>Le fer et la hampe de la flèche magique, pointés vers <paramref name="dir"/> (la pointe à 11 unités devant <paramref name="at"/>).</summary>
    private static void ArrowHead(CanvasItem canvas, Vector2 at, Vector2 dir, Vector2 side)
    {
        int n = (int)(15 * U / Pixel);
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            var rel = new Vector2(x, y) * Pixel;
            float u = rel.Dot(dir) / U, v = MathF.Abs(rel.Dot(side)) / U;
            // le fer : un triangle, ses deux barbes en arrière ; la hampe : un trait qui part en arrière
            float head = u >= -3 && u <= 11 ? (11 - u) * 0.36f : -1;
            bool inHead = head > 0 && v <= head && !(u < 1.5f && v < (1.5f - u) * 1.2f && v > 0.9f);
            bool inShaft = u > -13 && u < 1 && v <= 1.1f;
            if (!inHead && !inShaft)
                continue;
            float edge = inHead ? Math.Max(head - v, inShaft ? 1.1f - v : 0) : 1.1f - v;
            var c = edge < 0.7f ? VioletDeep : v < 0.8f && (inShaft || u > -1) ? White : edge < 1.7f ? Violet : VioletCore;
            Dot(canvas, at + rel, c);
        }
    }

    /// <summary>La flèche se brise sur la cible : un éclair, un anneau qui s'élargit, des éclats qui filent en avant et ralentissent.</summary>
    private static void DrawMagicBurst(CanvasItem canvas, Effect e, float t)
    {
        var dir = (e.To - e.From).Normalized();
        Glow(canvas, e.To, (8 + 16 * t) * U, (6 + 12 * t) * U, Violet, 0.6f * (1 - t));
        if (t < 0.3f)
            Glow(canvas, e.To, 9 * U, 7 * U, VioletCore, 1 - t / 0.3f);
        Ring(canvas, e.To, (4 + 24 * t) * U, (3 + 18 * t) * U, 1.5f * U, VioletCore, 0.9f * (1 - t));
        for (int n = 0; n < 12; n++)
        {
            var d = dir.Rotated((Noise(e.Seed, n) - 0.5f) * 2.6f);
            float reach = (10 + 18 * Noise(e.Seed, n + 30)) * U * (1 - (1 - t) * (1 - t));
            Streak(canvas, e.To + d * reach, d, (2 + 5 * (1 - t)) * U, VioletCore, Violet, 1 - t);
        }
        for (int n = 0; n < 6; n++)
        {
            var at = e.To + new Vector2(Noise(e.Seed, n + 60) - 0.5f, Noise(e.Seed, n + 70) - 0.5f) * 30 * U;
            Star(canvas, at, 2, Violet, Bell(Math.Clamp(t * 1.6f - n * 0.08f, 0, 1)));
        }
    }

    /// <summary>
    /// La boule de feu en vol : une sphère de feu où tournent des bandes en spirale, enveloppée de flammes qui lèchent un
    /// peu vers l'arrière ; une lueur autour, des braises qui tombent, de la fumée derrière.
    /// </summary>
    private static void DrawFireball(CanvasItem canvas, Effect e)
    {
        float k = (float)Math.Clamp(e.Age / e.Duration, 0, 1);
        float dist = Math.Max(1, e.From.DistanceTo(e.To));
        float time = (float)e.Age;
        var here = Flight(e, k);
        var dir = Heading(e, k);
        var side = new Vector2(-dir.Y, dir.X);
        float radius = 10 * U;
        for (int n = 7; n >= 0; n--)
        {
            float back = k - (30 + n * 8) * U / dist;
            if (back < 0)
                continue;
            var p = Flight(e, back) - new Vector2(0, n * 1.5f * U);
            Puff(canvas, p, (2.5f + n * 0.8f) * U, Smoke, 0.5f * (1 - n / 8f), e.Seed + n + (int)(time * 4));
        }
        Glow(canvas, here, 30 * U, 22 * U, FireOrange, 0.35f);
        for (int n = 0; n < 16; n++)
        {
            float life = (time * 2.5f + Noise(e.Seed, n)) % 1;
            float back = k - (8 + life * 34) * U / dist;
            if (back < 0)
                continue;
            var p = Flight(e, back) + side * (Noise(e.Seed, n + 10) - 0.5f) * 14 * U + new Vector2(0, life * life * 16 * U);
            Disc(canvas, p, n % 4 == 0 ? 1 : 0, A(Heat(FireColors, 0.9f - life * 0.8f), 1 - life));
        }
        // une seule boule en feu : des flammes l'enveloppent et lèchent un peu vers l'arrière, la sphère brûle au milieu
        Blaze(canvas, here, radius * 1.3f, dir, 0.9f, e.Seed, time, 1, FireColors);
        SwirlBall(canvas, here + dir * 1.5f * U, radius * 0.8f, time * 9, e.Seed, time);
    }

    /// <summary>
    /// Une traîne de comète derrière <paramref name="at"/> : une forme qui s'effile sur <paramref name="length"/> fois le
    /// rayon, faite de bandes de flamme qui ondulent et s'effilochent au bout ; chaude contre la boule, rouge au bout.
    /// </summary>
    private static void CometTail(CanvasItem canvas, Vector2 at, Vector2 dir, float radius, float length, int seed, float time)
    {
        var side = new Vector2(-dir.Y, dir.X);
        float reach = radius * length;
        int frame = (int)(time * 18);
        for (float u = 0; u < reach; u += Pixel)
        {
            float f = u / reach;
            float half = radius * 1.05f * MathF.Pow(1 - f, 0.75f);
            for (float v = -half; v <= half; v += Pixel)
            {
                float g = MathF.Abs(v) / Math.Max(half, 1);
                // des bandes de flamme le long de la traîne, qui ondulent et se défont au bout
                float band = MathF.Sin(v / (radius * 0.28f) + MathF.Sin(u * 0.05f - time * 24 + seed) * 1.4f);
                float heat = 1 - f * 1.05f - g * 0.55f + band * 0.16f + (Noise(seed + frame, (int)(u * 7 + v)) - 0.5f) * 0.14f;
                if (heat < 0.08f || (f > 0.55f && band < -0.3f))
                    continue;
                Dot(canvas, at - dir * u + side * v, A(Heat(FireColors, heat), Math.Min(1, heat * 4)));
            }
        }
    }

    /// <summary>
    /// Une sphère de feu : des bandes en spirale qui tournent (<paramref name="spin"/>), un bord rouge sombre, un reflet
    /// clair en haut à gauche ; le bord frémit un peu.
    /// </summary>
    private static void SwirlBall(CanvasItem canvas, Vector2 at, float radius, float spin, int seed, float time, float alpha = 1)
    {
        int n = (int)(radius * 1.15f / Pixel) + 1;
        int frame = (int)(time * 16);
        for (int y = -n; y <= n; y++)
        for (int x = -n; x <= n; x++)
        {
            var rel = new Vector2(x, y) * Pixel;
            float a = MathF.Atan2(rel.Y, rel.X);
            float edge = radius * (1 + 0.06f * MathF.Sin(6 * a + time * 20 + seed));
            float d = rel.Length() / edge;
            if (d >= 1)
                continue;
            float swirl = MathF.Sin(a * 3 + d * 7 - spin);
            float heat = 1 - d * 0.62f + swirl * 0.17f + (Noise(seed + frame, x * 31 + y * 7) - 0.5f) * 0.08f;
            if (d > 0.84f)
                heat = Math.Min(heat, 0.3f);
            var lightSpot = rel / edge - new Vector2(-0.38f, -0.38f);
            if (lightSpot.Length() < 0.22f)
                heat = 1;
            Dot(canvas, at + rel, A(Heat(FireColors, heat), alpha));
        }
    }

    /// <summary>
    /// Une flamme large, comme un buisson de feu : une base ronde et trois langues (la plus haute au milieu, deux plus basses
    /// qui penchent vers l'extérieur), plus chaude en bas et au cœur.
    /// </summary>
    private static void FlameBush(CanvasItem canvas, Vector2 foot, float width, float height, int seed, double time, float alpha, Color[] palette)
    {
        if (alpha <= 0.01f || height < Pixel)
            return;
        float lean = 0.45f;
        Tongue(canvas, foot + new Vector2(-width * 0.55f, 0), width * 0.62f, height * (0.55f + 0.15f * Noise(seed, 1)), seed + 3, time, alpha, palette, -lean);
        Tongue(canvas, foot + new Vector2(width * 0.55f, 0), width * 0.62f, height * (0.55f + 0.15f * Noise(seed, 2)), seed + 7, time, alpha, palette, lean);
        Tongue(canvas, foot, width * 0.85f, height, seed, time, alpha, palette);
        // la base : un demi-disque très chaud qui relie les langues
        int nx = (int)(width * 1.2f / Pixel), ny = (int)(width * 0.55f / Pixel);
        for (int y = -ny; y <= 0; y++)
        for (int x = -nx; x <= nx; x++)
        {
            float d = (float)(x * x) / (nx * nx + 0.01f) + (float)(y * y) / (ny * ny + 0.01f);
            if (d < 1)
                Dot(canvas, foot + new Vector2(x, y) * Pixel, A(Heat(palette, 1 - d * 0.55f), alpha));
        }
    }

    // ------------------------------------------------------------------ la boule de feu explose

    private static void DrawExplosionGround(CanvasItem canvas, Effect e, float t)
    {
        float burnt = Math.Clamp(t * 6, 0, 1) * Math.Clamp((1 - t) * 3, 0, 1);
        t = (float)e.Age / 1.1f; // le choc, la lueur et l'onde gardent leur rythme
        // une lueur qui inonde toute la zone au moment du choc
        float glow = Math.Clamp(1 - t * 2.2f, 0, 1);
        Glow(canvas, e.Ground, 66 * U, 28 * U, FireOrange, 0.45f * glow);
        Glow(canvas, e.Ground, 34 * U, 15 * U, FireYellow, 0.55f * glow);
        // la terre brûlée, et des braises qui refroidissent dessus
        Glow(canvas, e.Ground, 48 * U, 21 * U, Scorch, 0.6f * burnt);
        for (int n = 0; n < 30; n++)
        {
            float a = Noise(e.Seed, n + 200) * MathF.Tau, r = MathF.Sqrt(Noise(e.Seed, n + 230));
            float heat = 0.75f - t * 0.9f + Noise(e.Seed, n + 260) * 0.25f;
            if (heat > 0.12f)
                Dot(canvas, e.Ground + new Vector2(MathF.Cos(a) * 40 * U, MathF.Sin(a) * 17 * U) * r, A(Heat(FireColors, heat), burnt));
        }
        // l'onde de choc : un anneau clair qui file sur le sol
        if (t < 0.4f)
        {
            float u = t / 0.4f;
            Ring(canvas, e.Ground, (10 + 62 * u) * U, (4 + 26 * u) * U, 2 * U, FireCore, 0.8f * (1 - u));
        }
    }

    /// <summary>
    /// L'explosion : une boule de feu qui gonfle au milieu puis se refroidit en montant ; sur chaque case, de hautes langues
    /// de flamme, des braises qui jaillissent en traînées, de la fumée qui monte à la fin.
    /// </summary>
    private static void DrawExplosion(CanvasItem canvas, Effect e, float t)
    {
        float time = (float)e.Age;
        t = time / 1.1f; // le choc, la fumée et les braises gardent leur rythme ; les flammes durent FlameLife
        for (int h = 0; h < e.Area.Length; h++)
        {
            var g = e.Area[h];
            for (int n = 0; n < 2; n++)
            {
                float u = (t - 0.3f - n * 0.12f) / 0.65f;
                if (u <= 0 || u >= 1)
                    continue;
                var at = g + new Vector2((Noise(e.Seed + h, n + 90) - 0.5f) * 18 * U, -(16 + 34 * u) * U);
                Puff(canvas, at, (4 + 7 * u) * U, Smoke, 0.55f * Bell(Math.Min(1, u * 1.6f + 0.2f)), e.Seed + h * 5 + n);
            }
        }
        if (t < 0.55f)
        {
            float u = t / 0.55f;
            Blaze(canvas, e.To - new Vector2(0, u * 12 * U), (8 + 22 * MathF.Sqrt(u)) * U, Vector2.Zero, 0, e.Seed, time,
                Math.Clamp((1 - u) * 2.5f, 0, 1), FireColors, u * 0.45f);
        }
        for (int h = 0; h < e.Area.Length; h++)
        {
            var g = e.Area[h];
            for (int n = 0; n < 5; n++)
            {
                float start = Noise(e.Seed + h, n) * 0.3f;
                float u = (time - start) / FlameLife;
                if (u <= 0 || u >= 1)
                    continue;
                float x = (Noise(e.Seed + h, n + 20) - 0.5f) * 30 * U;
                float y = (Noise(e.Seed + h, n + 30) - 0.5f) * 10 * U;
                float rise = MathF.Sin(MathF.Min(1, u * 3f) * MathF.PI * 0.5f) * (1 - u * u * 0.6f);
                FlameBush(canvas, g + new Vector2(x, y), (5 + 3 * Noise(e.Seed + h, n + 50)) * U, (30 + 22 * Noise(e.Seed + h, n + 40)) * U * rise,
                    e.Seed + h * 10 + n, e.Age, Math.Clamp((1 - u) * 4, 0, 1), FireColors);
            }
            if (t >= 1)
                continue;
            // des braises qui jaillissent en traînées et retombent
            for (int n = 0; n < 8; n++)
            {
                float a = Noise(e.Seed + h, n + 60) * MathF.Tau;
                float up = 20 + 22 * Noise(e.Seed + h, n + 70);
                Vector2 Spot(float s) => g + new Vector2(MathF.Cos(a) * 32 * s, -up * s + 44 * s * s) * U;
                var p = Spot(t);
                var d = (p - Spot(Math.Max(0, t - 0.03f))).Normalized();
                Streak(canvas, p, d, 3 * U, FireCore, FireOrange, 1 - t);
            }
        }
    }

    // ------------------------------------------------------------------ la pluie de météores

    /// <summary>Les météores par case : chacun tombe à sa place, un peu écarté du milieu de la case.</summary>
    private const int MeteorsPerHex = 2;

    /// <summary>Le moment où tombe le météore n, et où il part du ciel (en haut, un peu à droite de sa case).</summary>
    private static double MeteorStart(int n) => n * MeteorGap;

    private static Vector2 MeteorSky(Effect e, int n) => MeteorGround(e, n) + new Vector2((24 + Noise(e.Seed, n) * 16) * U, -110 * U);

    /// <summary>Où tombe le météore n : sur sa case (tour à tour sur chacune), un peu à côté du milieu.</summary>
    private static Vector2 MeteorGround(Effect e, int n) => e.Area[n % e.Area.Length]
        + (n < e.Area.Length ? Vector2.Zero : new Vector2((Noise(e.Seed, n + 300) - 0.5f) * 20 * U, (Noise(e.Seed, n + 310) - 0.5f) * 8 * U));

    private static void DrawMeteorsGround(CanvasItem canvas, Effect e)
    {
        float fade = (float)Math.Clamp((e.Duration - e.Age) / 0.4, 0, 1);
        for (int n = 0; n < e.Area.Length * MeteorsPerHex; n++)
        {
            double u = e.Age - MeteorStart(n) - MeteorFall;
            if (u < 0)
                continue;
            var at = MeteorGround(e, n);
            float glow = (float)Math.Clamp(1 - u / 0.35, 0, 1);
            Glow(canvas, at, 24 * U, 10 * U, FireOrange, 0.55f * glow);
            // le cratère : un creux sombre, bordé de roche fondue qui refroidit (jaune, orange, rouge, puis sombre)
            Glow(canvas, at, 14 * U, 6 * U, Scorch, 0.75f * fade);
            float heat = 0.85f - (float)u * 0.9f;
            Ring(canvas, at, 10 * U, 4.2f * U, 1.4f * U, heat > 0.17f ? Heat(FireColors, heat) : EarthDark, 0.9f * fade);
            Ring(canvas, at + new Vector2(0, -Pixel), 12 * U, 5 * U, 0.8f * U, Dust, 0.5f * fade);
        }
    }

    /// <summary>
    /// Chaque météore : une roche fissurée de lave, enveloppée d'un feu qui s'étire en longue traîne, et de fumée derrière ;
    /// au sol, un éclair, une onde, des éclats de roche qui volent en arc, de hautes flammes et un nuage de poussière.
    /// </summary>
    private static void DrawMeteors(CanvasItem canvas, Effect e)
    {
        float time = (float)e.Age;
        for (int n = 0; n < e.Area.Length * MeteorsPerHex; n++)
        {
            double since = e.Age - MeteorStart(n);
            if (since < 0)
                continue;
            var sky = MeteorSky(e, n);
            var ground = MeteorGround(e, n) - new Vector2(0, 4 * U);
            var dir = (ground - sky).Normalized();
            if (since < MeteorFall)
            {
                float k = (float)(since / MeteorFall);
                var at = sky.Lerp(ground, k * k);
                for (int j = 6; j >= 1; j--)
                {
                    float back = k - j * 0.07f;
                    if (back <= 0)
                        continue;
                    var p = sky.Lerp(ground, back * back) - new Vector2(0, j * U);
                    Puff(canvas, p, (2.5f + j * 0.9f) * U, Smoke, 0.5f * (1 - j / 7f), e.Seed + n * 9 + j);
                }
                Glow(canvas, at, 18 * U, 18 * U, FireOrange, 0.3f);
                Blaze(canvas, at, 8 * U, dir, 3.6f, e.Seed + n, time, 0.95f, FireColors);
                CometTail(canvas, at - dir * 4 * U, dir, 5 * U, 5, e.Seed + n, time);
                Rock(canvas, at + dir * 2 * U, 5.5f * U, e.Seed + n, time * 6 + n, 1, dir, MeteorRock);
                continue;
            }
            float u = (float)((since - MeteorFall) / 1.0);
            float burn = (float)((since - MeteorFall) / FlameLife);
            if (burn >= 1)
                continue;
            for (int m = 0; m < 3; m++)
            {
                float late = m * 0.06f;
                float v = burn - late;
                if (v <= 0)
                    continue;
                float x = (Noise(e.Seed + n, m) - 0.5f) * 22 * U;
                float y = (Noise(e.Seed + n, m + 30) - 0.5f) * 7 * U;
                float rise = MathF.Sin(MathF.Min(1, v * 3.5f) * MathF.PI * 0.5f) * (1 - v * v * 0.6f);
                FlameBush(canvas, ground + new Vector2(x, y + 4 * U), (4.5f + 2.5f * Noise(e.Seed + n, m + 50)) * U, (24 + 20 * Noise(e.Seed + n, m + 40)) * U * rise,
                    e.Seed + n * 10 + m, e.Age, Math.Clamp((1 - v) * 4, 0, 1), FireColors);
            }
            if (u >= 1)
                continue;
            if (u < 0.15f)
                Glow(canvas, ground, 20 * U, 14 * U, FireCore, 1 - u / 0.15f);
            if (u < 0.35f)
                Ring(canvas, ground + new Vector2(0, 4 * U), (8 + 30 * u / 0.35f) * U, (3.5f + 12 * u / 0.35f) * U, 1.5f * U, FireCore, 0.8f * (1 - u / 0.35f));
            // la poussière soulevée, qui s'étale au ras du sol
            for (int m = 0; m < 3; m++)
            {
                float side = m - 1;
                var at = ground + new Vector2(side * (6 + 20 * u) * U, (2 - 6 * u) * U);
                Puff(canvas, at, (4 + 6 * u) * U, Dust, 0.5f * (1 - u), e.Seed + n * 7 + m);
            }
            // des éclats de roche encore chauds qui volent en arc et retombent
            for (int m = 0; m < 6; m++)
            {
                float a = MathF.PI + (0.1f + 0.8f * Noise(e.Seed + n, m + 50)) * MathF.PI;
                float s = Math.Min(u * 1.4f, 0.75f);
                var at = ground + new Vector2(MathF.Cos(a) * 30 * s, MathF.Sin(a) * 26 * s + 46 * s * s) * U;
                Rock(canvas, at, (1.2f + Noise(e.Seed + n, m + 70)) * U, e.Seed + n * 11 + m, time * 9, 1 - u * 1.5f, Vector2.Zero, MeteorRock,
                    Math.Clamp((1 - u) * 3, 0, 1));
            }
        }
    }

    // ------------------------------------------------------------------ la chaîne d'éclairs

    /// <summary>
    /// L'arc d'une troupe à la suivante : un éclair (un cœur blanc, un halo bleu, une lueur), épais pour le premier, du
    /// héros à la première troupe, plus fin pour les sauts suivants ; des fourches qui partent de biais, une lueur aux deux
    /// bouts, et des étincelles qui jaillissent de la cible.
    /// </summary>
    private static void DrawArc(CanvasItem canvas, Effect e, float t)
    {
        int flicker = (int)(e.Age / 0.06);
        float strength = t < 0.55f ? 1 : (1 - t) / 0.45f;
        bool first = e.First;
        var path = Zigzag(e.From, e.To, e.Seed + flicker * 13);
        Glow(canvas, e.To, (first ? 18 : 14) * U, (first ? 14 : 11) * U, ArcHalo, 0.5f * strength);
        Glow(canvas, e.From, 10 * U, 8 * U, ArcHalo, 0.35f * strength);
        for (int n = 0; n < 5; n++)
        {
            int from = 1 + (int)(Noise(e.Seed + flicker, 10 + n) * (path.Length - 2));
            var dir = (path[Math.Min(from + 1, path.Length - 1)] - path[from]).Normalized();
            float side = Noise(e.Seed + flicker, 20 + n) < 0.5f ? -1 : 1;
            var fork = new Vector2[5];
            fork[0] = path[from];
            for (int k = 1; k < fork.Length; k++)
                fork[k] = fork[k - 1] + dir.Rotated(side * (0.4f + Noise(e.Seed + flicker, 30 + n * 7 + k) * 0.7f)) * (5 + Noise(e.Seed + flicker, 60 + n * 7 + k) * 5) * U;
            if (first)
                Bolt(canvas, fork, 1, 2, 3, 0.8f * strength);
            else
                Bolt(canvas, fork, 1, 1, 2, 0.8f * strength);
        }
        // en pixels d'art de part et d'autre du milieu : le premier arc fait 13 pixels de large, les suivants 9
        if (first)
            Bolt(canvas, path, 2, 4, 6, strength);
        else
            Bolt(canvas, path, 1, 3, 4, strength);
        for (int n = 0; n < 16; n++)
        {
            float a = Noise(e.Seed, n + 100) * MathF.Tau;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a) * 0.7f);
            Streak(canvas, e.To + d * (4 + 20 * t) * U, d, 3 * U * (1 - t), ArcCore, ArcHalo, 1 - t);
        }
    }

    /// <summary>
    /// Un éclair le long de <paramref name="path"/> : de part et d'autre du milieu, le cœur jusqu'à <paramref name="core"/>
    /// pixels d'art, le halo jusqu'à <paramref name="halo"/>, la lueur jusqu'à <paramref name="outer"/>.
    /// </summary>
    private static void Bolt(CanvasItem canvas, Vector2[] path, int core, int halo, int outer, float strength)
    {
        for (int k = 0; k < path.Length - 1; k++)
        {
            var a = path[k];
            var b = path[k + 1];
            var dir = (b - a).Normalized();
            var normal = new Vector2(-dir.Y, dir.X) * Pixel;
            for (int s = outer; s >= 0; s--)
            {
                var c = s < core ? A(ArcCore, strength) : s < halo ? A(ArcHalo, 0.85f * strength) : A(ArcOuter, 0.35f * strength);
                Line(canvas, a + normal * s, b + normal * s, c);
                if (s > 0)
                    Line(canvas, a - normal * s, b - normal * s, c);
            }
        }
    }

    /// <summary>Une ligne brisée de <paramref name="from"/> à <paramref name="to"/>, qui s'écarte de part et d'autre.</summary>
    private static Vector2[] Zigzag(Vector2 from, Vector2 to, int seed)
    {
        int steps = Math.Max(4, (int)(from.DistanceTo(to) / (12 * U)));
        var dir = (to - from) / steps;
        var normal = new Vector2(-dir.Y, dir.X).Normalized();
        var points = new Vector2[steps + 1];
        for (int k = 0; k <= steps; k++)
        {
            float off = k == 0 || k == steps ? 0 : (Noise(seed, k) - 0.5f) * 14 * U;
            points[k] = from + dir * k + normal * off;
        }
        return points;
    }

    // ------------------------------------------------------------------ les sorts joués sur une troupe

    private static Vector2 Head(Effect e) => e.Ground - new Vector2(0, e.Height * 0.85f);

    /// <summary>Une lueur derrière le corps : elle déborde de sa silhouette, comme un contour de lumière.</summary>
    private static void BackGlow(CanvasItem canvas, Effect e, Color color, float alpha)
    {
        if (_back)
            Glow(canvas, e.To, 22 * U, e.Height * 0.64f, color, alpha);
    }

    /// <summary>
    /// Une colonne de lumière qui descend du ciel sur la troupe (la bénédiction), et des grains qui tombent dedans
    /// (<paramref name="falling"/>) ou montent.
    /// </summary>
    private static void DrawShaft(CanvasItem canvas, Effect e, float t, Color color, Color core, bool falling)
    {
        float open = Math.Clamp(t * 4, 0, 1) * Math.Clamp((1 - t) * 3, 0, 1);
        float half = 14 * U * open;
        float top = e.Ground.Y - e.Height - 60 * U;
        // la colonne : des bandes verticales, plus claires au milieu, qui s'effacent vers le haut
        for (float x = -half; x <= half; x += Pixel)
        {
            float side = Math.Abs(x) / Math.Max(1, half);
            var c = side < 0.35f ? core : color;
            for (float y = top; y < e.Ground.Y; y += Pixel * 2)
            {
                float up = (e.Ground.Y - y) / (e.Ground.Y - top);
                float alpha = 0.6f * open * (1 - up * up) * (1 - side * 0.5f);
                if (alpha > 0.02f)
                    Dot(canvas, new Vector2(e.Ground.X + x, y + ((int)(x / Pixel) & 1) * Pixel), A(c, alpha));
            }
        }
        for (int n = 0; n < 20; n++)
        {
            float u = (t * 1.6f + Noise(e.Seed, n)) % 1;
            float x = (Noise(e.Seed, n + 20) - 0.5f) * 24 * U;
            float y = falling ? top + 40 * U + u * (e.Ground.Y - top - 40 * U) : e.Ground.Y - u * (e.Height + 10 * U);
            var at = new Vector2(e.Ground.X + x, y);
            var c = A(n % 3 == 0 ? White : core, Bell(u) * open);
            if (n % 3 == 0)
                Cross(canvas, at, 2, c);
            else if (n % 3 == 1)
                Blob(canvas, at, 1, c);
            else
                Dot(canvas, at, c);
        }
    }

    /// <summary>Une flaque de lumière au pied de la troupe (la bénédiction).</summary>
    private static void DrawLightPool(CanvasItem canvas, Effect e)
    {
        float t = (float)(e.Age / e.Duration);
        float open = Math.Clamp(t * 4, 0, 1) * Math.Clamp((1 - t) * 3, 0, 1);
        Ellipse(canvas, e.Ground, 20 * U, 8 * U, A(Gold, 0.5f * open), e.Seed);
        Ellipse(canvas, e.Ground, 11 * U, 4.5f * U, A(White, 0.55f * open), e.Seed + 1);
    }

    /// <summary>Une flaque de lumière douce au pied de la troupe (soins, résurrection), et une onde qui s'en écarte.</summary>
    private static void DrawHealPool(CanvasItem canvas, Effect e, float t, Color color, Color core)
    {
        float open = Hold(t);
        Glow(canvas, e.Ground, 24 * U, 10 * U, color, 0.55f * open);
        Glow(canvas, e.Ground, 12 * U, 5 * U, core, 0.7f * open);
        float u = (t * 1.6f) % 1;
        Ring(canvas, e.Ground, (8 + 20 * u) * U, (3.2f + 8 * u) * U, 1 * U, core, 0.6f * (1 - u) * open);
    }

    /// <summary>
    /// Une colonne de lumière plus douce que celle de la bénédiction : un dégradé du milieu vers les bords, des rayons plus
    /// clairs qui coulent vers le bas, et qui s'efface vers le haut.
    /// </summary>
    private static void SoftShaft(CanvasItem canvas, Effect e, float t, Color color, Color core, float width)
    {
        float open = Math.Clamp(t * 4, 0, 1) * Math.Clamp((1 - t) * 3, 0, 1);
        float half = width * U * open;
        float top = e.Ground.Y - e.Height - 60 * U;
        for (float x = -half; x <= half; x += Pixel)
        {
            float side = Math.Abs(x) / Math.Max(1, half);
            int column = (int)(x / Pixel);
            float ray = Noise(e.Seed, column + 500);
            for (float y = top; y < e.Ground.Y; y += Pixel)
            {
                float up = (e.Ground.Y - y) / (e.Ground.Y - top);
                // des rayons plus clairs, qui descendent
                float flow = ray > 0.7f ? 0.5f + 0.5f * MathF.Sin(y * 0.05f - (float)e.Age * 14 + column) : 0;
                float level = (1 - side * side) * (1 - up * up);
                float alpha = open * MathF.Ceiling((0.35f * level + 0.25f * flow * level) * 8) / 8;
                if (alpha > 0.03f && ((int)(y / Pixel) + column & 1) == 0 | side < 0.4f)
                    Dot(canvas, new Vector2(e.Ground.X + x, y), A(side < 0.3f + flow * 0.2f ? core : color, alpha));
            }
        }
    }

    /// <summary>Les soins : une colonne verte, une lueur derrière la troupe, des croix cernées de vert qui montent, des étincelles.</summary>
    private static void DrawHealing(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        BackGlow(canvas, e, Green, 0.4f * open);
        if (!_back)
            SoftShaft(canvas, e, t, Green, GreenPale, 13);
        for (int n = 0; n < 9; n++)
        {
            float u = t * 1.4f - n * 0.07f;
            if (u <= 0 || u >= 1)
                continue;
            float a = Noise(e.Seed, n + 50) * MathF.Tau + u * 2;
            float z = MathF.Sin(a);
            if (!Shows(z))
                continue;
            var at = e.Ground + new Vector2(MathF.Cos(a) * 16 * U, z * 5 * U - (6 + u * (e.Height + 4 * U)));
            float alpha = Bell(u) * (z < 0 ? 0.7f : 1);
            int arm = n % 3 == 0 ? 3 : 2;
            foreach (var d in new[] { new Vector2(Pixel, 0), new Vector2(-Pixel, 0), new Vector2(0, Pixel), new Vector2(0, -Pixel) })
                Cross(canvas, at + d, arm, A(GreenDeep, alpha * 0.8f));
            Cross(canvas, at, arm, A(Green, alpha));
            Dot(canvas, at, A(White, alpha));
        }
        if (!_back)
            for (int n = 0; n < 8; n++)
            {
                var at = e.To + new Vector2(Noise(e.Seed, n + 80) - 0.5f, Noise(e.Seed, n + 90) - 0.5f) * new Vector2(30 * U, e.Height);
                Star(canvas, at, n % 2 == 0 ? 2 : 1, GreenPale, Bell(Math.Clamp(t * 2 - Noise(e.Seed, n + 70), 0, 1)) * open);
            }
    }

    /// <summary>
    /// La résurrection : une colonne dorée et ses rayons, des âmes (une tête blanche, une traîne d'or) qui montent du sol
    /// en tournant autour de la troupe et la rejoignent, un anneau de lumière qui monte des pieds à la tête.
    /// </summary>
    private static void DrawResurrection(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        BackGlow(canvas, e, Gold, 0.45f * open);
        if (!_back)
            SoftShaft(canvas, e, t, Gold, GoldPale, 16);
        for (int n = 0; n < 7; n++)
        {
            float start = Noise(e.Seed, n + 70) * 0.35f;
            float a0 = Noise(e.Seed, n + 80) * MathF.Tau;
            float u = (t - start) / 0.6f;
            Ribbon(canvas, u, 14, 0.025f, 2.2f, v =>
            {
                float a = a0 + v * 5;
                float r = (1 - v * 0.7f) * 20 * U;
                return (e.Ground + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.3f - v * e.Height * 0.75f), MathF.Sin(a));
            }, 0.95f, White, GoldPale, Gold, GoldDeep);
        }
        float ring = Math.Clamp((t - 0.35f) / 0.55f, 0, 1);
        if (ring > 0 && ring < 1)
            Ring(canvas, e.Ground - new Vector2(0, ring * e.Height), 16 * U, 5 * U, 1.2f * U, GoldPale, Bell(ring), true);
    }

    /// <summary>Une ombre au pied de la troupe (malédiction, faiblesse), plus sombre au milieu.</summary>
    private static void DrawShadowPool(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        var color = e.Kind == "malediction" ? PurpleDark : EarthDark;
        Glow(canvas, e.Ground, 24 * U, 10 * U, color, 0.75f * open);
        if (e.Kind == "malediction")
            Ring(canvas, e.Ground, (12 + 10 * t) * U, (5 + 4 * t) * U, 0.8f * U, Purple, 0.5f * open);
    }

    /// <summary>
    /// La malédiction : trois volutes de fumée violette qui tournent autour de la troupe en descendant (son corps cache
    /// celles de derrière), une lueur violette derrière elle, des gouttes d'ombre qui tombent ; la troupe s'assombrit.
    /// </summary>
    private static void DrawCurse(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        BackGlow(canvas, e, Purple, 0.35f * open);
        var head = Head(e);
        for (int w = 0; w < 3; w++)
        {
            float phase = w * MathF.Tau / 3;
            Ribbon(canvas, t * 1.15f - w * 0.1f, 24, 0.013f, 2.8f, u =>
            {
                float a = u * 7.5f + phase;
                float r = (18 - 6 * u) * U;
                return (head + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.3f + u * e.Height * 0.8f), MathF.Sin(a));
            }, 1, VioletCore, Violet, Purple, PurpleDark);
        }
        for (int n = 0; n < 10; n++)
        {
            float u = (t * 1.3f + Noise(e.Seed, n)) % 1;
            float z = Noise(e.Seed, n + 40) - 0.5f;
            if (!Shows(z))
                continue;
            var at = new Vector2(e.Ground.X + (Noise(e.Seed, n + 20) - 0.5f) * 26 * U, head.Y + u * e.Height * 0.85f);
            float alpha = Bell(u) * open;
            Disc(canvas, at, 1, A(PurpleDark, alpha));
            Dot(canvas, at - new Vector2(0, 2 * Pixel), A(Purple, alpha * 0.6f));
            Dot(canvas, at - new Vector2(0, 3 * Pixel), A(Purple, alpha * 0.3f));
        }
    }

    /// <summary>
    /// La faiblesse : la force quitte la troupe : une lueur chaude derrière elle qui s'éteint et grisaille ; quatre
    /// coulées partent de ses épaules et descendent en tournant autour d'elle jusqu'au sol (chaudes au départ, grises en
    /// bas ; derrière elle pour moitié), où elles s'étalent ; des grains gris s'écoulent ; la troupe se ternit.
    /// </summary>
    private static void DrawWeakness(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        var warm = new Color(1, 0.72f, 0.38f);
        // la lueur de la force : chaude au début, elle s'éteint en gris
        BackGlow(canvas, e, warm.Lerp(Grey, Math.Clamp(t * 1.6f, 0, 1)), (0.7f - 0.3f * t) * open);
        for (int w = 0; w < 4; w++)
        {
            float phase = w * MathF.Tau / 4 + 0.4f;
            Ribbon(canvas, t * 1.2f - w * 0.09f, 26, 0.014f, 3.2f, u =>
            {
                float a = phase + u * 4.2f;
                float r = (10 + 8 * u) * U;
                return (e.Ground + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.3f - (1 - u) * e.Height * 0.72f), MathF.Sin(a));
            }, open, new Color(0.92f, 0.9f, 0.84f), Grey, new Color(0.2f, 0.19f, 0.16f));
            // la tête de la coulée garde encore un peu de chaleur
            float head = t * 1.2f - w * 0.09f;
            if (head > 0 && head < 0.6f)
            {
                float a = phase + head * 4.2f;
                float z = MathF.Sin(a);
                if (Shows(z))
                {
                    float r = (10 + 8 * head) * U;
                    var at = e.Ground + new Vector2(MathF.Cos(a) * r, z * r * 0.3f - (1 - head) * e.Height * 0.72f);
                    Disc(canvas, at, 2.4f, A(warm, open * (1 - head / 0.6f)));
                }
            }
        }
        for (int n = 0; n < 12; n++)
        {
            float u = (t * 1.3f + Noise(e.Seed, n)) % 1;
            float z = Noise(e.Seed, n + 40) - 0.5f;
            if (!Shows(z))
                continue;
            var at = new Vector2(e.Ground.X + (Noise(e.Seed, n + 30) - 0.5f) * 24 * U, e.Ground.Y - e.Height * 0.75f * (1 - u));
            float alpha = Bell(u) * open;
            Disc(canvas, at, 0.8f, A(Grey, alpha));
            Dot(canvas, at - new Vector2(0, 2 * Pixel), A(Grey, alpha * 0.5f));
        }
    }

    /// <summary>
    /// La peau de pierre : des éclats de pierre arrivent en spirale de tout autour (ceux de derrière passent derrière la
    /// troupe), se posent sur son corps et s'y fondent dans une bouffée de poussière ; la troupe prend la couleur de la pierre
    /// (<see cref="Look"/>), puis des reflets passent sur elle.
    /// </summary>
    private static void DrawStoneSkin(CanvasItem canvas, Effect e, float t)
    {
        float time = (float)e.Age;
        for (int n = 0; n < 14; n++)
        {
            float start = Noise(e.Seed, n + 10) * 0.3f;
            float u = (t - start) / 0.38f;
            if (u <= 0)
                continue;
            float a0 = Noise(e.Seed, n) * MathF.Tau;
            float h = (Noise(e.Seed, n + 30) - 0.5f) * e.Height * 0.6f;
            float rest = (5 + 4 * Noise(e.Seed, n + 40)) * U;
            float size = (1.8f + 1.2f * Noise(e.Seed, n + 50)) * U;
            if (u < 1)
            {
                float ease = 1 - (1 - u) * (1 - u);
                float a = a0 + ease * 2.4f;
                float z = MathF.Sin(a);
                if (!Shows(z))
                    continue;
                float r = (34 + 8 * Noise(e.Seed, n + 20)) * U * (1 - ease) + rest * ease;
                var at = e.To + new Vector2(MathF.Cos(a) * r, z * r * 0.32f + h * ease - (1 - ease) * 22 * U);
                Rock(canvas, at, size * (1 + z * 0.15f), e.Seed + n, time * 7 + n, 0, Vector2.Zero, GreyRock);
            }
            else if (u < 1.7f)
            {
                // posée : elle rentre dans la peau, une bouffée de poussière s'en échappe
                float v = (u - 1) / 0.7f;
                float a = a0 + 2.4f;
                float z = MathF.Sin(a);
                if (!Shows(z))
                    continue;
                var at = e.To + new Vector2(MathF.Cos(a) * rest, z * rest * 0.32f + h);
                if (v < 0.4f)
                    Rock(canvas, at, size * (1 - v / 0.4f), e.Seed + n, time * 7 + n, 0, Vector2.Zero, GreyRock);
                Puff(canvas, at - new Vector2(0, v * 6 * U), (1.5f + 3 * v) * U, Dust, 0.55f * (1 - v), e.Seed + n);
            }
        }
        if (!_back && t > 0.55f)
            for (int n = 0; n < 6; n++)
            {
                float u = Math.Clamp(((t - 0.55f) / 0.45f) * 1.6f - n * 0.1f, 0, 1);
                var at = e.To + new Vector2((Noise(e.Seed, n + 60) - 0.5f) * 22 * U, (Noise(e.Seed, n + 70) - 0.5f) * e.Height * 0.8f);
                Star(canvas, at, n % 2 == 0 ? 3 : 2, StoneLight, Bell(u));
            }
    }

    /// <summary>La soif de sang au sol : une flaque rouge, et un anneau à chaque battement de cœur.</summary>
    private static void DrawBloodPool(CanvasItem canvas, Effect e, float t)
    {
        Glow(canvas, e.Ground, 22 * U, 9 * U, Blood, (0.3f + 0.3f * Beat(t)) * Hold(t));
        foreach (float start in new[] { 0.05f, 0.4f })
        {
            float u = (t - start) / 0.45f;
            if (u > 0 && u < 1)
                Ring(canvas, e.Ground, (8 + 20 * u) * U, (3.2f + 8 * u) * U, 1.2f * U, BloodLight, 0.8f * (1 - u));
        }
    }

    /// <summary>
    /// La soif de sang : des flammes rouges de rage autour du corps (celles de derrière derrière lui), qui grandissent à
    /// chaque battement de cœur, des gouttes qui montent en tournant ; la troupe rougit à chaque battement (<see cref="Look"/>).
    /// </summary>
    private static void DrawBloodlust(CanvasItem canvas, Effect e, float t)
    {
        float beat = Beat(t), open = Hold(t);
        BackGlow(canvas, e, Blood, 0.3f * open + 0.25f * beat);
        for (int n = 0; n < 9; n++)
        {
            float a = n * MathF.Tau / 9 + Noise(e.Seed, n) * 0.5f;
            float z = MathF.Sin(a);
            if (!Shows(z))
                continue;
            var foot = e.Ground + new Vector2(MathF.Cos(a) * 14 * U, z * 5 * U);
            float tall = (e.Height * 0.5f + 12 * U * Noise(e.Seed, n + 9)) * (0.55f * open + 0.5f * beat);
            Tongue(canvas, foot, (2.5f + 1.5f * Noise(e.Seed, n + 4)) * U, tall, e.Seed + n, e.Age, 0.8f * open * (z < 0 ? 0.85f : 1), BloodColors);
        }
        for (int n = 0; n < 12; n++)
        {
            float u = (t * 1.3f + Noise(e.Seed, n + 20)) % 1;
            float a = Noise(e.Seed, n + 30) * MathF.Tau + u * 3;
            float z = MathF.Sin(a);
            if (!Shows(z))
                continue;
            var at = e.Ground + new Vector2(MathF.Cos(a) * 17 * U, z * 5 * U - u * (e.Height + 10 * U));
            float alpha = Bell(u) * open;
            Drop(canvas, at, alpha);
        }
    }

    /// <summary>Une goutte de sang qui monte : ronde, un reflet clair, une traîne sous elle.</summary>
    private static void Drop(CanvasItem canvas, Vector2 at, float alpha)
    {
        for (int k = 3; k >= 1; k--)
            Dot(canvas, at + new Vector2(0, (k + 1) * Pixel), A(Blood, alpha * (0.8f - k * 0.2f)));
        Disc(canvas, at, 1, A(Blood, alpha));
        Dot(canvas, at + new Vector2(Pixel, Pixel), A(BloodDark, alpha));
        Dot(canvas, at - new Vector2(Pixel, Pixel), A(BloodLight, alpha));
    }

    /// <summary>
    /// La hâte : des rubans de vent qui s'enroulent autour de la troupe en montant et filent vers l'avant (ceux de derrière
    /// derrière elle), des traits de vitesse en arrière, de la poussière soulevée ; la troupe laisse des images fantômes
    /// derrière elle (<see cref="Look"/>).
    /// </summary>
    private static void DrawHaste(CanvasItem canvas, Effect e, float t)
    {
        float open = Math.Clamp((1 - t) * 3, 0, 1) * Math.Clamp(t * 6, 0, 1);
        BackGlow(canvas, e, Sky, 0.22f * open);
        for (int w = 0; w < 4; w++)
        {
            float y0 = e.Ground.Y - (0.08f + 0.18f * w) * e.Height;
            float phase = w * 1.7f;
            Ribbon(canvas, t * 1.5f - w * 0.12f, 30, 0.011f, 1.8f, u =>
            {
                float a = u * 9 + phase;
                var at = new Vector2(e.Ground.X + MathF.Cos(a) * 14 * U + e.Facing * (u - 0.5f) * 26 * U, y0 - u * e.Height * 0.4f + MathF.Sin(a) * 4 * U);
                return (at, MathF.Sin(a));
            }, open, White, Wind, Sky);
        }
        if (_back)
            return;
        for (int n = 0; n < 8; n++)
        {
            float u = (t * 2 + Noise(e.Seed, n)) % 1;
            float y = e.Ground.Y - (0.1f + 0.8f * Noise(e.Seed, n + 10)) * e.Height;
            float x = e.Ground.X + e.Facing * (-34 + 50 * u) * U;
            Streak(canvas, new Vector2(x, y), new Vector2(e.Facing, 0), (8 + 10 * Noise(e.Seed, n + 20)) * U, White, Sky, Bell(u) * open);
        }
        for (int n = 0; n < 4; n++)
        {
            float u = (t * 1.6f + n * 0.25f) % 1;
            var at = e.Ground + new Vector2(-e.Facing * (4 + 18 * u) * U, (2 - 4 * u) * U);
            Puff(canvas, at, (1.5f + 3 * u) * U, Dust, 0.45f * (1 - u) * open, e.Seed + n);
        }
    }

    /// <summary>
    /// La lenteur au sol : un cadran de sable (un dégradé, un bord sombre, douze repères) et son aiguille, qui tourne de plus
    /// en plus lentement.
    /// </summary>
    private static void DrawSlowDial(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        Glow(canvas, e.Ground, 22 * U, 9 * U, Sand, 0.4f * open);
        Ring(canvas, e.Ground, 20 * U, 8 * U, 1.1f * U, SandDark, 0.9f * open);
        Ring(canvas, e.Ground + new Vector2(0, -Pixel), 20 * U, 8 * U, 0.5f * U, Sand.Lightened(0.3f), 0.8f * open);
        for (int n = 0; n < 12; n++)
        {
            float a = n * MathF.Tau / 12;
            var inner = e.Ground + new Vector2(MathF.Cos(a) * 14, MathF.Sin(a) * 5.6f) * U;
            var outer = e.Ground + new Vector2(MathF.Cos(a) * 17.5f, MathF.Sin(a) * 7) * U;
            Line(canvas, inner, outer, A(SandDark, open));
            Dot(canvas, outer, A(n % 3 == 0 ? White : Sand, open));
        }
        float hand = -MathF.PI / 2 + 5 * (1 - (1 - t) * (1 - t));
        var tip = e.Ground + new Vector2(MathF.Cos(hand) * 15, MathF.Sin(hand) * 6) * U;
        for (int k = -1; k <= 1; k++)
            Line(canvas, e.Ground + new Vector2(0, k * Pixel), tip, A(k == 0 ? Sand.Lightened(0.3f) : Wood, open));
        Disc(canvas, e.Ground, 1.5f, A(Wood, open));
        Dot(canvas, e.Ground, A(Sand, open));
    }

    /// <summary>
    /// La lenteur : un sablier au-dessus de la troupe, dont le sable coule, et du sable qui tombe lentement autour d'elle en
    /// tournant (derrière elle pour la moitié) ; la troupe prend la couleur du sable.
    /// </summary>
    private static void DrawSlow(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        BackGlow(canvas, e, Sand, 0.25f * open);
        for (int n = 0; n < 22; n++)
        {
            float u = (t * 0.7f + Noise(e.Seed, n)) % 1;
            float a = Noise(e.Seed, n + 30) * MathF.Tau + u * 2;
            float z = MathF.Sin(a);
            if (!Shows(z))
                continue;
            var at = new Vector2(e.Ground.X + MathF.Cos(a) * 15 * U, e.Ground.Y - e.Height - 4 * U + u * (e.Height + 4 * U) + z * 4 * U);
            float alpha = Bell(u) * open;
            Dot(canvas, at, A(n % 2 == 0 ? Sand : SandDark, alpha));
            Dot(canvas, at - new Vector2(0, Pixel), A(Sand, alpha * 0.5f));
        }
        if (!_back)
            Hourglass(canvas, Head(e) - new Vector2(0, 22 * U + MathF.Sin((float)e.Age * 3) * U), t, open);
    }

    /// <summary>
    /// Un sablier ouvragé : deux plateaux de bois arrondis (un dessus clair, un dessous sombre), trois colonnes tournées de
    /// chaque côté (renflées, éclairées à gauche), deux bulbes de verre (un bord clair, un reflet), le sable qui passe du haut
    /// vers le bas (<paramref name="t"/> de 0 à 1) en un filet, des grains qui brillent ; une lueur dorée derrière.
    /// </summary>
    private static void Hourglass(CanvasItem canvas, Vector2 center, float t, float alpha)
    {
        if (alpha <= 0.01f)
            return;
        float w = 9 * U, h = 14 * U, cap = 2.4f * U;
        float inner = h - cap;
        float sand = 0.15f + 0.85f * t; // la part déjà tombée
        Glow(canvas, center, w * 2.2f, h * 1.5f, Gold, 0.35f * alpha);
        var woodLight = new Color(0.72f, 0.48f, 0.22f);
        var woodDark = new Color(0.24f, 0.13f, 0.05f);
        var glassEdge = new Color(0.9f, 0.96f, 1);
        int nx = (int)((w + 2 * U) / Pixel) + 1, ny = (int)(h / Pixel) + 1;
        for (int y = -ny; y <= ny; y++)
        for (int x = -nx; x <= nx; x++)
        {
            float px = x * Pixel, py = y * Pixel, ax = MathF.Abs(px), ay = MathF.Abs(py);
            if (ay > h)
                continue;
            Color? c = null;
            // les plateaux : arrondis aux coins, un dessus clair, une moulure, un dessous sombre
            float capHalf = w + 2 * U - Math.Max(0, ay - (h - Pixel * 1.5f)) * 1.5f;
            if (ay >= inner && ax <= capHalf)
            {
                float k = (ay - inner) / cap; // 0 : contre le verre, 1 : le bord
                bool top = py < 0;
                c = (top ? k > 0.75f : k < 0.25f) ? woodLight : (top ? k < 0.2f : k > 0.8f) ? woodDark : MathF.Abs(k - 0.5f) < 0.1f ? woodDark : Wood;
            }
            else if (ay < inner)
            {
                // trois colonnes tournées de chaque côté : renflées au milieu et près des plateaux
                float f = ay / inner;
                float bulge = 0.7f * U * (MathF.Cos(f * MathF.PI * 2.5f) * 0.5f + 0.5f) + 0.6f * U;
                float post = w + 0.9f * U;
                if (MathF.Abs(ax - post) <= bulge)
                {
                    float lit = (px < 0 ? -1 : 1) * (ax - post) / bulge; // éclairée du côté gauche de chaque colonne
                    c = lit < -0.3f ? woodLight : lit > 0.4f ? woodDark : Wood;
                }
                else
                {
                    float gw = MathF.Pow(f, 0.75f) * (w - 1.2f * U) + 0.7f * U;
                    if (ax <= gw)
                    {
                        bool edge = ax > gw - Pixel;
                        bool full = py < 0 ? -py <= inner * (1 - sand) : py >= inner * (1 - sand) + ax * 0.3f;
                        bool stream = py >= 0 && ax < Pixel && t < 0.97f;
                        bool shine = !edge && px < 0 && ax > gw - 3 * Pixel && ax < gw - Pixel && f > 0.25f && f < 0.8f;
                        if (edge)
                            c = new Color(glassEdge, 0.85f);
                        else if (full || stream)
                            c = Noise(x, y + 900) > 0.93f ? White : (x + y & 1) == 0 ? Sand : new Color(0.85f, 0.62f, 0.28f);
                        else if (shine)
                            c = new Color(White, 0.6f);
                        else
                            c = new Color(Wind, 0.22f);
                    }
                }
            }
            if (c is { } color)
                Dot(canvas, center + new Vector2(px, py), A(color, color.A * alpha));
        }
        // quelques grains qui brillent en tombant dans le filet
        for (int n = 0; n < 3; n++)
        {
            float u = ((float)n / 3 + t * 6) % 1;
            if (t < 0.97f)
                Dot(canvas, center + new Vector2(0, u * inner * (1 - sand)), A(White, alpha * Bell(u)));
        }
    }

    /// <summary>
    /// Le bouclier contre les tirs : un bouclier de lumière courbe, du côté de l'ennemi, fait d'un treillis d'hexagones qui
    /// s'allument du milieu vers les bords ; un bord blanc, un reflet qui le parcourt, des étincelles. Aussi l'aura du sort,
    /// plus pâle.
    /// </summary>
    private static void Shield(CanvasItem canvas, Vector2 body, float height, float facing, float time, float reveal, float alpha, int seed)
    {
        if (alpha <= 0.01f)
            return;
        var center = body + new Vector2(facing * 6 * U, 0);
        float rx = 12 * U, ry = height * 0.6f + 6 * U, thick = 7 * U, cell = 3.2f * U;
        int nx = (int)(rx / Pixel) + 2, ny = (int)(ry / Pixel) + 1;
        float sweep = (time * 0.9f) % 1.6f - 0.3f;
        for (int y = -ny; y <= ny; y++)
        {
            float ly = y * Pixel / ry;
            if (MathF.Abs(ly) >= 1)
                continue;
            float curve = rx * MathF.Sqrt(1 - ly * ly);
            float inside = curve - thick * MathF.Sqrt(1 - ly * ly) - Pixel;
            if (MathF.Abs(ly) > reveal)
                continue;
            for (int x = -nx; x <= nx; x++)
            {
                float lx = x * Pixel;
                if (lx > curve || lx < inside)
                    continue;
                var at = center + new Vector2(facing * lx, y * Pixel);
                float g = (lx - inside) / Math.Max(Pixel, curve - inside); // 0 : le bord intérieur, 1 : le bord extérieur
                float edge = HexEdge(at.X, at.Y, cell);
                float shine = MathF.Max(0, 1 - MathF.Abs((ly + 1) / 2 - sweep) * 7);
                Color c;
                float a;
                if (curve - lx < Pixel * 1.5f)
                {
                    c = White;
                    a = 0.95f;
                }
                else if (edge > 0.82f)
                {
                    c = Wind;
                    a = 0.55f + 0.3f * g;
                }
                else
                {
                    c = g < 0.3f ? SkyDeep : Sky;
                    a = 0.18f + 0.18f * g;
                }
                a += shine * 0.4f;
                Dot(canvas, at, A(shine > 0.5f ? c.Lightened(0.4f) : c, a * alpha));
            }
        }
        for (int n = 0; n < 5; n++)
        {
            float ly = MathF.Sin(time * 2.3f + n * 1.9f + seed) * 0.9f * reveal;
            var at = center + new Vector2(facing * rx * MathF.Sqrt(1 - ly * ly), ly * ry);
            Star(canvas, at, 2, Wind, alpha * (0.5f + 0.5f * MathF.Sin(time * 7 + n)));
        }
    }

    /// <summary>Près de 1 sur le bord d'un hexagone d'un treillis de cellules de <paramref name="size"/>, près de 0 en son milieu.</summary>
    private static float HexEdge(float x, float y, float size)
    {
        float q = 2f / 3 * x / size, r = (-1f / 3 * x + MathF.Sqrt(3) / 3 * y) / size, s = -q - r;
        float rq = MathF.Round(q), rr = MathF.Round(r), rs = MathF.Round(s);
        float dq = MathF.Abs(rq - q), dr = MathF.Abs(rr - r), ds = MathF.Abs(rs - s);
        if (dq > dr && dq > ds)
            rq = -rr - rs;
        else if (dr > ds)
            rr = -rq - rs;
        else
            rs = -rq - rr;
        return 2 * MathF.Max(MathF.Abs(q - rq), MathF.Max(MathF.Abs(r - rr), MathF.Abs(s - rs)));
    }

    /// <summary>
    /// L'aveuglement : un éclair blanc devant les yeux (une lueur et des rayons), puis un bandeau de fumée noire qui se serre
    /// autour de la tête en tournant (derrière elle pour la moitié) et laisse échapper des volutes.
    /// </summary>
    private static void DrawBlind(CanvasItem canvas, Effect e, float t)
    {
        var head = Head(e);
        if (!_back && t < 0.3f)
        {
            float u = t / 0.3f;
            Glow(canvas, head, (8 + 18 * u) * U, (6 + 12 * u) * U, White, 1 - u);
            for (int n = 0; n < 10; n++)
            {
                var d = Vector2.Right.Rotated(n * MathF.Tau / 10 + 0.3f);
                Streak(canvas, head + d * (6 + 22 * u) * U, d, (4 + 8 * (1 - u)) * U, White, Wind, 1 - u);
            }
        }
        float band = Math.Clamp((t - 0.15f) / 0.5f, 0, 1);
        float fade = Math.Clamp((1 - t) * 4, 0, 1);
        if (band <= 0)
            return;
        for (int n = 0; n < 24; n++)
        {
            float a = n * MathF.Tau / 24 + t * 5;
            float z = MathF.Sin(a);
            if (!Shows(z))
                continue;
            float r = (24 - 12 * band) * U;
            var at = head + new Vector2(MathF.Cos(a) * r, z * r * 0.22f + MathF.Sin(n * 1.7f + t * 8) * U);
            float size = 2 + Noise(e.Seed, n) * 1.5f;
            float alpha = 0.9f * band * fade * (z < 0 ? 0.8f : 1);
            Disc(canvas, at, size, A(n % 3 == 0 ? NightGrey : Night, alpha));
            Dot(canvas, at - new Vector2(0, size * Pixel), A(NightGrey.Lightened(0.2f), alpha * 0.6f));
        }
        if (!_back)
            for (int n = 0; n < 5; n++)
            {
                float u = (t * 1.5f + Noise(e.Seed, n + 30)) % 1;
                var at = head + new Vector2((Noise(e.Seed, n + 40) - 0.5f) * 20 * U, -u * 18 * U);
                Puff(canvas, at, (1.5f + 2 * u) * U, NightGrey, 0.5f * band * fade * (1 - u), e.Seed + n);
            }
    }

    /// <summary>La dissipation au sol : un cercle de runes qui s'ouvre (deux anneaux, des signes entre eux), sur une lueur blanche.</summary>
    private static void DrawDispelCircle(CanvasItem canvas, Effect e, float t)
    {
        float open = Hold(t);
        float s = 0.6f + 0.7f * (1 - (1 - t) * (1 - t));
        Glow(canvas, e.Ground, 22 * U * s, 9 * U * s, Sky, 0.35f * open);
        Ring(canvas, e.Ground, 20 * U * s, 8 * U * s, 0.9f * U, White, open);
        Ring(canvas, e.Ground, 14 * U * s, 5.6f * U * s, 0.6f * U, Wind, 0.9f * open);
        for (int n = 0; n < 16; n++)
        {
            float a = n * MathF.Tau / 16 + t * 1.5f;
            var at = e.Ground + new Vector2(MathF.Cos(a) * 17, MathF.Sin(a) * 6.8f) * U * s;
            // un petit signe : deux traits en croix ou en angle
            var c = A(n % 2 == 0 ? White : Wind, open);
            Line(canvas, at - new Vector2(Pixel, Pixel), at + new Vector2(Pixel, Pixel), c);
            Line(canvas, at + new Vector2(n % 3 == 0 ? -Pixel : Pixel, -Pixel), at + new Vector2(0, Pixel), c);
        }
    }

    /// <summary>
    /// La dissipation : une onde blanche autour du corps, et les sorts de la troupe qui se brisent en éclats de toutes les
    /// couleurs ; ils s'envolent en tournant, avec une traîne (derrière elle pour la moitié). La troupe blanchit un instant.
    /// </summary>
    private static void DrawDispel(CanvasItem canvas, Effect e, float t)
    {
        float wave = Math.Clamp(t / 0.5f, 0, 1);
        if (wave < 1)
            Ring(canvas, e.To, (8 + 24 * wave) * U, (3 + 9 * wave) * U, 1.3f * U, White, 1 - wave, true);
        Color[] colors = { Gold, Purple, Sky, Blood, Green, White };
        for (int n = 0; n < 24; n++)
        {
            float a = Noise(e.Seed, n) * MathF.Tau;
            float z = MathF.Sin(a);
            if (!Shows(z))
                continue;
            float u = Math.Clamp(t * 1.3f - Noise(e.Seed, n + 50) * 0.2f, 0, 1);
            if (u <= 0 || u >= 1)
                continue;
            float speed = 0.6f + 0.8f * Noise(e.Seed, n + 9);
            var dir = new Vector2(MathF.Cos(a), z * 0.35f - 0.45f).Normalized();
            float reach = (6 + 30 * speed * (1 - (1 - u) * (1 - u))) * U;
            var at = e.To + new Vector2(0, (Noise(e.Seed, n + 20) - 0.5f) * e.Height * 0.5f) + dir * reach;
            var color = colors[n % colors.Length];
            float alpha = (1 - u) * (z < 0 ? 0.8f : 1);
            Streak(canvas, at - dir * 3 * Pixel, dir, 6 * U * (1 - u), color, color.Darkened(0.3f), alpha * 0.6f);
            // l'éclat : un petit losange qui tourne, un cœur blanc
            var spin = Vector2.Right.Rotated(u * 12 + n);
            var cross = new Vector2(-spin.Y, spin.X);
            Line(canvas, at - spin * 2.5f * Pixel, at + spin * 2.5f * Pixel, A(color, alpha));
            Line(canvas, at - cross * Pixel, at + cross * Pixel, A(color, alpha));
            Dot(canvas, at, A(White, alpha));
        }
    }

    /// <summary>La téléportation au sol : un cercle de lumière bleue, plus vif au moment où la troupe part ou arrive.</summary>
    private static void DrawTeleportGround(CanvasItem canvas, Effect e, float t)
    {
        bool leaving = e.Kind == "teleport-depart";
        float peak = leaving ? Bell(Math.Clamp(t * 1.1f, 0, 1)) : Bell(Math.Clamp(t * 1.1f - 0.1f, 0, 1));
        Glow(canvas, e.Ground, 20 * U, 8 * U, Sky, 0.5f * peak);
        Ring(canvas, e.Ground, 16 * U, 6.4f * U, 1 * U, Wind, 0.9f * peak);
        Ring(canvas, e.Ground, (8 + 14 * (leaving ? 1 - t : t)) * U, (3.2f + 5.6f * (leaving ? 1 - t : t)) * U, 0.6f * U, White, peak);
    }

    /// <summary>
    /// La téléportation : la troupe s'efface (<see cref="Look"/>) dans une colonne de lumière bleue et trois rubans qui se
    /// resserrent en tournant autour d'elle (derrière elle pour moitié), puis réapparaît dans des rubans qui s'ouvrent ; un
    /// éclat au moment où elle disparaît et où elle apparaît, des traits de lumière qui montent ou descendent.
    /// </summary>
    private static void DrawTeleport(CanvasItem canvas, Effect e, float t, bool leaving)
    {
        float peak = Bell(t);
        BackGlow(canvas, e, Sky, 0.45f * peak);
        if (!_back)
        {
            // la colonne : un dégradé du milieu vers les bords, et des traits qui montent (départ) ou descendent (arrivée)
            Glow(canvas, e.To, 9 * U, e.Height * 0.9f, Wind, 0.4f * peak);
            for (int n = 0; n < 10; n++)
            {
                float u = (t * 2.2f + Noise(e.Seed, n)) % 1;
                float x = (Noise(e.Seed, n + 10) - 0.5f) * 16 * U;
                float y = leaving ? e.Ground.Y - u * (e.Height + 20 * U) : e.Ground.Y - (1 - u) * (e.Height + 20 * U);
                Streak(canvas, new Vector2(e.Ground.X + x, y), new Vector2(0, leaving ? -1 : 1), (4 + 6 * Noise(e.Seed, n + 20)) * U, White, Sky, Bell(u) * peak);
            }
        }
        for (int w = 0; w < 3; w++)
        {
            float phase = w * MathF.Tau / 3;
            float h = (w - 1) * e.Height * 0.3f;
            Ribbon(canvas, Math.Min(t * 1.25f, 0.999f), 16, 0.03f, 2f, v =>
            {
                float r = (leaving ? 1 - v * 0.8f : 0.2f + v * 0.8f) * 20 * U;
                float a = phase + v * 10 * (leaving ? 1 : -1);
                return (e.To + new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r * 0.3f + h), MathF.Sin(a));
            }, 1, White, Wind, Sky, Violet);
        }
        float flash = leaving ? Math.Clamp(1 - MathF.Abs(t - 0.75f) * 5, 0, 1) : Math.Clamp(1 - MathF.Abs(t - 0.25f) * 5, 0, 1);
        if (!_back && flash > 0)
        {
            Glow(canvas, e.To, 14 * U * flash, 18 * U * flash, White, flash);
            Star(canvas, e.To, 8, Wind, flash);
        }
    }

    // ------------------------------------------------------------------ les auras des sorts qui durent

    /// <summary>
    /// L'aura d'un sort qui dure, discrète, en boucle : des grains d'or qui montent (bénédiction), violets qui tombent
    /// (malédiction), des éclats de pierre qui tournent (peau de pierre), des gouttes rouges (soif de sang), des traits de
    /// vent (hâte), du sable qui tombe lentement (lenteur), un bouclier pâle (bouclier), des grains gris qui s'écoulent
    /// (faiblesse), un bandeau d'ombre (aveuglement). <paramref name="facing"/> : le côté de l'ennemi.
    /// <paramref name="behind"/> : la part de derrière la troupe (dessinée avant elle), sinon celle de devant.
    /// </summary>
    public static void DrawLasting(CanvasItem canvas, SpellEffect effect, Vector2 body, Vector2 ground, float facing, double time, int seed, bool behind)
    {
        _back = behind;
        float height = 2 * (ground.Y - body.Y);
        var head = ground - new Vector2(0, height * 0.85f);
        float clock = (float)time;
        switch (effect)
        {
            case SpellEffect.Bless:
                if (!behind)
                    Motes(canvas, ground, height, clock * 0.6f, seed, 3, Gold, true);
                break;
            case SpellEffect.Curse:
                if (!behind)
                    Motes(canvas, ground, height, clock * 0.4f, seed, 3, Purple, false);
                break;
            case SpellEffect.Weakness:
                if (!behind)
                    Motes(canvas, ground, height, clock * 0.5f, seed, 3, Grey, false);
                break;
            case SpellEffect.Bloodlust:
                for (int n = 0; n < 4; n++)
                {
                    float u = (clock * 0.7f + Noise(seed, n)) % 1;
                    float a = Noise(seed, n + 30) * MathF.Tau + u * 2;
                    if (Shows(MathF.Sin(a)))
                        Drop(canvas, ground + new Vector2(MathF.Cos(a) * 12 * U, MathF.Sin(a) * 4 * U - u * height), Bell(u) * 0.8f);
                }
                break;
            case SpellEffect.StoneSkin:
                for (int n = 0; n < 3; n++)
                {
                    float a = clock * 1.2f + n * MathF.Tau / 3;
                    float z = MathF.Sin(a);
                    if (Shows(z))
                        Rock(canvas, body + new Vector2(MathF.Cos(a) * 15 * U, z * 4 * U + height * 0.15f), 1.6f * U * (1 + z * 0.15f), seed + n, clock * 3 + n, 0,
                            Vector2.Zero, GreyRock);
                }
                break;
            case SpellEffect.Haste:
                if (!behind)
                    for (int n = 0; n < 3; n++)
                    {
                        float u = (clock * 1.3f + Noise(seed, n)) % 1;
                        float y = ground.Y - (0.15f + 0.6f * Noise(seed, n + 20)) * height;
                        float x = ground.X + facing * (8 - 22 * u) * U;
                        Streak(canvas, new Vector2(x, y), new Vector2(-facing, 0), 6 * U, White, Sky, Bell(u) * 0.8f);
                    }
                break;
            case SpellEffect.Slow:
                for (int n = 0; n < 6; n++)
                {
                    float a = clock * 0.5f + n * MathF.Tau / 6;
                    if (Shows(MathF.Sin(a)))
                        Blob(canvas, ground + new Vector2(MathF.Cos(a) * 15, MathF.Sin(a) * 6) * U, 0, A(Sand, 0.8f));
                }
                break;
            case SpellEffect.ArrowShield:
                if (!behind)
                    Shield(canvas, body, height, facing, clock, 1, 0.45f, seed);
                break;
            case SpellEffect.Blind:
                for (int n = 0; n < 10; n++)
                {
                    float a = clock * 1.5f + n * MathF.Tau / 10;
                    float z = MathF.Sin(a);
                    if (Shows(z))
                        Disc(canvas, head + new Vector2(MathF.Cos(a) * 11 * U, z * 2.5f * U), 1.2f, A(n % 3 == 0 ? NightGrey : Night, 0.85f));
                }
                break;
        }
        _back = false;
    }

    /// <summary>Quelques grains qui montent (ou tombent) en boucle autour du corps, chacun à son rythme.</summary>
    private static void Motes(CanvasItem canvas, Vector2 ground, float height, float clock, int seed, int count, Color color, bool rising)
    {
        for (int n = 0; n < count; n++)
        {
            float u = (clock + Noise(seed, n)) % 1;
            float x = (Noise(seed, n + 10) - 0.5f) * 16 * U;
            float y = rising ? ground.Y - 4 * U - u * height : ground.Y - height + u * (height - 4 * U);
            Dot(canvas, new Vector2(ground.X + x, y), A(color, Bell(u) * 0.9f));
        }
    }
}
