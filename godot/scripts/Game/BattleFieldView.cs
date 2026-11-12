using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le champ de bataille dessiné (étape 6) : le sol du terrain où l'on se bat, les cases hexagonales, les obstacles,
/// les troupes (×2 comme la carte, l'ennemi tourné vers la gauche) avec leur nombre, les cases où la troupe active peut aller, et
/// de petites animations (marche case par case, éclair du coup, dégâts qui montent). Ce que montre l'écran suit le
/// journal du combat (<see cref="TacticalBattle.TakeLog"/>), à son rythme : les nombres affichés changent au moment du coup.
/// </summary>
public partial class BattleFieldView : Control
{
    /// <summary>
    /// Taille d'une case : largeur (écart entre deux colonnes) et hauteur (pointe à pointe). Les cases sont aplaties (plus
    /// larges que hautes, le champ vu un peu de biais, comme Heroes 3) : la place gagnée en hauteur va au-dessus de la
    /// grille (<see cref="TopMargin"/>). Écran de 1920 × 1080 : les cases, les images et les marges sont en pixels de l'écran.
    /// </summary>
    public const float HexWidth = 110, HexHeight = 100;
    private const float RowStep = HexHeight * 0.75f;

    /// <summary>
    /// Le sol au-dessus de la grille : les créatures sont plus hautes que leur case (l'Ange, le Colosse), et celles de la
    /// première rangée y ont la place de leur tête (choix de l'utilisateur, 2026-11-07 : elles étaient coupées).
    /// </summary>
    public const float TopMargin = 177; // le champ remplit toute la hauteur entre la ligne du haut et la barre du bas (1920 × 1080)
    private const int SpriteScale = 4;

    /// <summary>
    /// Agrandissement des images de combat (art/creatures/battle/) : ×2, comme la carte, pour qu'un pixel d'art ait la même
    /// taille partout (choix de l'utilisateur, 2026-11-09 : ×3 avant ; les créatures ont été refaites 1,5 fois plus hautes).
    /// </summary>
    private const int BattleSpriteScale = 2;

    /// <summary>
    /// Les animations d'une créature : des feuilles de 8 images (4 colonnes, 2 rangées), à côté de son image de combat
    /// (« epeiste-idle.png », « epeiste-move.png », « epeiste-attack.png »). Sans feuille, l'image fixe est montrée.
    /// Une feuille peut avoir plus d'images (« moveFrames »: 16 dans animations.json : 4 colonnes, 4 rangées), pour un
    /// mouvement plus lent mais aussi fluide.
    /// </summary>
    private const int AnimationFrames = 8, AnimationColumns = 4;

    /// <summary>Images d'animation par seconde.</summary>
    private const double AnimationFps = 10;

    /// <summary>
    /// Les vitesses propres à certaines créatures (un géant lent…), dans <c>art/creatures/battle/animations.json</c>
    /// (facultatif) : <c>{ "colosse-os": { "idle": 6, "move": 7 } }</c>, en images par seconde. L'entrée « * » vaut
    /// pour toutes les créatures (une créature peut la remplacer). Aussi les effets de la créature : « projectile »
    /// (« fleche », « boule-feu »), « launchFrame » (l'image de l'attaque où le tir part), « launchPoint » ([x, y] dans
    /// cette image, en pixels d'art), « impact » (« choc » par défaut, « morsure »…).
    /// </summary>
    private static Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>? _speeds;

    /// <summary>Images par seconde de cette animation de la créature (10 par défaut).</summary>
    private static double Fps(BattleUnit unit, string state)
    {
        if (_speeds == null)
        {
            const string path = Art.Root + "creatures/battle/animations.json";
            _speeds = Godot.FileAccess.FileExists(path)
                ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, System.Text.Json.JsonElement>>>(
                    Godot.FileAccess.GetFileAsString(path)) ?? new()
                : new();
        }
        return Setting(unit, state) is double fps and > 0 ? fps : AnimationFps;
    }

    /// <summary>Un réglage de <c>animations.json</c> pour cette créature, ou null.</summary>
    private static double? Setting(BattleUnit unit, string key) =>
        Raw(unit, key) is { ValueKind: System.Text.Json.JsonValueKind.Number } value ? value.GetDouble() : null;

    /// <summary>Un réglage texte de <c>animations.json</c> (« projectile », « impact »), ou null.</summary>
    private static string? TextSetting(BattleUnit unit, string key) =>
        Raw(unit, key) is { ValueKind: System.Text.Json.JsonValueKind.String } value ? value.GetString() : null;

    /// <summary>Un point [x, y] de <c>animations.json</c> (« launchPoint »), ou null.</summary>
    private static Vector2? PointSetting(BattleUnit unit, string key) =>
        Raw(unit, key) is { ValueKind: System.Text.Json.JsonValueKind.Array } value && value.GetArrayLength() == 2
            ? new Vector2((float)value[0].GetDouble(), (float)value[1].GetDouble()) : null;

    private static System.Text.Json.JsonElement? Raw(BattleUnit unit, string key)
    {
        if (_speeds == null)
            Fps(unit, Idle);
        // le réglage de la créature, sinon celui de toutes (« * »)
        foreach (var id in new[] { unit.Type.Id, "*" })
            if (_speeds!.TryGetValue(id, out var settings) && settings.TryGetValue(key, out var value))
                return value;
        return null;
    }

    /// <summary>Le nombre d'images de cette feuille (« idleFrames », « moveFrames », « attackFrames » ; 8 par défaut).</summary>
    private static int Frames(BattleUnit unit, string state) =>
        Setting(unit, state + "Frames") is double frames and >= 1 ? (int)frames : AnimationFrames;

    /// <summary>
    /// Le temps pour traverser une case (« stepTime », en secondes) : par défaut, une créature qui marche fait un cycle
    /// de son animation de déplacement par case (ses pieds ne glissent pas) ; une créature qui vole va vite (0,09 s par
    /// case : un vol traverse beaucoup de cases d'un coup).
    /// </summary>
    private static double StepTime(BattleUnit unit)
    {
        if (Setting(unit, "stepTime") is double time and > 0)
            return time;
        if (unit.Type.Abilities.Contains("flies") || Sheet(unit, Move) == null)
            return 0.09;
        return Frames(unit, Move) / Fps(unit, Move);
    }

    /// <summary>
    /// La pause entre deux attentes (« idlePauseMin » et « idlePauseMax », en secondes) : la créature reste sur sa pose
    /// de départ un moment tiré entre les deux, puis joue son attente une fois. Null : l'attente tourne en boucle.
    /// </summary>
    private static (double Least, double Most)? IdlePause(BattleUnit unit) =>
        Setting(unit, "idlePauseMax") is double most and > 0 ? (Math.Min(Setting(unit, "idlePauseMin") ?? most, most), most) : null;

    /// <summary>
    /// L'image d'attente avec des pauses : pause, attente jouée une fois, pause… Chaque pause a sa durée, tirée d'après
    /// le numéro de la pause et la troupe (toujours la même, sans hasard de la partie), pour que les troupes ne
    /// bougent pas ensemble.
    /// </summary>
    private static int IdleFrameWithPauses(double time, double fps, double least, double most, double phase, int frames)
    {
        double play = frames / fps;
        for (int cycle = 0; ; cycle++)
        {
            double noise = Math.Abs(Math.Sin(cycle * 12.9898 + phase * 78.233) * 43758.5453) % 1;
            double pause = least + noise * (most - least);
            if (time < pause)
                return 0;
            time -= pause;
            if (time < play)
                return (int)(time * fps);
            time -= play;
        }
    }

    /// <summary>Agrandissement du sol et des obstacles (les images de la carte) : ×2, comme sur la carte.</summary>
    private const int GroundScale = 2;

    /// <summary>Pendant une attaque animée, le coup porte à ce moment (en secondes), vers la fin du geste.</summary>
    private const double HitDelay = 0.45;

    /// <summary>« transform » (facultatif) : jouée avant un déplacement (le Vampire devient chauve-souris), et à
    /// l'envers à l'arrivée.</summary>
    private const string Idle = "idle", Move = "move", AttackState = "attack", Transform = "transform";

    /// <summary>
    /// La place de chaque côté de la grille (étape 7) : le sol continue, pour les héros (et plus tard les machines de
    /// guerre : catapulte, baliste…).
    /// </summary>
    public const float SideMargin = 318;

    public static Vector2 FieldSize => new(Battlefield.Columns * HexWidth + HexWidth / 2 + 2 * SideMargin, TopMargin + (Battlefield.Rows - 1) * RowStep + HexHeight);

    /// <summary>Le héros de l'attaquant, dessiné à gauche de la grille (null : pas de héros, une vitrine…).</summary>
    public Hero? AttackerHero { get; set; }

    /// <summary>Le héros qui se défend (combat entre héros), dessiné à droite de la grille, tourné vers la gauche.</summary>
    public Hero? DefenderHero { get; set; }

    /// <summary>Le camp du joueur : ses troupes ont la couleur bleue (le joueur attaqué se défend à droite).</summary>
    public BattleSide PlayerSide { get; set; } = BattleSide.Attacker;

    private const int HeroScale = 4;

    /// <summary>Où est dessiné le héros de l'attaquant, à gauche de la grille (pour savoir si on clique dessus).</summary>
    public Rect2 HeroRect => HeroRectAt(SideMargin / 2);

    /// <summary>Où est dessiné le héros qui se défend, à droite de la grille.</summary>
    public Rect2 DefenderHeroRect => HeroRectAt(FieldSize.X - SideMargin / 2);

    private static Rect2 HeroRectAt(float centerX)
    {
        var size = (Art.Get("heroes/hero")?.GetSize() ?? new Vector2(24, 24)) * HeroScale;
        return new Rect2(new Vector2(centerX - size.X / 2, (FieldSize.Y + TopMargin) / 2 - size.Y / 2), size); // au milieu de la grille
    }

    /// <summary>Un clic sur un des héros.</summary>
    public event Action<Hero>? HeroClicked;

    /// <summary>Clic droit enfoncé sur une troupe (sa fiche, tant que le bouton reste enfoncé), et relâché.</summary>
    public event Action<BattleUnit, Vector2>? UnitInfoPressed;
    public event Action? UnitInfoReleased;

    public TacticalBattle Battle { get; private set; } = null!;
    private TerrainType _terrain;

    /// <summary>Ce que l'écran montre de chaque troupe (en retard sur le combat pendant les animations).</summary>
    private sealed class Shown
    {
        public Vector2 Position;
        public int Count;
        public float Flash;
        public bool Gone;

        /// <summary>L'animation jouée (attente, marche, attaque) et depuis quand.</summary>
        public string State = Idle;
        public double StateTime;

        /// <summary>Décalage de l'attente, pour que les troupes ne respirent pas toutes ensemble.</summary>
        public double Phase;

        /// <summary>Le recul d'une troupe touchée (de 1 au coup à 0), et son sens (+1 : vers la droite).</summary>
        public float Recoil, RecoilDirection;

        /// <summary>Une lueur verte qui s'éteint (de 1 à 0) : la troupe vient de se régénérer.</summary>
        public float Glow;

        /// <summary>Tournée vers la gauche (dessinée en miroir) pendant une marche ou un coup ; null : le côté de son
        /// camp (l'attaquant regarde à droite, le défenseur à gauche).</summary>
        public bool? FacesLeft;

        /// <summary>L'animation est jouée à l'envers (la transformation, au retour).</summary>
        public bool Backwards;

        public void Play(string state, bool backwards = false)
        {
            State = state;
            StateTime = 0;
            Backwards = backwards;
        }
    }

    private readonly Dictionary<BattleUnit, Shown> _shown = new();
    private readonly List<(string Text, Vector2 Position, float Age, Color Color)> _floating = new();

    /// <summary>Projectiles et impacts (dessinés par le jeu).</summary>
    private readonly BattleEffects _effects = new();

    /// <summary>Le temps du combat à l'écran (pour les auras, qui tournent en boucle).</summary>
    private double _time;

    /// <summary>Les cases à mettre en valeur : où la troupe active peut aller, la case survolée, la cible visée.</summary>
    public IReadOnlyCollection<Hex> Reachable { get; set; } = Array.Empty<Hex>();
    public Hex? Hovered { get; set; }
    public Hex? Target { get; set; }
    public BattleUnit? ActiveUnit { get; set; }

    /// <summary>Les cases d'où la troupe active frappera la cible visée (le côté choisi à la souris).</summary>
    public IReadOnlyCollection<Hex> AttackFrom { get; set; } = Array.Empty<Hex>();

    /// <summary>Pendant le choix de la cible d'un sort : les cases visables, et celles que toucherait le sort sous la souris.</summary>
    public IReadOnlyCollection<Hex> SpellTargets { get; set; } = Array.Empty<Hex>();
    public IReadOnlyCollection<Hex> SpellArea { get; set; } = Array.Empty<Hex>();

    /// <summary>L'éclat d'un sort lancé : ses cases, sa couleur, depuis quand (secondes).</summary>
    private readonly List<(List<Hex> Hexes, Color Color, float Age)> _bursts = new();

    private const float BurstTime = 0.5f;

