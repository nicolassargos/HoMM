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
    /// <summary>Taille d'une case : largeur (écart entre deux colonnes) et hauteur (pointe à pointe).</summary>
    public const float HexWidth = 73, HexHeight = 84;
    private const float RowStep = HexHeight * 0.75f;
    private const int SpriteScale = 3;

    /// <summary>Agrandissement des images de combat (art/creatures/battle/).</summary>
    private const int BattleSpriteScale = 2;

    /// <summary>
    /// Les animations d'une créature : des feuilles de 8 images (4 colonnes, 2 rangées), à côté de son image de combat
    /// (« epeiste-idle.png », « epeiste-walk.png », « epeiste-attack.png »). Sans feuille, l'image fixe est montrée.
    /// </summary>
    private const int AnimationFrames = 8, AnimationColumns = 4, AnimationRows = 2;

    /// <summary>Images d'animation par seconde.</summary>
    private const double AnimationFps = 10;

    /// <summary>Pendant une attaque animée, le coup porte à ce moment (en secondes), vers la fin du geste.</summary>
    private const double HitDelay = 0.45;

    private const string Idle = "idle", Walk = "walk", AttackState = "attack";

    /// <summary>
    /// La place de chaque côté de la grille (étape 7) : le sol continue, pour les héros (et plus tard les machines de
    /// guerre : catapulte, baliste…).
    /// </summary>
    public const float SideMargin = 212;

    public static Vector2 FieldSize => new(Battlefield.Columns * HexWidth + HexWidth / 2 + 2 * SideMargin, (Battlefield.Rows - 1) * RowStep + HexHeight);

    /// <summary>Le héros de l'attaquant, dessiné à gauche de la grille (null : pas de héros, une vitrine…).</summary>
    public Hero? AttackerHero { get; set; }

    private const int HeroScale = 3;

    /// <summary>Où est dessiné le héros, à gauche de la grille (pour savoir si on clique dessus).</summary>
    public Rect2 HeroRect
    {
        get
        {
            var size = (Art.Get("heroes/hero")?.GetSize() ?? new Vector2(24, 24)) * HeroScale;
            return new Rect2(new Vector2(SideMargin / 2 - size.X / 2, FieldSize.Y / 2 - size.Y / 2), size);
        }
    }

    /// <summary>Un clic sur le héros.</summary>
    public event Action? HeroClicked;

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

        public void Play(string state)
        {
            State = state;
            StateTime = 0;
        }
    }

    private readonly Dictionary<BattleUnit, Shown> _shown = new();
    private readonly List<(string Text, Vector2 Position, float Age, Color Color)> _floating = new();

    /// <summary>Les cases à mettre en valeur : où la troupe active peut aller, la case survolée, la cible visée.</summary>
    public IReadOnlyCollection<Hex> Reachable { get; set; } = Array.Empty<Hex>();
    public Hex? Hovered { get; set; }
    public Hex? Target { get; set; }
    public BattleUnit? ActiveUnit { get; set; }

    /// <summary>Le joueur a cliqué sur le champ : la case, et le point exact (pour choisir de quel côté attaquer).</summary>
    public event Action<Hex, Vector2>? Clicked;

    public void Start(TacticalBattle battle, TerrainType terrain)
    {
        Battle = battle;
        _terrain = terrain;
        _shown.Clear();
        _floating.Clear();
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
        new(SideMargin + hex.Col * HexWidth + HexWidth / 2 + ((hex.Row & 1) == 1 ? HexWidth / 2 : 0), hex.Row * RowStep + HexHeight / 2);

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
                HeroClicked?.Invoke();
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
        foreach (var action in actions)
        {
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
                    _animations.Enqueue(delta =>
                    {
                        if (start == null)
                        {
                            start = shown.Position; // d'où elle part : là où elle est dessinée
                            shown.Play(Walk);
                        }
                        t += delta;
                        var from = step == 0 ? start.Value : points[step - 1];
                        // une case de marche en 0,09 s ; un vol prend le temps de toutes les cases survolées
                        float k = (float)Math.Min(1, t / (0.09 * Math.Max(1, from.DistanceTo(points[step]) / HexWidth)));
                        shown.Position = from.Lerp(points[step], k);
                        if (k < 1)
                            return false;
                        t = 0;
                        if (++step < points.Count)
                            return false;
                        shown.Play(Idle);
                        return true;
                    });
                    break;
                case UnitStruck struck:
                    double age = 0, wind = 0;
                    bool hit = false;
                    _animations.Enqueue(delta =>
                    {
                        // l'attaquant fait son geste (s'il a une animation d'attaque) ; le coup porte vers la fin
                        var striker = _shown[struck.Attacker];
                        if (wind == 0 && Sheet(struck.Attacker, AttackState) != null)
                            striker.Play(AttackState);
                        if (striker.State == AttackState && wind < HitDelay)
                        {
                            wind += delta;
                            return false;
                        }
                        wind = Math.Max(wind, 1e-6);
                        if (!hit)
                        {
                            hit = true;
                            var target = _shown[struck.Target];
                            target.Count = Math.Max(0, target.Count - struck.Killed);
                            target.Flash = 1;
                            _floating.Add(($"-{struck.Damage}", target.Position + new Vector2(0, -50), 0, MapRenderer.Rgb(255, 210, 120)));
                            if (struck.Killed > 0)
                                _floating.Add(($"{struck.Killed} †", target.Position + new Vector2(0, -30), 0, Colors.White));
                        }
                        age += delta;
                        if (age < 0.35)
                            return false;
                        if (_shown[struck.Target].Count == 0)
                            _shown[struck.Target].Gone = true;
                        if (striker.State == AttackState)
                            striker.Play(Idle);
                        return true;
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
            shown.StateTime += delta;
        }
        for (int i = _floating.Count - 1; i >= 0; i--)
        {
            var (text, position, age, color) = _floating[i];
            age += (float)delta;
            if (age > 1.1f)
                _floating.RemoveAt(i);
            else
                _floating[i] = (text, position + new Vector2(0, -30 * (float)delta), age, color);
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
            var tile = ground.GetSize() * SpriteScale;
            // des cases entières, la dernière de chaque rangée coupée au bord du champ (les créatures, elles, peuvent dépasser)
            for (float y = 0; y < Size.Y; y += tile.Y)
            for (float x = 0; x < Size.X; x += tile.X)
            {
                var part = new Vector2(Math.Min(tile.X, Size.X - x), Math.Min(tile.Y, Size.Y - y));
                DrawTextureRectRegion(ground, new Rect2(new Vector2(x, y), part), new Rect2(Vector2.Zero, part / SpriteScale));
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
            DrawPolyline(corners.Append(corners[0]).ToArray(), new Color(0, 0, 0, 0.28f), 1);
        }
        if (ActiveUnit is { IsAlive: true } active && _shown.TryGetValue(active, out var activeShown) && !IsAnimating)
            foreach (var hex in active.Hexes)
                DrawHexOutline(Center(hex), MapRenderer.Rgb(240, 200, 80), 3);
        if (Hovered is { } hovered)
            DrawHexOutline(Center(hovered), new Color(1, 1, 1, 0.7f), 2);
        if (Target is { } target)
            foreach (var hex in Battle.UnitAt(target)?.Hexes ?? new[] { target })
                DrawHexOutline(Center(hex), MapRenderer.Rgb(220, 60, 40), 3);

        // le héros, à gauche de la grille (son image de la carte en attendant une image de combat)
        if (AttackerHero != null && Art.Get("heroes/hero") is { } heroImage)
            DrawTextureRect(heroImage, HeroRect, false);

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

        var font = ThemeDB.FallbackFont;
        foreach (var (text, position, age, color) in _floating)
        {
            var faded = new Color(color, Math.Clamp(1.4f - age, 0, 1));
            DrawString(font, position + new Vector2(-20, 0), text, HorizontalAlignment.Center, 40, Ui.TextSize(18), faded);
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
            var size = image.GetSize() * 2;
            DrawTextureRect(image, new Rect2(center + new Vector2(-size.X / 2, HexHeight / 2 - 8 - size.Y), size), false);
            return;
        }
        DrawColoredPolygon(new[] { center + new Vector2(-22, 18), center + new Vector2(-8, -16), center + new Vector2(10, -10), center + new Vector2(24, 18) },
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
        int index = (int)(time * AnimationFps);
        index = state == AttackState ? Math.Min(index, AnimationFrames - 1) : index % AnimationFrames;
        var cell = new Vector2(sheet.GetWidth() / AnimationColumns, sheet.GetHeight() / AnimationRows);
        return (sheet, new Rect2(new Vector2(index % AnimationColumns * cell.X, index / AnimationColumns * cell.Y), cell));
    }

    private void DrawUnit(BattleUnit unit, Shown shown)
    {
        var foot = shown.Position + new Vector2(0, HexHeight / 2 - 10);
        var tint = shown.Flash > 0 ? new Color(1, 1 - shown.Flash * 0.6f, 1 - shown.Flash * 0.6f) : Colors.White;
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
            if (unit.Side == BattleSide.Defender)
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
            DrawCircle(foot + new Vector2(0, -24), 20, unit.Side == BattleSide.Attacker ? MapRenderer.PlayerColor : MapRenderer.NeutralColor);

        // le nombre de créatures, dans une petite boîte à la couleur du camp
        string count = shown.Count.ToString();
        var box = new Rect2(shown.Position + new Vector2(unit.Side == BattleSide.Attacker ? 6 : -36, HexHeight / 2 - 24), 30, 18);
        DrawRect(box, unit.Side == BattleSide.Attacker ? MapRenderer.Rgb(40, 60, 120) : MapRenderer.Rgb(110, 40, 30));
        DrawRect(box, MapRenderer.Rgb(230, 200, 120), false, 1);
        DrawString(ThemeDB.FallbackFont, box.Position + new Vector2(0, 14), count, HorizontalAlignment.Center, box.Size.X, Ui.TextSize(12), Colors.White);
    }
}
