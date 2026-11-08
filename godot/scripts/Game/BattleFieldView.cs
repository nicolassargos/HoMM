using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le champ de bataille dessiné (étape 6) : le sol du terrain où l'on se bat, les cases hexagonales, les obstacles,
/// les troupes (×3, l'ennemi tourné vers la gauche) avec leur nombre, les cases où la troupe active peut aller, et
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

    /// <summary>Agrandissement des images de combat (art/creatures/battle/).</summary>
    private const int BattleSpriteScale = 3;

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

    /// <summary>Agrandissement du sol (ses images de 24 pixels) et des obstacles.</summary>
    private const int GroundScale = 3;

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

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion)
            Hovered = HexAt(motion.Position);
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            if (AttackerHero != null && HeroRect.HasPoint(click.Position))
                HeroClicked?.Invoke(AttackerHero);
            else if (DefenderHero != null && DefenderHeroRect.HasPoint(click.Position))
                HeroClicked?.Invoke(DefenderHero);
            else if (HexAt(click.Position) is { } hex)
                Clicked?.Invoke(hex, click.Position);
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
        {
            if (right.Pressed && HexAt(right.Position) is { } hex && Battle.UnitAt(hex) is { } unit && ShownCount(unit) > 0)
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
                    _animations.Enqueue(delta =>
                    {
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
                    double age = 0, wind = 0;
                    bool hit = false, launched = false, landed = false;
                    // un tir avec un projectile (flèche, boule de feu) : il part à l'image du lâcher et le coup porte à son
                    // arrivée ; sinon le coup porte au moment habituel du geste
                    string? shot = struck.Ranged ? TextSetting(struck.Attacker, "projectile") : null;
                    _animations.Enqueue(delta =>
                    {
                        // l'attaquant fait son geste (s'il a une animation d'attaque) ; le coup porte vers la fin
                        var striker = _shown[struck.Attacker];
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
                        if (striker.State == AttackState && wind < release)
                        {
                            wind += delta;
                            return false;
                        }
                        wind = Math.Max(wind, 1e-6);
                        if (shot != null && !launched)
                        {
                            launched = true;
                            _effects.Launch(shot, LaunchPoint(struck.Attacker, striker), BodyCenter(struck.Target, _shown[struck.Target]),
                                () => landed = true);
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
                            _effects.Hit(impact, BodyCenter(struck.Target, target), direction);
                            // le nuage de mort s'étale autour de la cible (ses coups sur les voisines suivent)
                            if (shot != null && struck.Attacker.Has(Ability.DeathCloud))
                                _effects.Hit("nuage", target.Position + new Vector2(0, HexHeight / 2 - Ui.Px(24)), direction);
                            _floating.Add(($"-{struck.Damage}", target.Position + Ui.V(0, -50), 0, MapRenderer.Rgb(255, 210, 120)));
                            if (struck.Killed > 0)
                                _floating.Add(($"{struck.Killed} †", target.Position + Ui.V(0, -30), 0, Colors.White));
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
                        if (flash == 0)
                        {
                            // la foudre tombe sur la cible
                            _bursts.Add((bolt.Target.Hexes.ToList(), MapRenderer.Rgb(255, 240, 120), 0));
                            var target = _shown[bolt.Target];
                            target.Count = Math.Max(0, target.Count - bolt.Killed);
                            target.Flash = 1;
                            _floating.Add(($"-{bolt.Damage}", target.Position + Ui.V(0, -50), 0, MapRenderer.Rgb(255, 240, 120)));
                            if (bolt.Killed > 0)
                                _floating.Add(($"{bolt.Killed} †", target.Position + Ui.V(0, -30), 0, Colors.White));
                        }
                        flash += delta;
                        if (flash < 0.35)
                            return false;
                        if (_shown[bolt.Target].Count == 0)
                            _shown[bolt.Target].Gone = true;
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
        // le sol : l'image du terrain, ×3
        var ground = Art.Terrain(DecorLayer.Covers(_terrain) ? TerrainType.Grass : _terrain, 0);
        if (ground != null)
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
            things.Add((center.Y, () => DrawObstacle(center)));
        }
        foreach (var (unit, shown) in _shown)
            if (!shown.Gone)
                things.Add((shown.Position.Y, () => DrawUnit(unit, shown)));
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

    private void DrawObstacle(Vector2 center)
    {
        var image = _terrain == TerrainType.Forest ? Art.Variant("decor/pine", (int)center.X) : Art.Variant("decor/peak-small", (int)center.X);
        if (image != null)
        {
            var size = image.GetSize() * GroundScale;
            DrawTextureRect(image, new Rect2(center + new Vector2(-size.X / 2, HexHeight / 2 - Ui.Px(8) - size.Y), size), false);
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
    private static Vector2 LaunchPoint(BattleUnit unit, Shown shown)
    {
        if (PointSetting(unit, "launchPoint") is not { } point || Sheet(unit, AttackState) is not { } sheet)
            return BodyCenter(unit, shown);
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
        var foot = shown.Position + new Vector2(shown.RecoilDirection * shown.Recoil * BattleEffects.Pixel * 3, HexHeight / 2 - Ui.Px(10));
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
        // L'image de combat (plus détaillée), sinon la petite de la carte ×3 ; une grande créature est centrée sur ses 2 cases
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