    /// <summary>La couleur d'un sort : feu pour les dégâts, vert pour les soins, violet pour un sort néfaste, or sinon.</summary>
    private static Color SpellColor(SpellType spell) => spell switch
    {
        { Effect: SpellEffect.Damage, Target: SpellTarget.Chain } => MapRenderer.Rgb(150, 200, 255),
        { Effect: SpellEffect.Damage } => MapRenderer.Rgb(255, 140, 60),
        { Effect: SpellEffect.Cure or SpellEffect.Resurrection } => MapRenderer.Rgb(130, 230, 130),
        { Harmful: true } => MapRenderer.Rgb(190, 110, 230),
        _ => MapRenderer.Rgb(250, 220, 120),
    };

    /// <summary>Le joueur a cliqué sur le champ : la case, et le point exact (pour choisir de quel côté attaquer).</summary>
    public event Action<Hex, Vector2>? Clicked;

    public void Start(TacticalBattle battle, TerrainType terrain)
    {
        Battle = battle;
        terrain = DevTerrain ?? terrain;
        _terrain = terrain;
        _shown.Clear();
        _floating.Clear();
        _effects.Clear();
        foreach (var unit in battle.Units)
            _shown[unit] = new Shown { Position = UnitCenter(unit, unit.Position), Count = unit.Count, Phase = unit.Id * 0.37 };
        CustomMinimumSize = FieldSize;
        Size = FieldSize;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true; // la lisière ne déborde pas sur la barre du haut
        BuildBackdrop(terrain);
    }

    /// <summary>
    /// La lisière en haut du champ, les arbres moyens d'un paysage ouvert et les bords : chaque image telle qu'elle est
    /// dessinée (peut-être brumeuse), l'image d'origine (pour son ombre de contact, d'après ses pieds) et ses pieds.
    /// </summary>
    private List<(Texture2D Shown, Texture2D Original, Vector2 Foot)> _edge = new();

    /// <summary>
    /// La lisière du champ (choix de l'utilisateur, 2026-11-10 : vue du même angle que le champ et du style de la carte,
    /// comme Heroes 3) : trois rangées le long du haut, tirées et posées au hasard selon <paramref name="seed"/> (rien ne se
    /// répète d'un combat à l'autre), un peu brumeuses vers le fond. Au fond, de grands troncs (art/battle/trunk-grass.png…)
    /// dont le haut sort du champ : on n'en voit que le tronc ; devant, des buissons (bush-grass) parmi les arbres
    /// (edge-grass). Un paysage ouvert n'a pas de lisière, mais quelques arbres moyens (mid-grass) entre les collines et le
    /// champ. Les bords gauche et droit, comme Heroes 2 : des buissons et des arbres (side-grass), loin des héros.
    /// </summary>
    private static List<(Texture2D, Texture2D, Vector2)> BuildEdge(TerrainType terrain, int seed, bool open = false)
    {
        string name = Art.Name(DecorLayer.Covers(terrain) ? TerrainType.Grass : terrain);
        var trees = Art.VariantsOf($"battle/edge-{name}");
        var trunks = Art.VariantsOf($"battle/trunk-{name}");
        var bushes = Art.VariantsOf($"battle/bush-{name}");
        var edge = new List<(Texture2D, Texture2D, Vector2)>();
        var random = new Random(seed);
        var hazeColor = PaletteOf(terrain).Haze;
        void Add(Texture2D image, float haze, Vector2 foot) => edge.Add((Hazy(image, haze, hazeColor), image, Snap(foot)));
        Texture2D Pick(Texture2D[] main, Texture2D[] other, int percent) =>
            other.Length > 0 && random.Next(100) < percent ? other[random.Next(other.Length)] : main[random.Next(main.Length)];
        if (open)
        {
            var middle = Art.VariantsOf($"battle/mid-{name}");
            for (float x = random.Next(120); middle.Length > 0 && x < FieldSize.X; x += 90 + random.Next(220))
            {
                if (random.Next(3) == 0)
                    x += 150 + random.Next(200); // des trouées
                Add(middle[random.Next(middle.Length)], 0.12f, new Vector2(x, 168 + random.Next(30)));
            }
        }
        else if (trees.Length > 0)
        {
            // du fond vers l'avant : la hauteur des pieds, la brume (0 : l'image telle quelle), l'écart moyen entre deux
            // arbres (pixels de l'écran), la part de grands troncs et celle de buissons
            var rows = new[]
            {
                (Foot: 70f, Haze: 0.26f, Step: 70f, Trunks: 45, Bushes: 0),
                (Foot: 105f, Haze: 0.14f, Step: 110f, Trunks: 25, Bushes: 10),
                (Foot: 145f, Haze: 0.05f, Step: 150f, Trunks: 0, Bushes: 40),
            };
            foreach (var row in rows)
                for (float x = -random.Next((int)row.Step); x < FieldSize.X + row.Step; x += row.Step * (0.7f + 0.6f * (float)random.NextDouble()))
                {
                    var image = Pick(trees, bushes, row.Bushes);
                    if (!bushes.Contains(image))
                        image = Pick(trees, trunks, row.Trunks);
                    // un buisson est bas : il se pose un peu plus bas que la rangée, devant les troncs
                    float y = row.Foot + random.Next(-8, 9) + (bushes.Contains(image) ? 12 : 0);
                    Add(image, row.Haze + 0.04f * (float)random.NextDouble(), new Vector2(x, y));
                }
        }
        var sides = Art.VariantsOf($"battle/side-{name}");
        if (sides.Length == 0)
            sides = trees;
        if (sides.Length > 0)
            foreach (bool left in new[] { true, false })
                for (float y = 230 + random.Next(60); y < FieldSize.Y + 60; y += 150 + random.Next(90))
                    Add(sides[random.Next(sides.Length)], 0.04f * (float)random.NextDouble(),
                        new Vector2(left ? random.Next(-40, 30) : FieldSize.X - random.Next(-40, 30), y));
        // de l'arrière vers l'avant
        edge.Sort((a, b) => a.Item3.Y.CompareTo(b.Item3.Y));
        return edge;
    }

    /// <summary>Les petits détails semés sur le sol (cailloux, fleurs, touffes, racines) : l'image et sa place.</summary>
    private List<(Texture2D Image, Vector2 Position)> _details = new();

    /// <summary>
    /// Les détails du sol (demandés par l'utilisateur, 2026-11-10 : un sol plus travaillé) : art/battle/detail-grass.png,
    /// -2…, semés au hasard selon <paramref name="seed"/> sous la lisière, une quarantaine par champ.
    /// </summary>
    private static List<(Texture2D, Vector2)> BuildDetails(TerrainType terrain, int seed)
    {
        var ground = DecorLayer.Covers(terrain) ? TerrainType.Grass : terrain;
        var images = Art.VariantsOf($"battle/detail-{Art.Name(ground)}");
        var details = new List<(Texture2D, Vector2)>();
        if (images.Length == 0)
            return details;
        var random = new Random(seed * 7 + 3);
        // moins dans le désert (remarque de l'utilisateur : moins de crânes et de touffes) : ses taches de sol ont déjà leurs marques
        int count = ground == TerrainType.Sand ? 30 : 44;
        for (int i = 0; i < count; i++)
        {
            var image = images[random.Next(images.Length)];
            var at = new Vector2(20 + random.Next((int)FieldSize.X - 40), 170 + random.Next((int)FieldSize.Y - 190));
            details.Add((image, Snap(at)));
        }
        return details;
    }

    /// <summary>Développement (« --battle-scene=open » ou « forest ») : le décor du champ, au lieu du tirage.</summary>
    private static readonly string? DevScene = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--battle-scene="))?["--battle-scene=".Length..];

    /// <summary>Le paysage ouvert en haut du champ (ciel, collines, petits arbres lointains), ×2, ou null.</summary>
    private Texture2D? _landscape;

    /// <summary>
    /// Un paysage ouvert (comme l'herbe de Heroes 3 ou les déserts de Heroes 2 : au loin, de petites collines et une bande
    /// de ciel) : dessiné par le jeu, différent à chaque combat (selon <paramref name="seed"/>). Le ciel en bandes tramées,
    /// deux chaînes de collines de plus en plus brumeuses vers le fond, les petits arbres de la carte (decor/oak, pine)
    /// au pied des collines, brumeux eux aussi. Sous la dernière colline, rien : le sol du champ continue jusqu'à elle.
    /// </summary>
    private static Texture2D BuildLandscape(TerrainType terrain, int seed)
    {
        int[,] bayer = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
        var palette = PaletteOf(terrain);
        int w = (int)Math.Ceiling(FieldSize.X / GroundScale), h = 84;
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var random = new Random(seed * 13 + 5);
        // une ligne de crête : des sinus aux phases tirées au sort
        Func<int, float> Ridge(float baseLine, float amplitude, float scale)
        {
            // de grandes ondulations et de petites irrégularités, aux longueurs pas multiples les unes des autres
            var waves = Enumerable.Range(0, 6).Select(i => (Freq: MathF.Pow(i + 1, 1.6f) * (1.1f + 0.5f * (float)random.NextDouble()) / scale,
                Phase: (float)random.NextDouble() * 6.28f, Amp: amplitude * MathF.Pow(0.55f, i) * (0.6f + 0.8f * (float)random.NextDouble()))).ToArray();
            float total = waves.Sum(v => v.Amp);
            return x => baseLine - amplitude * waves.Sum(v => v.Amp * (0.5f + 0.5f * MathF.Sin(x * v.Freq + v.Phase))) / total;
        }
        var ridge = Ridge(44, 26, 90);
        // des mesas (le désert) : le haut de la chaîne lointaine coupé à plat
        Func<int, float> far = palette.FlatTops ? x => Math.Max(ridge(x), 44 - 26 * 0.8f) : ridge;
        // des dunes (le désert), une à une, chacune de taille et de forme tirées au sort : une face courte et raide à gauche
        // (à l'ombre), une longue pente douce à droite (au soleil, qui vient d'en haut à droite)
        var dunes = new List<(float Crest, float Left, float Right, float Height)>();
        for (float x = -60; palette.Dunes && x < w + 60; )
        {
            float left = 12 + random.Next(16), right = 40 + random.Next(80), height = 10 + random.Next(18);
            dunes.Add((x + left, left, right, height));
            x += left + right - random.Next(30); // elles se chevauchent un peu
        }
        // la dune visible en x (la plus haute) et sa hauteur
        (int Index, float Top) DuneAt(float x)
        {
            int best = -1;
            float top = 0;
            for (int i = 0; i < dunes.Count; i++)
            {
                var d = dunes[i];
                float u = x < d.Crest ? (x - (d.Crest - d.Left)) / d.Left : 1 - (x - d.Crest) / d.Right;
                if (u <= 0)
                    continue;
                float hgt = d.Height * (x < d.Crest ? MathF.Pow(u, 0.7f) : u * u * (3 - 2 * u)); // face raide, pente douce
                if (hgt > top) { top = hgt; best = i; }
            }
            return (best, top);
        }
        var near = palette.Dunes ? (Func<int, float>)(x => 66 - DuneAt(x).Top) : Ridge(62, 14, 55);
        var meadow = Ridge(74, 5, 30);
        // le ciel : du bleu en haut au pâle à l'horizon, en 4 bandes tramées entre elles
        Color[] sky = palette.Sky;
        // chaque chaîne : éclairée sous la crête, puis sa couleur, puis son ombre vers le pied (tramées entre elles)
        Color[] farTones = palette.Far, nearTones = palette.Near;
        // le pré au pied des collines : la couleur du sol du champ (mesurée en jeu), un peu brumeuse
        Color[] meadowTones = palette.Meadow;
        // un bruit fixe par pixel (des bois lointains sur les pentes)
        bool Speck(int x, int y, int rate) => ((x * 73856093) ^ (y * 19349663) ^ seed) % rate == 0;
        Color Tone(Color[] tones, float t, int x, int y)
        {
            float u = Math.Clamp(t, 0, 0.999f) * (tones.Length - 1);
            int i = (int)u;
            var c = i + 1 < tones.Length && bayer[y % 4, x % 4] < (u - i) * 16 ? tones[i + 1] : tones[i];
            return Speck(x, y, 9) ? c.Darkened(0.12f) : c;
        }
        for (int x = 0; x < w; x++)
        {
            int f = (int)far(x), n = (int)near(x), m = (int)meadow(x);
            for (int y = 0; y < h; y++)
            {
                Color c;
                if (y >= m + 3)
                    continue; // le sol du champ
                if (y >= n)
                    // les collines proches ; une dune : sa face raide dans l'ombre, dont le bord part de la crête et descend en
                    // biais, en s'incurvant (pas une ligne droite) ; la pente douce au soleil, plus sombre en descendant
                    if (palette.Dunes && DuneAt(x) is { Index: >= 0 } at)
                    {
                        var d = dunes[at.Index];
                        float below = y - (66 - d.Height); // sous la crête
                        float boundary = d.Crest + 6 + below * 0.35f - below * below * 0.04f + 1.5f * MathF.Sin(y * 0.7f + at.Index);
                        bool shade = x < boundary;
                        c = Tone(nearTones, (shade ? 0.92f : 0.02f) + below / 60f, x, y);
                    }
                    else
                        c = Tone(nearTones, (y - n) / 12f, x, y);
                else if (y >= f)
                    c = Tone(farTones, (y - f) / 18f, x, y);
                else
                {
                    float t = (float)y / Math.Max(1, f) * (sky.Length - 1);
                    int band = Math.Min(sky.Length - 2, (int)t);
                    c = bayer[y % 4, x % 4] < (t - band) * 16 ? sky[band + 1] : sky[band];
                }
                // le pré au pied des collines proches, entre elles et le sol du champ
                if (y >= n && y >= m - 4)
                    c = Tone(meadowTones, (y - m + 4) / 6f, x, y);
                // le bord du pré se fond dans le sol par paliers de transparence (75, 50, 25 %)
                if (y >= m)
                    c = new Color(c, 0.75f - 0.25f * (y - m));
                image.SetPixel(x, y, c);
            }
        }
        // les petits arbres lointains, au pied des collines proches, brumeux
        // de petits arbres vus de côté (art/battle/far-grass.png… ; ceux de la carte, vus de dessus, juraient)
        var trees = Art.VariantsOf($"battle/far-{Art.Name(DecorLayer.Covers(terrain) ? TerrainType.Grass : terrain)}");
        int spacing = palette.Dunes ? 40 : 6; // le désert : quelques arbres seulement
        for (float x = random.Next(30); trees.Length > 0 && x < w; x += spacing + random.Next(26 + spacing))
        {
            if (random.Next(3) == 0)
                x += 30 + random.Next(60); // des trouées
            var tree = Hazy(trees[random.Next(trees.Length)], 0.25f + 0.1f * (float)random.NextDouble(), palette.Haze).GetImage();
            int foot = (int)meadow((int)x) - 1 - random.Next(3);
            image.BlendRect(tree, new Rect2I(0, 0, tree.GetWidth(), tree.GetHeight()), new Vector2I((int)x - tree.GetWidth() / 2, foot - tree.GetHeight()));
        }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>La couleur de la brume du décor (herbe : un vert bleuté de sous-bois).</summary>
    private static readonly Color HazeColor = new(0.30f, 0.42f, 0.36f);

    /// <summary>
    /// Les couleurs du décor d'un terrain : le ciel (du haut vers l'horizon), la chaîne lointaine et la proche (éclairée,
    /// couleur, ombre), le pré qui rejoint le sol, la brume des images, la brume claire vers l'horizon, l'ombre de la lisière ;
    /// <c>FlatTops</c> : la chaîne lointaine en mesas.
    /// </summary>
    private sealed record ScenePalette(Color[] Sky, Color[] Far, Color[] Near, Color[] Meadow, Color Haze, Color Mist, Color Shade,
        bool FlatTops = false, bool Dunes = false);

    private static readonly ScenePalette GrassPalette = new(
        new Color[] { new(0.47f, 0.62f, 0.78f), new(0.58f, 0.71f, 0.82f), new(0.70f, 0.80f, 0.86f), new(0.80f, 0.86f, 0.87f) },
        new Color[] { new(0.62f, 0.70f, 0.76f), new(0.52f, 0.61f, 0.69f), new(0.44f, 0.53f, 0.62f) },
        new Color[] { new(0.45f, 0.62f, 0.38f), new(0.36f, 0.53f, 0.34f), new(0.29f, 0.45f, 0.31f) },
        new Color[] { new(0.47f, 0.64f, 0.27f), new(0.44f, 0.62f, 0.25f) },
        HazeColor, new Color(0.72f, 0.82f, 0.80f), new Color(0.02f, 0.06f, 0.02f));

    /// <summary>Le désert (comme Heroes 2) : un ciel pâle et chaud, des mesas rose-ocre au loin, des dunes ocre devant.</summary>
    private static readonly ScenePalette SandPalette = new(
        new Color[] { new(0.55f, 0.70f, 0.85f), new(0.66f, 0.78f, 0.88f), new(0.80f, 0.85f, 0.86f), new(0.90f, 0.88f, 0.80f) },
        new Color[] { new(0.80f, 0.66f, 0.54f), new(0.72f, 0.57f, 0.47f), new(0.63f, 0.49f, 0.42f) },
        new Color[] { new(0.88f, 0.73f, 0.48f), new(0.80f, 0.64f, 0.40f), new(0.70f, 0.55f, 0.34f) },
        new Color[] { new(0.86f, 0.74f, 0.50f), new(0.82f, 0.70f, 0.46f) },
        new Color(0.80f, 0.70f, 0.55f), new Color(0.95f, 0.88f, 0.75f), new Color(0.25f, 0.15f, 0.05f), FlatTops: true, Dunes: true);

    private static ScenePalette PaletteOf(TerrainType terrain) => terrain switch
    {
        TerrainType.Sand => SandPalette,
        _ => GrassPalette,
    };

    /// <summary>Le sol de pierres sous le sable (le désert), ×2, ou null.</summary>
    private Texture2D? _stones;

    /// <summary>
    /// Les marques du sol du désert (demandé par l'utilisateur, 2026-11-11, d'après ses références :
    /// art-candidats/references/sol-desert/, dont le « Desert Ground Pack » d'Alexet) : pas une texture pleine, mais des
    /// marques légères posées sur le sable, en quelques taches qui s'estompent vers leur bord :
    /// - des **rides de vent** : de courts traits courbes qui suivent un même vent (une direction par combat), un pixel
    ///   sombre et un clair au-dessus ;
    /// - des **traînées de points**, dans le sens du vent ;
    /// - de la **terre craquelée** dans certaines taches : des fissures ouvertes (des traits qui bifurquent), sans plaques ;
    /// - **quelques cailloux** pâles, surtout petits, un trait sombre dessous et une petite ombre.
    /// Tout en paliers de transparence : le sable reste la couleur du sol.
    /// </summary>
    private static Texture2D BuildStones(int seed)
    {
        int w = (int)Math.Ceiling(FieldSize.X / GroundScale), h = (int)Math.Ceiling(FieldSize.Y / GroundScale);
        var random = new Random(seed * 9 + 2);
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        // le vent : une direction presque horizontale qui ondule doucement d'un endroit à l'autre
        float wind = ((float)random.NextDouble() - 0.5f) * 0.6f, p1 = (float)random.NextDouble() * 6.28f, p2 = (float)random.NextDouble() * 6.28f;
        float Flow(float x, float y) => wind + 0.25f * MathF.Sin(x / 57f + p1) + 0.18f * MathF.Sin(y / 31f + p2);
        // les taches : de tailles très différentes, chacune avec ses marques (une sur trois craquelée)
        // la teinte de chaque tache : une terre rougeâtre, ocre, gris-brun ou un sable pâle
        Color[] tints = { new(0.64f, 0.34f, 0.20f), new(0.76f, 0.52f, 0.24f), new(0.50f, 0.45f, 0.40f), new(0.96f, 0.91f, 0.78f), new(0.58f, 0.40f, 0.28f) };
        // un peu rapprochées du sable (remarque de l'utilisateur : un peu moins de contraste)
        tints = tints.Select(t => t.Lerp(new Color(0.86f, 0.74f, 0.52f), 0.22f)).ToArray();
        // des taches plus grandes et irrégulières (remarque de l'utilisateur : elles paraissaient toutes rondes) : chacune
        // est faite de 2 à 4 ronds de tailles différentes qui se chevauchent, et son bord est déformé par un bruit doux
        var spots = Enumerable.Range(0, 8 + random.Next(6)).Select(_ =>
        {
            float r = 18f + 80f * MathF.Pow((float)random.NextDouble(), 1.55f); // entre les petites taches rondes et les très grandes
            var blobs = new List<(float Dx, float Dy, float R)> { (0, 0, r) };
            for (int b = 1 + random.Next(3); b > 0; b--)
            {
                float angle = (float)random.NextDouble() * 6.28f, dist = r * (0.4f + 0.55f * (float)random.NextDouble());
                blobs.Add((MathF.Cos(angle) * dist, MathF.Sin(angle) * dist / 1.6f, r * (0.35f + 0.45f * (float)random.NextDouble())));
            }
            var tint = tints[random.Next(tints.Length)];
            var second = tints[random.Next(tints.Length)];
            if (second == tint)
                second = tints[(Array.IndexOf(tints, tint) + 2) % tints.Length];
            return (X: 30f + random.Next(w - 60), Y: 115f + random.Next(h - 140), R: r, Cracked: random.Next(3) == 0, Tint: tint,
                Second: second, Blobs: blobs.ToArray());
        }).ToArray();
        // un bruit doux (des taches rondes, sans grille) : des valeurs tirées aux nœuds d'une grille, mêlées entre eux
        float Smooth(float x, float y, float cell, int k)
        {
            float gx = x / cell, gy = y / cell;
            int ix = (int)MathF.Floor(gx), iy = (int)MathF.Floor(gy);
            float fx = gx - ix, fy = gy - iy;
            fx = fx * fx * (3 - 2 * fx);
            fy = fy * fy * (3 - 2 * fy);
            float At(int cx, int cy) => (((cx * 73856093) ^ (cy * 19349663) ^ (k * 83492791) ^ seed) & 1023) / 1023f;
            float top = At(ix, iy) + (At(ix + 1, iy) - At(ix, iy)) * fx, bottom = At(ix, iy + 1) + (At(ix + 1, iy + 1) - At(ix, iy + 1)) * fx;
            return top + (bottom - top) * fy;
        }
        // de 1 au milieu d'une tache à 0 à son bord : le plus haut de ses ronds, le bord déformé par un bruit doux (des lobes,
        // des baies) qui suit la taille de la tache
        float Shape((float X, float Y, float R, bool Cracked, Color Tint, Color Second, (float Dx, float Dy, float R)[] Blobs) s, float x, float y)
        {
            float best = -1;
            foreach (var (bdx, bdy, br) in s.Blobs)
            {
                float dx = (x - s.X - bdx) / br, dy = (y - s.Y - bdy) * 1.6f / br;
                best = MathF.Max(best, 1 - MathF.Sqrt(dx * dx + dy * dy));
            }
            return best + 0.45f * (Smooth(x, y, MathF.Max(6, s.R * 0.32f), 11) - 0.5f);
        }
        float Density(float x, float y) => MathF.Max(0, spots.Max(s => Shape(s, x, y)));
        (float X, float Y) PointIn((float X, float Y, float R, bool Cracked, Color Tint, Color Second, (float Dx, float Dy, float R)[] Blobs) s)
        {
            // un point d'un des ronds, gardé s'il est bien dans la forme déformée (quelques essais)
            for (int attempt = 0; ; attempt++)
            {
                var (bdx, bdy, br) = s.Blobs[random.Next(s.Blobs.Length)];
                float angle = (float)random.NextDouble() * 6.28f, dist = br * MathF.Sqrt((float)random.NextDouble());
                float px = s.X + bdx + MathF.Cos(angle) * dist, py = s.Y + bdy + MathF.Sin(angle) * dist / 1.6f;
                if (Shape(s, px, py) > 0 || attempt >= 6)
                    return (px, py);
            }
        }
        // un point marqué : une couleur, une opacité en paliers (selon la densité de la tache) ; garde la plus forte
        void Mark(float fx, float fy, Color color, float alpha)
        {
            int x = (int)MathF.Round(fx), y = (int)MathF.Round(fy);
            if (x < 0 || y < 0 || x >= w || y >= h)
                return;
            float a = MathF.Ceiling(MathF.Min(1, alpha * (0.35f + 0.65f * Density(x, y))) * 4) / 4;
            if (a > 0 && a >= image.GetPixel(x, y).A)
                image.SetPixel(x, y, new Color(color, a));
        }
        var light = new Color(1f, 0.95f, 0.80f);
        // la terre de chaque tache, en un voile très léger de sa teinte (plus marqué au milieu, par paliers)
        foreach (var spot in spots)
        {
            int x0 = (int)MathF.Max(0, spot.X - spot.R * 1.9f), x1 = (int)MathF.Min(w - 1, spot.X + spot.R * 1.9f);
            int y0 = (int)MathF.Max(0, spot.Y - spot.R * 1.9f / 1.6f), y1 = (int)MathF.Min(h - 1, spot.Y + spot.R * 1.9f / 1.6f);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float inside = Shape(spot, x, y);
                // le bord du voile s'effrange un peu (un bruit par 3 pixels)
                inside -= 0.12f * ((((x / 3 * 73856093) ^ (y / 3 * 19349663) ^ seed) & 255) / 255f);
                if (inside <= 0)
                    continue;
                // des couleurs mêlées, comme sur un vrai sol : la teinte de la tache passe à une seconde par taches douces
                // (deux tailles de bruit), avec des zones un peu plus claires ou plus sombres, et un voile plus ou moins épais
                float blend = Smooth(x, y, 14, 7) * 0.65f + Smooth(x, y, 5, 8) * 0.35f;
                float shade = Smooth(x, y, 9, 9);
                var color = spot.Tint.Lerp(spot.Second, MathF.Round(blend * 4) / 4);
                color = shade > 0.6f ? color.Lightened(0.12f) : shade < 0.4f ? color.Darkened(0.16f) : color;
                float thickness = 0.55f + 0.45f * Smooth(x, y, 11, 10);
                float a = MathF.Ceiling(MathF.Min(1, inside * 1.6f) * thickness * 4) / 4 * 0.4f;
                if (a > image.GetPixel(x, y).A)
                    image.SetPixel(x, y, new Color(color, a));
            }
        }
        foreach (var spot in spots)
        {
            float area = spot.R * spot.R;
            // les traits : un brun chaud et sombre, mêlé de la teinte de la tache
            var dark = spot.Tint.Lerp(new Color(0.34f, 0.22f, 0.12f), 0.6f);
            // les rides de vent
            for (int i = 0; i < area / 70; i++)
            {
                var (x, y) = PointIn(spot);
                int length = 5 + random.Next(12);
                for (int k = 0; k < length; k++)
                {
                    float a = Flow(x, y) + 0.5f * MathF.Sin(k / (float)length * MathF.PI - MathF.PI / 2) * 0.4f;
                    x += MathF.Cos(a);
                    y += MathF.Sin(a) * 0.6f;
                    Mark(x, y, dark, 0.45f);
                    Mark(x, y - 1, light, 0.3f);
                }
            }
            // les traînées de points
            for (int i = 0; i < area / 120; i++)
            {
                var (x, y) = PointIn(spot);
                int dots = 3 + random.Next(8);
                for (int k = 0; k < dots; k++)
                {
                    float a = Flow(x, y);
                    float step = 2 + (float)random.NextDouble() * 2;
                    x += MathF.Cos(a) * step;
                    y += MathF.Sin(a) * step * 0.6f + ((float)random.NextDouble() - 0.5f);
                    // des grains de couleurs mêlées
                    Mark(x, y, random.Next(4) == 0 ? spot.Tint.Darkened(0.2f) : dark, 0.5f);
                }
            }
            // les fissures : des traits qui changent de direction et bifurquent
            if (spot.Cracked)
                for (int i = 0; i < area / 200 + 2; i++)
                {
                    var stack = new Stack<(float X, float Y, float A, int Left)>();
                    var start = PointIn(spot);
                    stack.Push((start.X, start.Y, (float)random.NextDouble() * 6.28f, 8 + random.Next(14)));
                    while (stack.Count > 0)
                    {
                        var (x, y, a, left) = stack.Pop();
                        for (; left > 0; left--)
                        {
                            if (random.Next(5) == 0)
                                a += (random.Next(2) == 0 ? 1 : -1) * (0.6f + (float)random.NextDouble() * 0.6f);
                            if (random.Next(9) == 0 && stack.Count < 6)
                                stack.Push((x, y, a + (random.Next(2) == 0 ? 1.2f : -1.2f), left / 2));
                            // un pas d'un pixel entier, en suivant la direction : un trait d'un pixel, sans pâté
                            float dx = MathF.Cos(a), dy = MathF.Sin(a) * 0.6f, step = MathF.Max(MathF.Abs(dx), MathF.Abs(dy));
                            x += dx / step;
                            y += dy / step;
                            Mark(x, y, spot.Tint.Lerp(new Color(0.28f, 0.17f, 0.09f), 0.75f), 0.6f);
                        }
                    }
                }
        }
        // de petites touffes d'herbe sèche (comme le « Desert Ground Pack ») : 3 à 6 brins fins depuis un pied, un peu penchés,
        // couleur paille, la pointe plus claire
        var straw = new Color(0.58f, 0.46f, 0.24f);
        foreach (var spot in spots)
            for (int i = 0; i < spot.R * spot.R / 500 + 1; i++)
            {
                var (bx, by) = PointIn(spot);
                int blades = 3 + random.Next(4);
                for (int b = 0; b < blades; b++)
                {
                    float lean = (b - (blades - 1) / 2f) * 0.5f + ((float)random.NextDouble() - 0.5f) * 0.4f;
                    int length = 2 + random.Next(3);
                    for (int k = 0; k <= length; k++)
                    {
                        int px = (int)MathF.Round(bx + lean * k), py = (int)by - k;
                        if (px < 0 || py < 0 || px >= w || py >= h)
                            continue;
                        image.SetPixel(px, py, k == length ? straw.Lightened(0.3f) : k == 0 ? straw.Darkened(0.25f) : straw);
                    }
                }
            }
        // des cailloux de couleurs mêlées (rosés, ocre, gris, blanchâtres, rouille), surtout petits ; les marques sont plus
        // espacées depuis que les taches sont grandes (remarque de l'utilisateur : trop chargé)
        Color[] pebbles =
        {
            new(0.93f, 0.89f, 0.80f), new(0.84f, 0.66f, 0.58f), new(0.80f, 0.62f, 0.40f), new(0.66f, 0.62f, 0.58f),
            new(0.72f, 0.46f, 0.34f), new(0.88f, 0.80f, 0.66f),
        };
        foreach (var spot in spots)
            for (int i = 0; i < spot.R * spot.R / 170 + 3; i++)
            {
                var (fx, fy) = PointIn(spot);
                int cx = (int)fx, cy = (int)fy;
                float u = (float)random.NextDouble();
                int r = u < 0.4f ? 0 : u < 0.8f ? 1 : u < 0.96f ? 2 : 3;
                var tone = pebbles[random.Next(pebbles.Length)].Darkened((float)random.NextDouble() * 0.08f);
                float rx = r + 0.6f, ry = MathF.Max(0.5f, rx * 0.7f);
                for (int dy = -r - 1; dy <= r + 1; dy++)
                for (int dx = -r - 1; dx <= r + 1; dx++)
                {
                    int px = cx + dx, py = cy + dy;
                    if (px < 0 || py < 0 || px >= w || py >= h)
                        continue;
                    float ex = dx / rx, ey = dy / ry;
                    if (ex * ex + ey * ey <= 1)
                        // trois tons : éclairé en haut à droite, dans l'ombre en bas à gauche
                        image.SetPixel(px, py, r == 0 ? tone : ex - ey > 0.4f ? tone.Lightened(0.3f) : ex - ey < -0.5f ? tone.Darkened(0.22f) : tone);
                    else if (ex * ex + (ey - 0.9f) * (ey - 0.9f) <= 1 && dy > 0)
                        image.SetPixel(px, py, new Color(0.35f, 0.25f, 0.15f, dx < 0 ? 0.75f : 0.5f)); // le trait et l'ombre dessous
                }
            }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Le relief du sol (les dunes du désert), ×2, ou null.</summary>
    private Texture2D? _relief;

    /// <summary>
    /// Des dunes sur le champ du désert (idée de l'utilisateur, 2026-11-11 ; comme Heroes 2) : trois ou quatre longues crêtes
    /// de plusieurs cases, tirées selon <paramref name="seed"/>, chacune une bande claire au-dessus de la crête (le versant au
    /// soleil) et une bande d'ombre au-dessous, qui s'amincissent aux deux bouts. Du relief seulement : on y marche.
    /// </summary>
    private static Texture2D BuildDunes(int seed)
    {
        int w = (int)Math.Ceiling(FieldSize.X / GroundScale), h = (int)Math.Ceiling(FieldSize.Y / GroundScale);
        var alpha = new float[w, h];
        var tone = new bool[w, h]; // vrai : la bande claire
        var random = new Random(seed * 5 + 11);
        int count = 3 + random.Next(2);
        for (int i = 0; i < count; i++)
        {
            int x0 = 30 + random.Next(Math.Max(1, w - 300)), length = 150 + random.Next(150);
            float y0 = 105 + random.Next(Math.Max(1, h - 140)), wave = 30 + random.Next(25), phase = (float)random.NextDouble() * 6.28f;
            float tilt = ((float)random.NextDouble() - 0.5f) * 0.3f;
            for (int x = x0; x < Math.Min(w, x0 + length); x++)
            {
                float e = MathF.Sin(MathF.PI * (x - x0) / length);
                int crest = (int)MathF.Round(y0 + 5 * MathF.Sin((x - x0) / wave + phase) + tilt * (x - x0));
                int lit = (int)MathF.Round(30 * e), dark = (int)MathF.Round(16 * e);
                for (int dy = 1; dy <= lit; dy++)
                    Mark(x, crest - dy, dy <= lit / 3 ? 0.22f : dy <= 2 * lit / 3 ? 0.13f : 0.06f, true);
                for (int dy = 0; dy <= dark; dy++)
                    Mark(x, crest + dy, dy <= dark / 3 ? 0.30f : dy <= 2 * dark / 3 ? 0.18f : 0.08f, false);
            }
        }
        void Mark(int x, int y, float a, bool light)
        {
            if (y < 0 || y >= h || a <= alpha[x, y])
                return;
            alpha[x, y] = a;
            tone[x, y] = light;
        }
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if (alpha[x, y] > 0)
                image.SetPixel(x, y, tone[x, y] ? new Color(1f, 0.95f, 0.8f, alpha[x, y]) : new Color(0.35f, 0.22f, 0.08f, alpha[x, y]));
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Développement (« --battle-terrain=sand ») : le terrain du champ, au lieu de celui du combat.</summary>
    private static readonly TerrainType? DevTerrain =
        OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--battle-terrain="))?["--battle-terrain=".Length..] is { } t
        && Enum.TryParse<TerrainType>(t, true, out var parsed) ? parsed : null;

    private static readonly Dictionary<(Texture2D, int), Texture2D> HazyCache = new();

    /// <summary>
    /// Une image du décor fondue dans la brume (comme Heroes 1 à 3 : le fond est doux, sans contour noir, pour que les
    /// créatures ressortent) : chaque pixel va vers la couleur de la brume selon <paramref name="amount"/> (0 à 1), et
    /// les contours très sombres s'éclaircissent davantage. Calculée une fois par image et par degré de brume.
    /// </summary>
    private static Texture2D Hazy(Texture2D texture, float amount, Color? haze = null)
    {
        int step = (int)MathF.Round(amount * 20);
        if (step <= 0)
            return texture;
        if (HazyCache.TryGetValue((texture, step), out var done))
            return done;
        float k = step / 20f;
        var color = haze ?? HazeColor;
        var image = texture.GetImage();
        image.Convert(Image.Format.Rgba8);
        for (int y = 0; y < image.GetHeight(); y++)
        for (int x = 0; x < image.GetWidth(); x++)
        {
            var c = image.GetPixel(x, y);
            if (c.A <= 0)
                continue;
            // un contour (pixel très sombre) se fond davantage
            float kk = c.Luminance < 0.12f ? Math.Min(1, k * 1.25f + 0.05f) : k;
            image.SetPixel(x, y, new Color(c.R + (color.R - c.R) * kk, c.G + (color.G - c.G) * kk, c.B + (color.B - c.B) * kk, c.A));
        }
        var result = ImageTexture.CreateFromImage(image);
        HazyCache[(texture, step)] = result;
        return result;
    }

    /// <summary>L'opacité des ombres de contact du décor du combat (plus forte que sur la carte : de grands arbres sur une herbe claire).</summary>
    private const float BattleContact = 0.8f;

    /// <summary>Un point posé sur la grille des pixels d'art (×2), pour que les images restent nettes.</summary>
    private static Vector2 Snap(Vector2 point) => (point / GroundScale).Round() * GroundScale;

    /// <summary>L'ombre de la forêt sur le haut du champ (tramée, ×2), ou null.</summary>
    private Texture2D? _shade;

    /// <summary>La hauteur de l'ombre de la forêt, en pixels de l'écran.</summary>
    private const float ShadeHeight = 300;

    /// <summary>
    /// L'ombre de la forêt (comme Heroes 3 : le sol s'assombrit sous les arbres) : trois bandes de moins en moins sombres
    /// vers le bas, aux bords ondulés (le tramage en points rendait l'image « granuleuse », remarque de l'utilisateur).
    /// </summary>
    private static Texture2D BuildShade(TerrainType terrain, int seed) =>
        Bands(PaletteOf(terrain).Shade, new[] { 0.22f, 0.14f, 0.07f }, 0, 46, seed);

    /// <summary>
    /// La brume sur le sol à l'approche de l'horizon d'un paysage ouvert (la perspective aérienne de Heroes 3 : le loin est
    /// plus clair et plus bleuté), du pied des collines vers le bas.
    /// </summary>
    private static Texture2D BuildMist(TerrainType terrain, int seed) =>
        Bands(PaletteOf(terrain).Mist, new[] { 0.14f, 0.08f, 0.04f }, 146, 20, seed);

    /// <summary>
    /// Des bandes horizontales d'une couleur, de plus en plus transparentes vers le bas (une opacité par bande), qui commencent
    /// à <paramref name="top"/> (pixels de l'écran) ; chaque bord ondule (pixels d'art entiers), pour ne pas faire de lignes.
    /// </summary>
    private static Texture2D Bands(Color color, float[] alphas, float top, float height, int seed)
    {
        int w = (int)Math.Ceiling(FieldSize.X / GroundScale), t = (int)(top / GroundScale), bh = (int)(height / GroundScale);
        int h = t + bh * alphas.Length + 4;
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var random = new Random(seed * 3 + 1);
        var phases = alphas.Select(_ => (A: (float)random.NextDouble() * 6.28f, B: (float)random.NextDouble() * 6.28f)).ToArray();
        for (int x = 0; x < w; x++)
        {
            // le bas de chaque bande, ondulé
            var bottoms = alphas.Select((_, i) => t + bh * (i + 1)
                + (int)MathF.Round(2.5f * MathF.Sin(x / 23f + phases[i].A) + 1.5f * MathF.Sin(x / 9f + phases[i].B))).ToArray();
            for (int y = 0; y < h; y++)
            {
                if (y < t)
                {
                    if (top <= 0)
                        image.SetPixel(x, y, new Color(color, alphas[0]));
                    continue;
                }
                int band = Array.FindIndex(bottoms, b => y < b);
                if (band >= 0)
                    image.SetPixel(x, y, new Color(color, alphas[band]));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Le sol du champ, dessiné par le shader de la carte (null : l'image du terrain répétée, dans _Draw).</summary>
    private Control? _backdrop;
    private TerrainType? _backdropTerrain;

    /// <summary>
    /// Le décor du champ de bataille (choix de l'utilisateur, 2026-11-10 : une scène avec un horizon, comme Heroes 3) :
    /// le sol de la carte (fleurs, cailloux, sans répétition), sous la grille et les créatures.
    /// </summary>
    private void BuildBackdrop(TerrainType terrain)
    {
        if (_backdropTerrain == terrain && _backdrop != null)
            return;
        _backdrop?.QueueFree();
        _backdrop = null;
        _backdropTerrain = terrain;

        int seed = Battle.Field.Obstacles.Aggregate(17, (h, o) => h * 31 + o.Col * 11 + o.Row);
        // deux décors, tirés selon le combat : la lisière d'une forêt, ou un paysage ouvert (collines lointaines et ciel)
        // sans arbres de lisière pour ce terrain (le désert), toujours le paysage ouvert
        bool open = DevScene switch { "open" => true, "forest" => false, _ => (seed & 1) == 1 }
            || Art.VariantsOf($"battle/edge-{Art.Name(DecorLayer.Covers(terrain) ? TerrainType.Grass : terrain)}").Length == 0;
        _edge = BuildEdge(terrain, seed, open);
        _landscape = open ? BuildLandscape(terrain, seed) : null;
        _shade = open ? BuildMist(terrain, seed) : BuildShade(terrain, seed);
        _details = BuildDetails(terrain, seed);
        _relief = PaletteOf(terrain).Dunes ? BuildDunes(seed) : null;
        _stones = PaletteOf(terrain).Dunes ? BuildStones(seed) : null;
        if (SmoothGround.CreateBattle(terrain, FieldSize, seed) is not { } ground)
            return;
        // le sol couvre des cases entières de la carte : le surplus est coupé au bord du champ
        var clip = new Control { Size = FieldSize, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
        ground.TextureFilter = TextureFilterEnum.Nearest;
        clip.AddChild(ground);
        AddChild(clip);
        _backdrop = clip;
    }

    // ------------------------------------------------------------------ géométrie

    /// <summary>Le centre d'une case, dans le dessin (les rangées impaires sont décalées d'une demi-case).</summary>
    public static Vector2 Center(Hex hex) =>
        new(SideMargin + hex.Col * HexWidth + HexWidth / 2 + ((hex.Row & 1) == 1 ? HexWidth / 2 : 0), TopMargin + hex.Row * RowStep + HexHeight / 2);

    /// <summary>Où dessiner une troupe dont la tête est sur cette case : au milieu de ses cases (2 pour une grande créature).</summary>
    public static Vector2 UnitCenter(BattleUnit unit, Hex head)
    {
        var hexes = unit.HexesAt(head).ToList();
        return hexes.Aggregate(Vector2.Zero, (sum, h) => sum + Center(h)) / hexes.Count;
    }

    private static Vector2[] Corners(Vector2 c)
    {
        float w = HexWidth / 2, h = HexHeight / 2;
        return new[] { c + new Vector2(0, -h), c + new Vector2(w, -h / 2), c + new Vector2(w, h / 2),
                       c + new Vector2(0, h), c + new Vector2(-w, h / 2), c + new Vector2(-w, -h / 2) };
    }

    /// <summary>La case sous un point du dessin (la plus proche de son centre), ou null hors du champ.</summary>
    public static Hex? HexAt(Vector2 point)
    {
        Hex? best = null;
        float bestDistance = float.MaxValue;
        for (int r = 0; r < Battlefield.Rows; r++)
        for (int c = 0; c < Battlefield.Columns; c++)
        {
            float d = Center(new Hex(c, r)).DistanceSquaredTo(point);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = new Hex(c, r);
            }
        }
        return bestDistance <= HexWidth * HexWidth * 0.36f ? best : null;
    }

    /// <summary>Le point du champ sous la souris, null quand elle n'est pas sur le champ.</summary>
    public Vector2? MousePoint { get; private set; }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit)
        {
            MousePoint = null;
            Hovered = null;
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion)
        {
            MousePoint = motion.Position;
            Hovered = HexAt(motion.Position);
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            if (AttackerHero != null && HeroRect.HasPoint(click.Position))
                HeroClicked?.Invoke(AttackerHero);
            else if (DefenderHero != null && DefenderHeroRect.HasPoint(click.Position))
                HeroClicked?.Invoke(DefenderHero);
            // une case, ou le corps d'une troupe qui dépasse de la grille (un Ange de la première rangée)
            else if ((HexAt(click.Position) ?? UnitUnder(click.Position)?.Position) is { } hex)
                Clicked?.Invoke(hex, click.Position);
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            if (right.Pressed && (UnitUnder(right.Position) ?? (HexAt(right.Position) is { } hex ? Battle.UnitAt(hex) : null)) is { } unit
                && ShownCount(unit) > 0)
                UnitInfoPressed?.Invoke(unit, right.Position);
            else if (!right.Pressed)
                UnitInfoReleased?.Invoke();
        }
    }

    // ------------------------------------------------------------------ animations

    /// <summary>Les animations en cours : la suivante attend que la précédente soit finie.</summary>
    private readonly Queue<Func<double, bool>> _animations = new();

    public bool IsAnimating => _animations.Count > 0;

    /// <summary>Le nombre de créatures affiché pour une troupe (il change au moment du coup, pas avant).</summary>
    public int ShownCount(BattleUnit unit) => _shown.TryGetValue(unit, out var shown) ? shown.Count : unit.Count;

    /// <summary>
    /// Ajoute au dessin ce que le combat vient de faire (déplacements, coups). <paramref name="begin"/> est appelé quand
    /// chaque action commence à être montrée (pour raconter le coup au bon moment).
    /// </summary>
    public void Show(IEnumerable<BattleAction> actions, Action<BattleAction>? begin = null)
    {
        BattleAction? previous = null;
        foreach (var action in actions)
        {
            // le nuage de mort de la Liche : ses coups sur les troupes autour de la cible suivent aussitôt le tir
            bool cloud = action is UnitStruck { Ranged: true, Retaliation: false } follow
                && previous is UnitStruck { Ranged: true } before && before.Attacker == follow.Attacker
                && follow.Attacker.Has(Ability.DeathCloud);
            var last = previous; // l'action d'avant (le coup du Vampire, avant qu'il boive)
            previous = action;
            var told = action;
            _animations.Enqueue(_ =>
            {
                begin?.Invoke(told);
                return true;
            });
            switch (action)
            {
                case UnitMoved moved:
                    var shown = _shown[moved.Unit];
                    var points = moved.Path.Select(h => UnitCenter(moved.Unit, h)).ToList();
                    int step = 0;
                    double t = 0;
                    Vector2? start = null;
                    // une créature qui se transforme (le Vampire en chauve-souris) : la transformation, puis le
                    // déplacement, puis la transformation à l'envers
                    bool transforms = Sheet(moved.Unit, Transform) != null;
                    double change = transforms ? Frames(moved.Unit, Transform) / Fps(moved.Unit, Transform) : 0;
                    double changing = 0;
                    bool arrived = false;
                    // un fouisseur (le Ver des profondeurs) soulève la terre en plongeant et en avançant sous le sol
                    bool burrows = moved.Unit.Has(Ability.Burrows);
                    _animations.Enqueue(delta =>
                    {
                        if (burrows && !arrived)
                            _effects.Burrow(shown, Ground(shown), delta);
                        if (start == null)
                        {
                            start = shown.Position; // d'où elle part : là où elle est dessinée
                            if (Math.Abs(points[0].X - start.Value.X) > 1)
                                shown.FacesLeft = points[0].X < start.Value.X;
                            shown.Play(transforms ? Transform : Move);
                        }
                        if (shown.State == Transform)
                        {
                            changing += delta;
                            if (changing < change)
                                return false;
                            changing = 0;
                            if (arrived)
                            {
                                shown.Play(Idle);
                                shown.FacesLeft = null;
                                return true;
                            }
                            shown.Play(Move);
                        }
                        t += delta;
                        var from = step == 0 ? start.Value : points[step - 1];
                        // elle regarde où elle va (une case droit vers le haut ou le bas ne la retourne pas)
                        if (Math.Abs(points[step].X - from.X) > 1)
                            shown.FacesLeft = points[step].X < from.X;
                        // une case de marche en 0,09 s (ou le « stepTime » de la créature) ; un vol prend le temps de
                        // toutes les cases survolées
                        float k = (float)Math.Min(1, t / (StepTime(moved.Unit) * Math.Max(1, from.DistanceTo(points[step]) / HexWidth)));
                        shown.Position = from.Lerp(points[step], k);
                        if (k < 1)
                            return false;
                        t = 0;
                        if (++step < points.Count)
                            return false;
                        if (burrows)
                            _effects.EndBurrow(shown);
                        if (transforms)
                        {
                            arrived = true;
                            shown.Play(Transform, backwards: true);
                            return false;
                        }
                        shown.Play(Idle);
                        shown.FacesLeft = null; // arrivée : de nouveau tournée vers le camp adverse
                        return true;
                    });
                    break;
                case UnitStruck cloudStruck when cloud:
                    double after = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (after == 0)
                        {
                            var target = _shown[cloudStruck.Target];
                            target.Count = Math.Max(0, target.Count - cloudStruck.Killed);
                            target.Flash = 1;
                            target.Recoil = 1;
                            target.RecoilDirection = target.Position.X >= _shown[cloudStruck.Attacker].Position.X ? 1 : -1;
                            _floating.Add(($"-{cloudStruck.Damage}", target.Position + Ui.V(0, -50), 0, MapRenderer.Rgb(170, 220, 150)));
                            if (cloudStruck.Killed > 0)
                                _floating.Add(($"{cloudStruck.Killed} †", target.Position + Ui.V(0, -30), 0, Colors.White));
                        }
                        after += Math.Max(delta, 1e-6);
                        if (after < 0.18)
                            return false;
                        if (_shown[cloudStruck.Target].Count == 0)
                            _shown[cloudStruck.Target].Gone = true;
                        return true;
                    });
                    break;
                case UnitStruck struck:
                    double age = 0, wind = 0, boltAge = -1;
                    bool hit = false, launched = false, landed = false;
                    // un tir avec un projectile (flèche, boule de feu) : il part à l'image du lâcher et le coup porte à son
                    // arrivée ; sinon le coup porte au moment habituel du geste
                    string? shot = struck.Ranged ? TextSetting(struck.Attacker, "projectile") : null;
                    // un coup joué sur la case de la cible (la foudre de l'Oiseau-tonnerre) : l'effet part un peu avant le
                    // coup, pour toucher le sol au moment où il porte
                    bool onHex = shot == null && BattleEffects.PlaysOnHex(TextSetting(struck.Attacker, "impact"));
                    _animations.Enqueue(delta =>
                    {
                        // l'attaquant fait son geste (s'il a une animation d'attaque) ; le coup porte vers la fin
                        var striker = _shown[struck.Attacker];
                        if (boltAge >= 0)
                            boltAge += delta;
                        if (wind == 0)
                        {
                            // l'attaquant se tourne vers sa cible, et la cible vers lui
                            var struckShown = _shown[struck.Target];
                            if (Math.Abs(struckShown.Position.X - striker.Position.X) > 1)
                            {
                                striker.FacesLeft = struckShown.Position.X < striker.Position.X;
                                struckShown.FacesLeft = !striker.FacesLeft;
                            }
                            if (Sheet(struck.Attacker, AttackState) != null)
                                striker.Play(AttackState);
                        }
                        // une attaque plus lente porte son coup plus tard, au même moment du geste
                        double release = shot != null && Setting(struck.Attacker, "launchFrame") is double frame
                            ? frame / Fps(struck.Attacker, AttackState)
                            : HitDelay * AnimationFps / Fps(struck.Attacker, AttackState);
                        if (onHex && boltAge < 0 && (striker.State != AttackState || wind + BattleEffects.LightningHitTime >= release))
                        {
                            boltAge = 0;
                            _effects.OnHex(TextSetting(struck.Attacker, "impact")!, Ground(_shown[struck.Target]));
                        }
                        if (striker.State == AttackState && wind < release)
                        {
                            wind += delta;
                            return false;
                        }
                        wind = Math.Max(wind, 1e-6);
                        if (onHex && boltAge < BattleEffects.LightningHitTime)
                            return false;
                        if (shot != null && !launched)
                        {
                            launched = true;
                            _effects.Launch(shot, LaunchPoint(struck.Attacker, striker), BodyCenter(struck.Target, _shown[struck.Target]),
                                () => landed = true, LaunchPoint(struck.Attacker, striker, "launchPoint2"));
                        }
                        if (shot != null && !landed)
                            return false;
                        if (!hit)
                        {
                            hit = true;
                            var target = _shown[struck.Target];
                            target.Count = Math.Max(0, target.Count - struck.Killed);
                            target.Flash = 1;
                            // un éclat sur la troupe touchée, et elle recule un peu, dans le sens du coup
                            float direction = target.Position.X >= striker.Position.X ? 1 : -1;
                            target.Recoil = 1;
                            target.RecoilDirection = direction;
                            string impact = shot != null ? BattleEffects.ImpactOf(shot) : TextSetting(struck.Attacker, "impact") ?? "choc";
                            if (!onHex)
                                _effects.Hit(impact, BodyCenter(struck.Target, target), direction);
                            // le nuage de mort s'étale autour de la cible (ses coups sur les voisines suivent)
                            if (shot != null && struck.Attacker.Has(Ability.DeathCloud))
                                _effects.Hit("nuage", target.Position + new Vector2(0, HexHeight / 2 - Ui.Px(24)), direction);
                            _floating.Add(($"-{struck.Damage}", target.Position + Ui.V(0, -50), 0, MapRenderer.Rgb(255, 210, 120)));
                            if (struck.Killed > 0)
                                _floating.Add(($"{struck.Killed} †", target.Position + Ui.V(0, -30), 0, Colors.White));
                            // la chance : dégâts doublés, ou divisés par deux
                            if (struck.Luck > 0)
                                _floating.Add(("Chance !", target.Position + Ui.V(0, -75), 0, MapRenderer.Rgb(120, 230, 120)));
                            else if (struck.Luck < 0)
                                _floating.Add(("Malchance", target.Position + Ui.V(0, -75), 0, MapRenderer.Rgb(190, 140, 220)));
                        }
                        age += delta;
                        if (age < 0.35 * AnimationFps / Fps(struck.Attacker, AttackState))
                            return false;
                        if (_shown[struck.Target].Count == 0)
                            _shown[struck.Target].Gone = true;
                        if (striker.State == AttackState)
                            striker.Play(Idle);
                        striker.FacesLeft = null;
                        _shown[struck.Target].FacesLeft = null;
                        return true;
                    });
                    break;
                case SpellCast cast:
                    var hexes = cast.Spell.Target == SpellTarget.Area
                        ? new[] { cast.Target }.Concat(cast.Target.Neighbours()).Where(Battlefield.InBounds).ToList()
                        : Battle.UnitAt(cast.Target)?.Hexes.ToList() ?? new List<Hex> { cast.Target };
                    double burst = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (burst == 0)
                            _bursts.Add((hexes, SpellColor(cast.Spell), 0));
                        burst += delta;
                        return burst >= BurstTime * 0.7;
                    });
                    break;
                case SpellDamaged damaged:
                    double hurt = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (hurt == 0)
                        {
                            // une chaîne d'éclairs : chaque troupe touchée s'allume à son tour
                            if (damaged.Spell.Target == SpellTarget.Chain)
                                _bursts.Add((damaged.Target.Hexes.ToList(), SpellColor(damaged.Spell), 0));
                            var target = _shown[damaged.Target];
                            target.Count = Math.Max(0, target.Count - damaged.Killed);
                            target.Flash = 1;
                            _floating.Add(($"-{damaged.Damage}", target.Position + Ui.V(0, -50), 0, MapRenderer.Rgb(255, 170, 90)));
                            if (damaged.Killed > 0)
                                _floating.Add(($"{damaged.Killed} †", target.Position + Ui.V(0, -30), 0, Colors.White));
                        }
                        hurt += delta;
                        if (hurt < 0.35)
                            return false;
                        if (_shown[damaged.Target].Count == 0)
                            _shown[damaged.Target].Gone = true;
                        return true;
                    });
                    break;
                case UnitTeleported teleported:
                    double jump = 0;
                    _animations.Enqueue(delta =>
                    {
                        // la troupe disparaît d'une case et apparaît sur l'autre, dans un éclat
                        if (jump == 0)
                        {
                            _shown[teleported.Unit].Position = UnitCenter(teleported.Unit, teleported.To);
                            _bursts.Add((teleported.Unit.HexesAt(teleported.To).ToList(), MapRenderer.Rgb(250, 220, 120), 0));
                        }
                        jump += delta;
                        return jump >= 0.3;
                    });
                    break;
                case SpellsDispelled dispelled:
                    double gone = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (gone == 0)
                            _floating.Add((dispelled.Spell.Name, _shown[dispelled.Unit].Position + Ui.V(0, -50), 0, MapRenderer.Rgb(200, 220, 255)));
                        gone += delta;
                        return gone >= 0.3;
                    });
                    break;
                case UnitPoisoned poisoned:
                    double venom = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (venom == 0)
                        {
                            var target = _shown[poisoned.Unit];
                            target.Count = Math.Max(0, target.Count - poisoned.Killed);
                            target.Flash = 1;
                            _effects.Hit("bulles", BodyCenter(poisoned.Unit, target), 1);
                            _floating.Add(($"-{poisoned.Damage}", target.Position + Ui.V(0, -50), 0, MapRenderer.Rgb(150, 220, 90)));
                        }
                        venom += delta;
                        if (venom < 0.35)
                            return false;
                        if (_shown[poisoned.Unit].Count == 0)
                            _shown[poisoned.Unit].Gone = true;
                        return true;
                    });
                    break;
                case UnitGrew grew:
                    double grow = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (grow == 0)
                        {
                            _shown[grew.Unit].Count += grew.Added;
                            _effects.Hit("ames", BodyCenter(grew.Unit, _shown[grew.Unit]), 1);
                            _floating.Add(($"+{grew.Added}", _shown[grew.Unit].Position + Ui.V(0, -50), 0, MapRenderer.Rgb(200, 220, 255)));
                        }
                        grow += delta;
                        return grow >= 0.3;
                    });
                    break;
                case UnitSoothed soothed:
                    double soothe = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (soothe == 0)
                        {
                            _effects.Hit("soin", BodyCenter(soothed.Unit, _shown[soothed.Unit]), 1);
                            _floating.Add(($"+{soothed.HitPoints}", _shown[soothed.Unit].Position + Ui.V(0, -50), 0, MapRenderer.Rgb(140, 235, 140)));
                        }
                        soothe += Math.Max(delta, 1e-6);
                        return soothe >= 0.25;
                    });
                    break;
                case UnitTerrified terrified:
                    double fear = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (fear == 0)
                            _floating.Add(("Terreur", _shown[terrified.Unit].Position + Ui.V(0, -70), 0, MapRenderer.Rgb(190, 120, 230)));
                        fear += Math.Max(delta, 1e-6);
                        return fear >= 0.12;
                    });
                    break;
                case UnitSwallowed swallowed:
                    double gulp = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (gulp == 0)
                        {
                            // une créature entière de moins (le Ver des profondeurs)
                            var target = _shown[swallowed.Unit];
                            target.Count = Math.Max(0, target.Count - 1);
                            target.Flash = 1;
                            _floating.Add(("Avalé !", target.Position + Ui.V(0, -70), 0, MapRenderer.Rgb(230, 110, 90)));
                        }
                        gulp += delta;
                        if (gulp < 0.3)
                            return false;
                        if (_shown[swallowed.Unit].Count == 0)
                            _shown[swallowed.Unit].Gone = true;
                        return true;
                    });
                    break;
                // l'Enfer : des troupes qui apparaissent (Limon de magma divisé, diablotins du Démon), et des textes
                case UnitSplit split:
                    AddShown(split.Part);
                    Announce(split.Part, "Division", MapRenderer.Rgb(255, 150, 60), () =>
                    {
                        _shown[split.From].Count = Math.Max(0, _shown[split.From].Count - split.Count);
                        Appear(split.Part, split.Count);
                    });
                    break;
                case UnitSummoned summoned:
                    AddShown(summoned.Summoned);
                    Announce(summoned.Summoned, "Portail", MapRenderer.Rgb(255, 90, 60), () => Appear(summoned.Summoned, summoned.Count), 0.4);
                    break;
                case UnitSacrificed sacrificed:
                    Announce(sacrificed.Unit, $"Sacrifice : -{sacrificed.Count}", MapRenderer.Rgb(220, 60, 60), () =>
                    {
                        var shown = _shown[sacrificed.Unit];
                        shown.Count = Math.Max(0, shown.Count - sacrificed.Count);
                        shown.Flash = 1;
                    });
                    break;
                case UnitBurned burned:
                    Announce(burned.Unit, $"-{burned.Damage}", MapRenderer.Rgb(255, 130, 50), () =>
                    {
                        var shown = _shown[burned.Unit];
                        shown.Count = Math.Max(0, shown.Count - burned.Killed);
                        shown.Flash = 1;
                    }, 0.35);
                    break;
                case UnitHooked hooked:
                    double pull = 0;
                    Vector2 from = default;
                    var to = UnitCenter(hooked.Target, hooked.To);
                    _animations.Enqueue(delta =>
                    {
                        // la cible glisse jusqu'au Tourmenteur, au bout de la chaîne
                        if (pull == 0)
                        {
                            from = _shown[hooked.Target].Position;
                            _floating.Add(("Crochet !", _shown[hooked.Hooker].Position + Ui.V(0, -70), 0, MapRenderer.Rgb(200, 200, 210)));
                        }
                        pull += Math.Max(delta, 1e-6);
                        float k = (float)Math.Min(1, pull / 0.3);
                        _shown[hooked.Target].Position = from.Lerp(to, k);
                        return k >= 1;
                    });
                    break;
                case UnitSeduced seduced:
                    Announce(seduced.Unit, "Séduit", MapRenderer.Rgb(255, 120, 200));
                    break;
                case UnitEntranced entranced:
                    Announce(entranced.Unit, "Sous le charme", MapRenderer.Rgb(255, 120, 200));
                    break;
                case BloodPaid blood:
                    Announce(blood.Unit, $"Sang : +{blood.Points} sort", MapRenderer.Rgb(200, 90, 230));
                    break;
                case SpellPointsStolen stolen:
                    Announce(stolen.Thief, $"Vol : {stolen.Points} sort", MapRenderer.Rgb(130, 170, 255));
                    break;
                case UnitRooted rooted:
                    double root = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (root == 0)
                            _floating.Add(("Enraciné", _shown[rooted.Unit].Position + Ui.V(0, -70), 0, MapRenderer.Rgb(150, 200, 90)));
                        root += Math.Max(delta, 1e-6);
                        return root >= 0.15;
                    });
                    break;
                case UnitCheered cheered:
                    double cheer = 0;
                    _animations.Enqueue(delta =>
                    {
                        // bon moral : la troupe s'illumine et rejoue
                        if (cheer == 0)
                        {
                            _shown[cheered.Unit].Glow = 1;
                            _floating.Add(("Moral !", _shown[cheered.Unit].Position + Ui.V(0, -70), 0, MapRenderer.Rgb(250, 215, 90)));
                        }
                        cheer += Math.Max(delta, 1e-6);
                        return cheer >= 0.45;
                    });
                    break;
                case UnitFroze froze:
                    double frozen = 0;
                    _animations.Enqueue(delta =>
                    {
                        // mauvais moral : elle hésite et passe son tour
                        if (frozen == 0)
                            _floating.Add(("Hésite…", _shown[froze.Unit].Position + Ui.V(0, -70), 0, MapRenderer.Rgb(150, 170, 220)));
                        frozen += Math.Max(delta, 1e-6);
                        return frozen >= 0.45;
                    });
                    break;
                case UnitRevolted revolted:
                    double revolt = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (revolt == 0)
                        {
                            // elle se tourne vers ses nouveaux ennemis
                            var turned = _shown[revolted.Unit];
                            turned.FacesLeft = null;
                            turned.Flash = 1;
                            _floating.Add(("Révolte !", turned.Position + Ui.V(0, -70), 0, MapRenderer.Rgb(240, 150, 130)));
                        }
                        revolt += Math.Max(delta, 1e-6);
                        return revolt >= 0.4;
                    });
                    break;
                case UnitEnraged enraged:
                    double fury = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (fury == 0)
                            _floating.Add(($"+{enraged.Rage} att.", _shown[enraged.Unit].Position + Ui.V(0, -70), 0, MapRenderer.Rgb(240, 90, 60)));
                        fury += Math.Max(delta, 1e-6);
                        return fury >= 0.15;
                    });
                    break;
                case LightningStruck bolt:
                    double flash = 0;
                    _animations.Enqueue(delta =>
                    {
                        var target = _shown[bolt.Target];
                        // la foudre appelée (une attaque sur cinq) : un second éclair, plus fort, sur la case de la cible ;
                        // ses dégâts s'affichent quand il touche le sol, au-dessus de ceux du coup
                        if (flash == 0)
                            _effects.OnHex("foudre-forte", Ground(target));
                        bool before = flash < BattleEffects.LightningHitTime;
                        flash += Math.Max(delta, 1e-6);
                        if (before && flash >= BattleEffects.LightningHitTime)
                        {
                            target.Count = Math.Max(0, target.Count - bolt.Killed);
                            target.Flash = 1;
                            _floating.Add(($"-{bolt.Damage}", target.Position + Ui.V(0, -85), 0, MapRenderer.Rgb(150, 200, 255)));
                            if (bolt.Killed > 0)
                                _floating.Add(($"{bolt.Killed} †", target.Position + Ui.V(0, -65), 0, Colors.White));
                        }
                        if (flash < 0.6)
                            return false;
                        if (target.Count == 0)
                            target.Gone = true;
                        return true;
                    });
                    break;
                case UnitDrained drained:
                    double drink = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (drink == 0)
                        {
                            _shown[drained.Unit].Count += drained.Revived;
                            // la vie bue coule de sa victime jusqu'à lui
                            if (last is UnitStruck bitten && bitten.Attacker == drained.Unit && _shown.TryGetValue(bitten.Target, out var victim))
                                _effects.Drain(BodyCenter(bitten.Target, victim), BodyCenter(drained.Unit, _shown[drained.Unit]));
                            _floating.Add(($"+{drained.HitPoints}", _shown[drained.Unit].Position + Ui.V(0, -50), 0, MapRenderer.Rgb(230, 80, 80)));
                        }
                        drink += delta;
                        return drink >= 0.3;
                    });
                    break;
                case UnitRegenerated regenerated:
                    double regrow = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (regrow == 0)
                            _shown[regenerated.Unit].Glow = 1;
                        if (regrow == 0)
                            _effects.Hit("regeneration", _shown[regenerated.Unit].Position + new Vector2(0, HexHeight / 2 - Ui.Px(10)), 1);
                        regrow += Math.Max(delta, 1e-6);
                        return regrow >= 0.3;
                    });
                    break;
                case UnitHealed healed:
                    double heal = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (heal == 0)
                        {
                            var target = _shown[healed.Unit];
                            target.Count += healed.Revived;
                            _effects.Hit("soin", BodyCenter(healed.Unit, target), 1);
                            target.Gone = target.Count == 0;
                            _floating.Add(($"+{healed.HitPoints}", target.Position + Ui.V(0, -50), 0, MapRenderer.Rgb(140, 235, 140)));
                        }
                        heal += delta;
                        return heal >= 0.35;
                    });
                    break;
                case SpellApplied applied:
                    double marked = 0;
                    _animations.Enqueue(delta =>
                    {
                        if (marked == 0)
                            _floating.Add((applied.Spell.Name, _shown[applied.Unit].Position + Ui.V(0, -50), 0, SpellColor(applied.Spell)));
                        marked += delta;
                        return marked >= 0.3;
                    });
                    break;
                case AttackerFled:
                    foreach (var unit in Battle.Units.Where(u => u.Side == BattleSide.Attacker))
                        _shown[unit].Count = unit.Count;
                    break;
            }
        }
    }

    /// <summary>
    /// Développement : « --effect-preview=foudre » joue cet effet sur la case d'une troupe après l'autre, toutes les deux
    /// secondes ; « --effect-preview=foudre:0.2 » le fige à cet instant (0,2 seconde après son départ) sur la dernière troupe
    /// du camp de droite (la plus basse : l'éclair le plus long), pour les captures. Un tir (« --effect-preview=eclair ») part
    /// sans cesse de la première troupe de gauche vers la première de droite.
    /// </summary>
    private static readonly string? EffectPreviewArg = OS.GetCmdlineUserArgs()
        .FirstOrDefault(a => a.StartsWith("--effect-preview="))?["--effect-preview=".Length..];

    private double _previewClock = -1;
    private int _previewTurn;

    /// <summary>Le sol sous une troupe (là où tombe un effet joué sur sa case).</summary>
    private static Vector2 Ground(Shown shown) => shown.Position + new Vector2(0, HexHeight / 2 - Ui.Px(10));

    private void PreviewEffect(double delta)
    {
        if (EffectPreviewArg is not { } arg || Battle == null)
            return;
        var parts = arg.Split(':');
        if (!BattleEffects.PlaysOnHex(parts[0]))
        {
            // un tir (« eclair ») : de la première troupe de gauche vers la première de droite, relancé sans cesse pour
            // rester visible sur une capture
            _previewClock = _previewClock < 0 ? 0 : _previewClock + delta;
            if (_previewClock > 0 && _previewClock < 0.2)
                return;
            _previewClock = 1e-6;
            var from = _shown.FirstOrDefault(p => !p.Value.Gone && p.Key.Side == BattleSide.Attacker);
            var to = _shown.FirstOrDefault(p => !p.Value.Gone && p.Key.Side == BattleSide.Defender);
            if (from.Key != null && to.Key != null)
                _effects.Launch(parts[0], LaunchPoint(from.Key, from.Value), BodyCenter(to.Key, to.Value), () => { },
                    LaunchPoint(from.Key, from.Value, "launchPoint2"));
            return;
        }
        double? frozen = parts.Length > 1 && double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var at) ? at : null;
        if (_previewClock >= 0 && (frozen != null || _previewClock < 2))
        {
            _previewClock += delta;
            return;
        }
        _previewClock = 0;
        var targets = _shown.Where(p => !p.Value.Gone && p.Key.IsAlive && (frozen == null || p.Key.Side == BattleSide.Defender))
            .Select(p => p.Value).ToList();
        if (targets.Count > 0)
            targets = new() { frozen != null ? targets[^1] : targets[_previewTurn++ % targets.Count] };
        foreach (var shown in targets)
            _effects.OnHex(parts[0], Ground(shown), frozen);
    }

    /// <summary>Une troupe qui apparaît en cours de combat : cachée jusqu'à son animation (<see cref="Appear"/>).</summary>
    private void AddShown(BattleUnit unit) =>
        _shown[unit] = new Shown { Position = UnitCenter(unit, unit.Position), Count = 0, Gone = true, Phase = unit.Id * 0.37 };

    private void Appear(BattleUnit unit, int count)
    {
        var shown = _shown[unit];
        shown.Gone = false;
        shown.Count = count;
        shown.Flash = 1;
    }

    /// <summary>
    /// Un texte qui monte au-dessus de la troupe, après <paramref name="first"/> (ce qui change à l'écran) ; la troupe
    /// disparaît si elle n'a plus de créatures.
    /// </summary>
    private void Announce(BattleUnit unit, string text, Color color, Action? first = null, double time = 0.2)
    {
        double elapsed = 0;
        _animations.Enqueue(delta =>
        {
            if (elapsed == 0)
            {
                first?.Invoke();
                _floating.Add((text, _shown[unit].Position + Ui.V(0, -70), 0, color));
            }
            elapsed += Math.Max(delta, 1e-6);
            if (elapsed < time)
                return false;
            if (_shown[unit].Count == 0)
                _shown[unit].Gone = true;
            return true;
        });
    }

    public override void _Process(double delta)
    {
        if (_animations.Count > 0 && _animations.Peek()(delta))
            _animations.Dequeue();
        foreach (var shown in _shown.Values)
        {
            shown.Flash = Math.Max(0, shown.Flash - (float)delta * 3);
            shown.Recoil = Math.Max(0, shown.Recoil - (float)delta * 4);
            shown.Glow = Math.Max(0, shown.Glow - (float)delta * 1.2f);
            shown.StateTime += delta;
        }
        _effects.Update(delta);
        PreviewEffect(delta);
        _time += delta;
        for (int i = _bursts.Count - 1; i >= 0; i--)
        {
            var (hexes, color, age) = _bursts[i];
            age += (float)delta;
            if (age > BurstTime)
                _bursts.RemoveAt(i);
            else
                _bursts[i] = (hexes, color, age);
        }
        for (int i = _floating.Count - 1; i >= 0; i--)
        {
            var (text, position, age, color) = _floating[i];
            age += (float)delta;
            if (age > 1.1f)
                _floating.RemoveAt(i);
            else
                _floating[i] = (text, position + new Vector2(0, -Ui.Px(30) * (float)delta), age, color);
        }
        QueueRedraw();
    }

    // ------------------------------------------------------------------ dessin

    public override void _Draw()
    {
        if (Battle == null)
            return;
        // le sol : celui du shader de la carte (_backdrop, dessiné dessous), sinon l'image du terrain, ×2 (comme la carte)
        var ground = Art.Terrain(DecorLayer.Covers(_terrain) ? TerrainType.Grass : _terrain, 0);
        if (_backdrop != null)
        {
            if (_landscape != null)
                DrawTextureRect(_landscape, new Rect2(Vector2.Zero, _landscape.GetSize() * GroundScale), false);
            // l'ombre de la forêt sur le haut du sol, puis la lisière : des rangées d'arbres, plus sombres vers le fond
            if (_shade != null)
                DrawTextureRect(_shade, new Rect2(Vector2.Zero, _shade.GetSize() * GroundScale), false);
            if (_stones != null)
                DrawTextureRect(_stones, new Rect2(Vector2.Zero, _stones.GetSize() * GroundScale), false);
            if (_relief != null)
                DrawTextureRect(_relief, new Rect2(Vector2.Zero, _relief.GetSize() * GroundScale), false);
            foreach (var (detail, at) in _details)
            {
                var rect = new Rect2(at, detail.GetSize() * GroundScale);
                ShadowLayer.DrawContactShadow(this, detail, rect, BattleContact);
                DrawTextureRect(detail, rect, false);
            }
            foreach (var (shown, original, foot) in _edge)
            {
                var size = shown.GetSize() * GroundScale;
                var rect = new Rect2(foot - new Vector2(size.X / 2, size.Y), size);
                // l'ombre de contact de la carte (ShadowLayer), d'après les pieds de l'image d'origine
                ShadowLayer.DrawContactShadow(this, original, rect, BattleContact);
                DrawTextureRect(shown, rect, false);
            }
        }
        else if (ground != null)
        {
            var tile = ground.GetSize() * GroundScale;
            // des cases entières, la dernière de chaque rangée coupée au bord du champ (les créatures, elles, peuvent dépasser)
            for (float y = 0; y < Size.Y; y += tile.Y)
            for (float x = 0; x < Size.X; x += tile.X)
            {
                var part = new Vector2(Math.Min(tile.X, Size.X - x), Math.Min(tile.Y, Size.Y - y));
                DrawTextureRectRegion(ground, new Rect2(new Vector2(x, y), part), new Rect2(Vector2.Zero, part / GroundScale));
            }
        }
        else
            DrawRect(new Rect2(Vector2.Zero, Size), MapRenderer.TerrainColor(_terrain));

        // les cases
        for (int r = 0; r < Battlefield.Rows; r++)
        for (int c = 0; c < Battlefield.Columns; c++)
        {
            var hex = new Hex(c, r);
            var corners = Corners(Center(hex));
            if (Reachable.Contains(hex))
                DrawColoredPolygon(corners, new Color(1, 1, 0.8f, 0.16f));
            if (SpellTargets.Contains(hex))
            {
                DrawColoredPolygon(corners, new Color(0.6f, 0.8f, 1, 0.3f));
                DrawPolyline(corners.Append(corners[0]).ToArray(), new Color(0.75f, 0.88f, 1, 0.8f), Ui.Px(2));
            }
            if (SpellArea.Contains(hex))
                DrawColoredPolygon(corners, new Color(0.6f, 0.8f, 1, 0.3f));
            DrawPolyline(corners.Append(corners[0]).ToArray(), new Color(0, 0, 0, 0.28f), Ui.Px(1));
        }
        if (ActiveUnit is { IsAlive: true } active && _shown.TryGetValue(active, out var activeShown) && !IsAnimating)
            foreach (var hex in active.Hexes)
                DrawHexOutline(Center(hex), MapRenderer.Rgb(240, 200, 80), Ui.Px(3));
        if (Hovered is { } hovered)
            DrawHexOutline(Center(hovered), new Color(1, 1, 1, 0.7f), Ui.Px(2));
        if (Target is { } target)
            foreach (var hex in Battle.UnitAt(target)?.Hexes ?? new[] { target })
                DrawHexOutline(Center(hex), MapRenderer.Rgb(220, 60, 40), Ui.Px(3));
        foreach (var hex in AttackFrom)
        {
            DrawColoredPolygon(Corners(Center(hex)), new Color(1, 0.85f, 0.4f, 0.3f));
            DrawHexOutline(Center(hex), MapRenderer.Rgb(250, 215, 110), Ui.Px(3));
        }
        foreach (var hex in SpellArea)
            DrawHexOutline(Center(hex), MapRenderer.Rgb(150, 200, 255), Ui.Px(3));
        // l'éclat d'un sort : les cases s'allument puis s'éteignent
        foreach (var (hexes, color, age) in _bursts)
        {
            float k = 1 - age / BurstTime;
            foreach (var hex in hexes)
            {
                DrawColoredPolygon(Corners(Center(hex)), new Color(color, 0.45f * k));
                DrawHexOutline(Center(hex), new Color(color, k), Ui.Px(3));
            }
        }

        // les héros (leur image de la carte, à leurs couleurs, en attendant une image de combat) : l'attaquant à gauche,
        // celui qui se défend à droite, tourné vers la gauche
        if (AttackerHero != null && MapRenderer.HeroImage(AttackerHero.Owner) is { } heroImage)
            DrawTextureRect(heroImage, HeroRect, false);
        if (DefenderHero != null && MapRenderer.HeroImage(DefenderHero.Owner) is { } defenderImage)
        {
            var rect = DefenderHeroRect;
            DrawSetTransform(new Vector2(rect.End.X, rect.Position.Y), 0, new Vector2(-1, 1));
            DrawTextureRect(defenderImage, new Rect2(Vector2.Zero, rect.Size), false);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        // obstacles et troupes, de haut en bas (ceux du bas passent devant)
        var things = new List<(float Y, Action Draw)>();
        foreach (var obstacle in Battle.Field.Obstacles)
        {
            var center = Center(obstacle);
            things.Add((center.Y, () => DrawObstacle(center, obstacle)));
        }
        foreach (var (unit, shown) in _shown)
            if (!shown.Gone)
                things.Add((shown.Position.Y, () => DrawUnit(unit, shown)));
        _effects.DrawGround(this);
        foreach (var (_, draw) in things.OrderBy(t => t.Y))
            draw();
        // les auras des états qui durent : poison, sorts utiles ou néfastes
        foreach (var (unit, shown) in _shown)
        {
            if (shown.Gone || !unit.IsAlive)
                continue;
            var body = BodyCenter(unit, shown);
            if (unit.PoisonTurnsLeft > 0)
                BattleEffects.DrawAura(this, "poison", body, _time, unit.Id * 7 + 1);
            if (unit.Spells.Any(s => !s.Spell.Harmful))
                BattleEffects.DrawAura(this, "benefique", body, _time, unit.Id * 7 + 2);
            if (unit.Spells.Any(s => s.Spell.Harmful))
                BattleEffects.DrawAura(this, "nefaste", body, _time, unit.Id * 7 + 3);
        }
        _effects.Draw(this);

        var font = ThemeDB.FallbackFont;
        foreach (var (text, position, age, color) in _floating)
        {
            var faded = new Color(color, Math.Clamp(1.4f - age, 0, 1));
            DrawString(font, position + Ui.V(-20, 0), text, HorizontalAlignment.Center, Ui.Px(40), Ui.TextSize(18), faded);
        }
    }

    private void DrawHexOutline(Vector2 center, Color color, float width)
    {
        var corners = Corners(center);
        DrawPolyline(corners.Append(corners[0]).ToArray(), color, width);
    }

    private void DrawObstacle(Vector2 center, Hex hex)
    {
        // les obstacles du terrain (art/battle/obstacle-grass.png, -2, -3… : arbres, rochers), sinon le décor de la carte
        var ground = DecorLayer.Covers(_terrain) ? TerrainType.Grass : _terrain;
        int pick = hex.Col * 5 + hex.Row * 3 + hex.Col * hex.Row;
        var image = Art.Variant($"battle/obstacle-{Art.Name(ground)}", pick)
            ?? (_terrain == TerrainType.Forest ? Art.Variant("decor/pine", (int)center.X) : Art.Variant("decor/peak-small", (int)center.X));
        if (image != null)
        {
            var size = image.GetSize() * GroundScale;
            var rect = new Rect2(center + new Vector2(-size.X / 2, HexHeight / 2 - Ui.Px(8) - size.Y), size);
            ShadowLayer.DrawContactShadow(this, image, rect, BattleContact);
            DrawTextureRect(image, rect, false);
            return;
        }
        DrawColoredPolygon(new[] { center + Ui.V(-22, 18), center + Ui.V(-8, -16), center + Ui.V(10, -10), center + Ui.V(24, 18) },
            MapRenderer.Rgb(110, 105, 100));
    }

    /// <summary>La feuille d'animation d'une créature pour cet état, ou null.</summary>
    private static Texture2D? Sheet(BattleUnit unit, string state) => Art.Get($"creatures/battle/{unit.Type.Id}-{state}");

    /// <summary>
    /// L'image d'animation à montrer : en boucle pour l'attente et la marche, une seule fois pour l'attaque. Sans
    /// feuille pour cet état, celle de l'attente ; sans rien, (null, …) : l'image fixe.
    /// </summary>
    private static (Texture2D? Sheet, Rect2 Frame) Frame(BattleUnit unit, Shown shown)
    {
        var state = shown.State;
        var sheet = Sheet(unit, state);
        if (sheet == null && state != Idle)
        {
            state = Idle;
            sheet = Sheet(unit, Idle);
        }
        if (sheet == null)
            return (null, default);
        double time = state == Idle ? shown.StateTime + shown.Phase : shown.StateTime;
        int frames = Frames(unit, state);
        int index = shown.State == Idle && IdlePause(unit) is var (least, most)
            ? IdleFrameWithPauses(time, Fps(unit, Idle), least, most, shown.Phase, frames)
            : (int)(time * Fps(unit, state));
        // l'attaque et la transformation sont jouées une fois (la transformation parfois à l'envers)
        index = state is AttackState or Transform ? Math.Min(index, frames - 1) : index % frames;
        if (state == Transform && shown.Backwards)
            index = frames - 1 - index;
        int rows = (frames + AnimationColumns - 1) / AnimationColumns;
        var cell = new Vector2(sheet.GetWidth() / AnimationColumns, sheet.GetHeight() / rows);
        return (sheet, new Rect2(new Vector2(index % AnimationColumns * cell.X, index / AnimationColumns * cell.Y), cell));
    }

    /// <summary>
    /// Le rectangle où est dessinée l'image de combat d'une troupe (ses pieds sur sa case, le corps au-dessus), ou null sans
    /// image de combat.
    /// </summary>
    public Rect2? UnitRect(BattleUnit unit)
    {
        if (!_shown.TryGetValue(unit, out var shown) || Art.Get($"creatures/battle/{unit.Type.Id}") is not { } image)
            return null;
        var foot = shown.Position + new Vector2(0, HexHeight / 2 - Ui.Px(10));
        var size = image.GetSize() * BattleSpriteScale;
        return new Rect2(foot + new Vector2(-size.X / 2, -size.Y), size);
    }

    private readonly Dictionary<string, Image?> _hitImages = new();

    /// <summary>
    /// La troupe dont un pixel de l'image est sous ce point (celle de devant d'abord : la rangée la plus basse), pour viser
    /// une créature par son corps, qui dépasse de sa case vers le haut.
    /// </summary>
    public BattleUnit? UnitUnder(Vector2 point)
    {
        foreach (var unit in Battle.Units.Where(u => u.IsAlive && ShownCount(u) > 0).OrderByDescending(u => u.Position.Row))
        {
            if (UnitRect(unit) is not { } rect || !rect.HasPoint(point))
                continue;
            if (!_hitImages.TryGetValue(unit.Type.Id, out var image))
                _hitImages[unit.Type.Id] = image = Art.Get($"creatures/battle/{unit.Type.Id}")?.GetImage();
            if (image == null)
                continue;
            var at = (point - rect.Position) / BattleSpriteScale;
            int x = (int)at.X, y = (int)at.Y;
            if (_shown[unit].FacesLeft ?? unit.Side == BattleSide.Defender)
                x = image.GetWidth() - 1 - x; // dessinée en miroir
            if (x >= 0 && y >= 0 && x < image.GetWidth() && y < image.GetHeight() && image.GetPixel(x, y).A > 0.5f)
                return unit;
        }
        return null;
    }

    /// <summary>Le milieu du corps d'une troupe (là où un projectile la touche) : la moitié de la hauteur de son image.</summary>
    private static Vector2 BodyCenter(BattleUnit unit, Shown shown)
    {
        var foot = shown.Position + new Vector2(0, HexHeight / 2 - Ui.Px(10));
        float height = Art.Get($"creatures/battle/{unit.Type.Id}") is { } image ? image.GetHeight() * BattleSpriteScale : Ui.Px(48);
        return foot - new Vector2(0, height * 0.5f);
    }

    /// <summary>
    /// D'où part le tir : le « launchPoint » de la créature dans son image d'attaque (pixels d'art depuis le coin en haut à
    /// gauche), à l'écran et retourné si elle regarde à gauche ; sinon le milieu de son corps.
    /// </summary>
    private static Vector2 LaunchPoint(BattleUnit unit, Shown shown) =>
        LaunchPoint(unit, shown, "launchPoint") ?? BodyCenter(unit, shown);

    /// <summary>
    /// Le point <paramref name="setting"/> (« launchPoint », ou « launchPoint2 » : la seconde main du Mage, d'où part un
    /// second éclair) de l'image d'attaque, à l'écran ; null si la créature ne l'a pas.
    /// </summary>
    private static Vector2? LaunchPoint(BattleUnit unit, Shown shown, string setting)
    {
        if (PointSetting(unit, setting) is not { } point || Sheet(unit, AttackState) is not { } sheet)
            return null;
        var foot = shown.Position + new Vector2(0, HexHeight / 2 - Ui.Px(10));
        int rows = (Frames(unit, AttackState) + AnimationColumns - 1) / AnimationColumns;
        var cell = new Vector2(sheet.GetWidth() / AnimationColumns, sheet.GetHeight() / rows);
        float x = (point.X - cell.X / 2) * BattleSpriteScale;
        if (shown.FacesLeft ?? unit.Side == BattleSide.Defender)
            x = -x;
        return foot + new Vector2(x, (point.Y - cell.Y) * BattleSpriteScale);
    }

    private void DrawUnit(BattleUnit unit, Shown shown)
    {
        // une troupe touchée recule d'un coup (3 pixels d'art) puis revient à sa place
        var foot = shown.Position + new Vector2(shown.RecoilDirection * shown.Recoil * BattleEffects.U * 3, HexHeight / 2 - Ui.Px(10));
        var tint = shown.Flash > 0 ? new Color(1, 1 - shown.Flash * 0.6f, 1 - shown.Flash * 0.6f) : Colors.White;
        // une troupe qui se régénère s'éclaire de vert un instant
        if (shown.Glow > 0)
            tint *= new Color(1 - 0.35f * shown.Glow, 1, 1 - 0.45f * shown.Glow);
        // une troupe empoisonnée verdit, en palpitant doucement, tant que le poison dure
        if (unit.PoisonTurnsLeft > 0)
        {
            float pulse = 0.5f + 0.5f * MathF.Sin((float)_time * 3 + unit.Id);
            tint *= new Color(0.72f - 0.08f * pulse, 1, 0.62f - 0.08f * pulse);
        }
        // L'image de combat (plus détaillée), sinon la petite de la carte ×4 ; une grande créature est centrée sur ses 2 cases
        var battleImage = Art.Get($"creatures/battle/{unit.Type.Id}");
        var (sheet, frame) = Frame(unit, shown);
        if ((sheet ?? battleImage ?? Art.Get($"creatures/{unit.Type.Id}")) is { } image)
        {
            // l'image de combat (40 × 52 pour une créature debout, plus large pour un loup, un dragon…) est toujours
            // agrandie ×2 : les pixels ont la même taille que sur la carte ; une image d'animation, posée de même
            int scale = sheet != null || battleImage != null ? BattleSpriteScale : SpriteScale;
            var source = sheet != null ? frame : new Rect2(Vector2.Zero, image.GetSize());
            var size = source.Size * scale;
            var rect = new Rect2(foot + new Vector2(-size.X / 2, -size.Y), size);
            if (shown.FacesLeft ?? unit.Side == BattleSide.Defender)
            {
                // tourné vers la gauche : dessiné en miroir
                DrawSetTransform(new Vector2(foot.X * 2, 0), 0, new Vector2(-1, 1));
                DrawTextureRectRegion(image, new Rect2(new Vector2(foot.X - size.X / 2, rect.Position.Y), size), source, tint);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            }
            else
                DrawTextureRectRegion(image, rect, source, tint);
        }
        else
            DrawCircle(foot + Ui.V(0, -24), Ui.Px(20), unit.Side == PlayerSide ? MapRenderer.PlayerColor : MapRenderer.NeutralColor);

        // le nombre de créatures, dans une petite boîte à la couleur du camp
        string count = shown.Count.ToString();
        var box = new Rect2(shown.Position + new Vector2(Ui.Px(unit.Side == BattleSide.Attacker ? 6 : -36), HexHeight / 2 - Ui.Px(24)), Ui.Px(30), Ui.Px(18));
        DrawRect(box, unit.Side == PlayerSide ? MapRenderer.Rgb(40, 60, 120) : MapRenderer.Rgb(110, 40, 30));
        DrawRect(box, MapRenderer.Rgb(230, 200, 120), false, 1);
        DrawString(ThemeDB.FallbackFont, box.Position + Ui.V(0, 14), count, HorizontalAlignment.Center, box.Size.X, Ui.TextSize(12), Colors.White);

        // les sorts qui durent sur la troupe : un petit losange par sort, à côté du nombre (vert : utile, violet : néfaste)
        float x = unit.Side == BattleSide.Attacker ? box.End.X + Ui.Px(4) : box.Position.X - Ui.Px(12);
        foreach (var active in unit.Spells.OrderBy(s => s.Spell.Effect))
        {
            var c = new Vector2(x + Ui.Px(4), box.Position.Y + box.Size.Y / 2);
            var d = Ui.Px(5);
            DrawColoredPolygon(new[] { c + new Vector2(0, -d), c + new Vector2(d, 0), c + new Vector2(0, d), c + new Vector2(-d, 0) },
                active.Spell.Harmful ? MapRenderer.Rgb(190, 110, 230) : MapRenderer.Rgb(130, 220, 130));
            x += unit.Side == BattleSide.Attacker ? Ui.Px(11) : -Ui.Px(11);
        }
    }
}
