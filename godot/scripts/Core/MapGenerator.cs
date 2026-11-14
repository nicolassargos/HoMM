using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Génère une carte d'aventure aléatoire. Même graine = même carte.
/// La carte est découpée en zones séparées par des montagnes ou des rivières et reliées par des
/// passages gardés. Plus une zone est loin du départ (en passages à franchir), plus ses monstres
/// sont forts. Des villes neutres attendent dans les zones voisines du départ et plus loin.
/// </summary>
public sealed class MapGenerator
{
    private readonly Random _rng;
    private readonly int _seed;
    /// <summary>Le hasard de la faction des villes neutres, à part : il ne change pas le reste de la carte.</summary>
    private readonly Random _factionRng;
    private readonly GameData? _data;
    private AdventureMap _map = null!;

    private static readonly string[] TownNames =
    {
        "Bellerive", "Hautemont", "Valombre", "Clairefont", "Roc-Fleuri", "Mirebois",
        "Pierrelune", "Bois-Doré", "Montclair", "Sombreval", "Aiguerive", "Combelaine", "Brunehaut", "Fontargent",
    };

    /// <summary>
    /// D'autres noms, pour les grandes cartes (une ville pour trois zones : une trentaine sur la très grande ; demandé par
    /// l'utilisateur, 2026-11-14). Mêlés à part, après les premiers : les cartes qui en ont assez ne changent pas.
    /// </summary>
    private static readonly string[] MoreTownNames =
    {
        "Castelroc", "Vaubrume", "Haut-Lierre", "Marbrecoeur", "Rivelune", "Pontdragon", "Orgemont", "Saule-Gris",
        "Ventelande", "Corbeval", "Ferrandine", "Givremont", "Lunebourg", "Mortefontaine", "Sylvebrune", "Cendrelac",
        "Belle-Épine", "Roncevaux", "Hautbois", "Étoile-Morte", "Valcendre", "Pierrefeu", "Clairmarais", "Aubefort",
        "Noirsapin", "Fleur-de-Sel",
    };

    /// <param name="data">Les données de jeu, pour les villes et les monstres. Sans données : ni ville ni monstre.</param>
    public MapGenerator(int seed, GameData? data = null)
    {
        _rng = new Random(seed);
        _seed = seed;
        _factionRng = new Random(seed * 7919 + 17);
        _data = data;
    }

    /// <summary>La faction de chaque joueur, dans l'ordre des départs (son château de départ) ; sans : le Château.</summary>
    public IReadOnlyList<string> PlayerFactions { get; init; } = new List<string>();

    /// <summary>La faction par défaut des villes (et des joueurs sans faction choisie).</summary>
    public const string DefaultFaction = "chateau";

    /// <summary>Monstres placés n'importe où, en plus de ceux qui gardent les passages, les mines et les coffres (pour une carte de 40 × 30).</summary>
    public int WanderingGuardCount { get; init; } = 5;

    /// <summary>
    /// Niveau maximal des monstres. Avec le recrutement dans les villes, tous les niveaux
    /// sont possibles (réglage vérifié par une simulation, voir BalanceTests).
    /// </summary>
    public int MaxGuardTier { get; init; } = 7;

    /// <summary>
    /// Grossissement des troupes de monstres avec la distance : dans la zone la plus lointaine,
    /// une troupe compte (1 + GuardSizeGrowth) fois plus de créatures qu'au départ.
    /// </summary>
    public double GuardSizeGrowth { get; init; } = 1.0;

    /// <summary>Une mine par zone, et au moins une de chaque ressource.</summary>
    // une mine par zone comme avant (les gemmes comprises), plus une de chacune des trois autres ressources rares : autant
    // de mines d'or, de bois et de minerai qu'avant
    public int MineCount => Math.Max(ResourceInfo.All.Length, (int)Math.Round(LandZones * MinesPerZone) + ResourceInfo.Rare.Length - 1);

    /// <summary>Le nombre de zones de terre (les zones de mer n'ont ni mine ni ville).</summary>
    private int LandZones => _zones.Count(z => !z.Sea);

    /// <summary>Mines par zone (la carte paraissait vide avec une seule ; Heroes 3 en met souvent 2 à 4).</summary>
    public double MinesPerZone { get; init; } = 1.5;

    /// <summary>
    /// Les factions dont viennent les monstres de la carte : les neutres, les morts-vivants, les Barbares et la Forêt
    /// (étape 9 : le Gobelin, le Loup, l'Orc, l'Ogre et le Troll étaient des neutres et gardent la carte).
    /// </summary>
    public static readonly string[] MonsterFactions = { "neutre", "morts-vivants", "barbares", "foret", "tour", "donjon", "enfer", "nains" };

    /// <summary>
    /// La force (paysans pour en vaincre une, <see cref="CreatureStrength"/>) du gardien pour lequel
    /// <see cref="GuardBaseCount"/> est réglé, par niveau : celle des neutres d'avant les factions (Gobelin 3, Loup 9,
    /// Orc 13, Ogre 31, Troll 50, le Troll encore au niveau 6, Dragon rouge 242).
    /// </summary>
    private static readonly int[] GuardReferenceStrength = { 0, 3, 9, 13, 31, 50, 50, 242 };

    /// <summary>
    /// Pour passer de ces forces en paysans à la mesure contre une vraie armée (<see cref="CreatureStrength.GoldToBeat"/>) :
    /// la créature de chaque niveau dont la force en paysans n'a pas changé depuis (le Vampire pour le Troll d'alors).
    /// </summary>
    private static readonly string[] GuardReferenceCreature = { "", "gobelin", "loup", "orc", "ogre", "vampire", "vampire", "dragon" };

    /// <summary>
    /// Un gardien qui n'aurait pas une seule créature de son niveau (moins de 0,75 : une seule serait bien trop forte, comme
    /// un Démon là où le gardien attendu vaut trois Dragons rouges) prend la créature du niveau en dessous de sa faction.
    /// </summary>
    private const double SmallestGuard = 0.75;

    /// <summary>Coffres et tas de ressources, pour une carte de 40 × 30 (plus sur une grande carte).</summary>
    public int ChestCount { get; init; } = 6;
    public int PileCount { get; init; } = 42; // 28 avant (la carte paraissait vide)

    /// <summary>Taille visée d'une zone, en cases : la carte est découpée en autant de zones qu'il en rentre.</summary>
    public const int ZoneWidth = 18;
    public const int ZoneHeight = 13;

    /// <summary>Chance d'ouvrir un passage de plus entre deux zones voisines (en plus de ceux qui relient tout).</summary>
    public double ExtraPassageChance { get; init; } = 0.25;

    /// <summary>Carte de référence pour le nombre d'objets (coffres, tas, monstres errants) : 40 × 30 cases.</summary>
    private const int ReferenceArea = 40 * 30;

    /// <summary>
    /// Nombre de joueurs (étape 8) : un départ chacun, aux coins de la carte (le joueur humain en haut à gauche, puis le
    /// coin opposé, puis les deux autres).
    /// </summary>
    public int Players { get; init; } = 1;

    /// <summary>Chaque joueur commence avec un château sur son départ (sans : le départ en pleine nature, comme avant).</summary>
    public bool StartingTowns { get; init; } = true;

    /// <summary>La force des gardiens des passages entre les zones (<see cref="ZoneAccess"/>).</summary>
    public ZoneAccess Access { get; init; } = ZoneAccess.Open;

    /// <summary>Le type de la carte : la part de la mer (<see cref="MapType"/>).</summary>
    public MapType Type { get; init; } = MapType.Continent;

    /// <summary>Une carte où l'on navigue : des zones de mer, ou des îles.</summary>
    private bool Sailing => Type != MapType.Continent;

    /// <summary>Après <see cref="Generate(int, int)"/> : les cases des gardiens des sorties des zones de départ.</summary>
    public IReadOnlyList<Cell> StartExitGates => _passages
        .Where(p => _zones[p.A].Level == 0 || _zones[p.B].Level == 0).Select(p => p.Gate).ToList();

    /// <summary>
    /// Combien de fois l'armée de départ il faut pour franchir la sortie de sa zone de départ (en perdant au plus
    /// <see cref="StartExitLosses"/>), selon <see cref="Access"/>.
    /// </summary>
    public static double StartExitArmies(ZoneAccess access) => access switch
    {
        ZoneAccess.Normal => 2,
        ZoneAccess.Closed => 4,
        _ => 1,
    };

    /// <summary>Les départs : à 4 cases des bords, dans l'ordre des joueurs.</summary>
    private static IEnumerable<Cell> StartCells(int width, int height, int players) =>
        new[] { new Cell(4, 4), new Cell(width - 5, height - 5), new Cell(width - 5, 4), new Cell(4, height - 5) }.Take(players);

    /// <summary>La distance (à vol d'oiseau) jusqu'au départ le plus proche.</summary>
    private double DistanceToStart(Cell c) => _map.Starts.Min(s => Distance(c, s));

    /// <summary>La distance en cases (diagonales comprises) jusqu'au départ le plus proche.</summary>
    private int StepsToStart(Cell c) => _map.Starts.Min(s => Math.Max(Math.Abs(c.X - s.X), Math.Abs(c.Y - s.Y)));

    public AdventureMap Generate(int width = 54, int height = 40)
    {
        if (_data != null)
            CreatureStrength.Prepare(_data.Creatures);
        _map = new AdventureMap(width, height);
        _map.Starts.AddRange(StartCells(width, height, Math.Clamp(Players, 1, 4)));
        _landArea = width * height;
        PaintTerrain();
        MakeZones();
        if (_sea != null)
            _landArea = AllCells().Count(c => !_sea[c.X, c.Y]);
        ClearStartArea();
        if (_data != null)
            PlaceTowns(_data);
        PlaceObjects();
        if (_data != null)
            PlaceGuards(_data.Creatures);
        RemoveUnreachableGuards();
        return _map;
    }

    /// <summary>
    /// Retire les monstres qu'aucun chemin n'atteint, même en battant les autres monstres (par exemple le gardien
    /// d'une mine posé derrière elle, dans un cul-de-sac) : on ne pourrait jamais les vaincre tous.
    /// </summary>
    private void RemoveUnreachableGuards()
    {
        var seen = new HashSet<Cell> { _map.Start };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell != _map.Start && _map.ObjectAt(cell) is { } obj && obj is not MonsterGuard)
                continue; // on visite un objet, on ne le traverse pas
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var next = cell.Offset(dx, dy);
                if (!Travelable(next) || seen.Contains(next))
                    continue;
                // pas de diagonale qui coupe un coin infranchissable (comme le Pathfinder)
                if (dx != 0 && dy != 0 && !(Travelable(cell.Offset(dx, 0)) && Travelable(cell.Offset(0, dy))))
                    continue;
                seen.Add(next);
                queue.Enqueue(next);
            }
        }
        foreach (var guard in _map.Objects.OfType<MonsterGuard>().Where(g => !seen.Contains(g.Position)).ToList())
            _map.RemoveObject(guard);
    }

    /// <summary>Nombre d'objets à poser pour cette carte, à partir du nombre prévu pour la carte de référence.</summary>
    private int Scaled(int count) => (int)Math.Round((double)count * _landArea / ReferenceArea);

    /// <summary>Le nombre de cases de terre (toute la carte sur un continent ; sans la mer sur une carte où l'on navigue).</summary>
    private int _landArea;

    // ------------------------------------------------------------------- zones

    /// <summary>
    /// Une zone de la carte. Les zones sont séparées par des montagnes ou des rivières,
    /// et reliées par des passages gardés. <see cref="Level"/> : nombre de passages à franchir depuis le départ.
    /// </summary>
    private sealed class Zone
    {
        public int Index { get; init; }
        public Cell Center { get; set; }
        public int Level { get; set; } = -1;

        /// <summary>Une zone toute de mer (<see cref="MapType"/>) : ni passage, ni objet.</summary>
        public bool Sea { get; set; }

        /// <summary>L'île de la zone : on passe d'une île à l'autre en bateau (sur un continent, une seule « île »).</summary>
        public int Island { get; set; }
    }

    /// <summary>Un passage entre deux zones. <see cref="Gate"/> : la case du gardien, du côté de la zone la plus lointaine.</summary>
    private sealed record Passage(int A, int B, Cell SideA, Cell SideB)
    {
        public Cell Gate { get; init; }
    }

    private readonly List<Zone> _zones = new();
    private readonly List<Passage> _passages = new();
    private int[,] _zoneOf = null!;
    private bool[,] _border = null!;

    /// <summary>
    /// Les cases prises par les mines : leurs 6 cases et la case devant l'entrée (aucun objet ni monstre n'y va, l'entrée
    /// reste libre).
    /// </summary>
    private readonly HashSet<Cell> _mineCells = new();

    /// <summary>
    /// Les cases sous l'image d'un lieu haut (<see cref="TallSite"/>), qui déborde d'une demi-case de chaque côté : on y
    /// passe, mais aucun autre objet ni monstre n'y est posé.
    /// </summary>
    private readonly HashSet<Cell> _overhang = new();

    /// <summary>
    /// Pose une mine d'entrée <paramref name="entrance"/> si ses 6 cases et la case devant l'entrée sont libres, et si
    /// tout reste accessible ; sinon rien (null).
    /// </summary>
    private Mine? TryPlaceMine(Cell entrance, ResourceType resource, HashSet<Cell> freeCells)
    {
        // ses 6 cases sur des cases libres ; devant l'entrée, une case où l'on passe (une route va bien)
        var approach = entrance.Offset(0, 1);
        var cells = Mine.FootprintAt(entrance).Append(approach).ToList();
        if (!Mine.FootprintAt(entrance).All(c => freeCells.Contains(c))
            || !_map.IsPassable(approach) || _reserved.Contains(approach) || approach == _map.Treasure
            || !cells.All(c => !_mineCells.Contains(c) && _map.OccupantAt(c) == null))
            return null;
        var mine = new Mine(entrance, resource);
        _map.AddObject(mine);
        if (!AllObjectsReachable())
        {
            _map.RemoveObject(mine);
            return null;
        }
        _mineCells.UnionWith(cells);
        return mine;
    }

    /// <summary>Les mines posées dans la zone de départ d'un joueur (bois, minerai, ressource principale) : sans gardien.</summary>
    private readonly HashSet<MapObject> _startZoneMines = new();

    /// <summary>
    /// La scierie et la mine de minerai près de chaque ville neutre (choix de l'utilisateur, 2026-11-14) : gardées par une
    /// petite armée (<see cref="TownMineGuardFactor"/>).
    /// </summary>
    private readonly HashSet<MapObject> _townMines = new();

    /// <summary>Les cases qu'aucun objet ne doit couvrir (les places des gardiens des mines près des villes, leurs abords).</summary>
    private readonly HashSet<Cell> _keptFree = new();

    /// <summary>
    /// Les cases libres à côté de l'entrée d'une mine (posée ou à poser en <paramref name="entrance"/>) où son gardien peut
    /// se tenir : de part et d'autre de la case d'où l'on y entre, à 4 cases au moins d'une ville.
    /// </summary>
    private List<Cell> GuardSpotsFor(Cell entrance, HashSet<Cell> freeSet)
    {
        var mine = Mine.FootprintAt(entrance).Append(entrance.Offset(0, 1)).ToHashSet();
        return Neighbours(entrance).Where(n => !mine.Contains(n) && freeSet.Contains(n) && DistanceToNearestTown(n) >= 4)
            .OrderBy(n => n.Y).ThenBy(n => n.X).ToList();
    }

    /// <summary>La taille du gardien d'une mine près d'une ville, par rapport à un gardien ordinaire de sa zone.</summary>
    public double TownMineGuardFactor { get; init; } = 0.5;

    /// <summary>Cases des passages et leurs abords : ni objet, ni ville, ni autre monstre.</summary>
    private readonly HashSet<Cell> _reserved = new();

    /// <summary>
    /// Les pas en diagonale des routes : leurs deux coins restent praticables et sans objet (un héros ne coupe pas un coin
    /// bloqué ; un monstre peut s'y tenir, comme sur la route : on le combat). Une ville peut couvrir un coin : l'autre devient alors une route (<see cref="KeepRoadsAround"/>).
    /// </summary>
    private readonly List<(Cell A, Cell B)> _diagonalSteps = new();
    private readonly HashSet<Cell> _roadCorners = new();

    private int MaxLevel => _zones.Count == 0 ? 0 : _zones.Max(z => z.Level);

    /// <summary>
    /// L'échelle du danger : le niveau de la zone la plus lointaine si le joueur était seul (depuis son départ). Avec des
    /// adversaires, chaque zone est plus près d'un départ : compter sur la même échelle que seul garde des voisines de
    /// départ à la portée de l'armée de départ (sinon les zones voisines devenaient aussi dures que le milieu).
    /// </summary>
    private int _dangerScale;

    private Zone ZoneAt(Cell c) => _zones[_zoneOf[c.X, c.Y]];

    /// <summary>
    /// Découpe la carte en zones (les cases vont au centre de zone le plus proche), dresse des murs
    /// entre elles, ouvre des passages pour que tout soit relié, et trace les routes de chaque zone.
    /// </summary>
    private void MakeZones()
    {
        int cols = Math.Max(1, _map.Width / ZoneWidth), rows = Math.Max(1, _map.Height / ZoneHeight);
        double cellWidth = (double)_map.Width / cols, cellHeight = (double)_map.Height / rows;
        // les zones des départs (des coins de la grille) ne sont pas décalées : chaque départ reste bien à l'intérieur
        var startSlots = _map.Starts.Select(s => ((int)(s.Y / cellHeight), (int)(s.X / cellWidth))).ToHashSet();
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            bool fixedZone = startSlots.Contains((r, c));
            double jx = fixedZone ? 0 : (_rng.NextDouble() - 0.5) * cellWidth * 0.4;
            double jy = fixedZone ? 0 : (_rng.NextDouble() - 0.5) * cellHeight * 0.4;
            var center = new Cell((int)((c + 0.5) * cellWidth + jx), (int)((r + 0.5) * cellHeight + jy));
            _zones.Add(new Zone { Index = _zones.Count, Center = center });
        }

        _zoneOf = new int[_map.Width, _map.Height];
        _border = new bool[_map.Width, _map.Height];
        // Des bords de zones qui serpentent (pas des lignes droites de cases ; choix de l'utilisateur, 2026-11-14) : chaque
        // case est déplacée par un bruit doux avant de chercher le centre le plus proche ; rien autour des départs, pour que
        // les murs restent loin du château
        var (warpX, warpY) = (SmoothNoise(ZoneWarpStep), SmoothNoise(ZoneWarpStep));
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
        {
            double nearStart = _map.Starts.Min(s => Math.Sqrt(SquaredDistance(s, new Cell(x, y))));
            double amount = ZoneWarp * Math.Clamp((nearStart - 9) / 6, 0, 1);
            double wx = x + amount * warpX(x, y), wy = y + amount * warpY(x, y);
            _zoneOf[x, y] = _zones.OrderBy(z => (z.Center.X - wx) * (z.Center.X - wx) + (z.Center.Y - wy) * (z.Center.Y - wy)).First().Index;
        }
        JoinZoneIslands();

        var startZone = ZoneAt(_map.Start);
        var startZones = _map.Starts.Select(ZoneAt).Distinct().ToList();
        if (_zones.Count == 1)
        {
            startZone.Level = 0;
            StartRoad(_map.Start, startZone, new HashSet<Cell>());
            return;
        }

        // Les murs : chaque case qui touche une autre zone
        var neighbours = new SortedSet<(int A, int B)>();
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
            foreach (var n in Orthogonal(new Cell(x, y)).Where(_map.InBounds))
                if (_zoneOf[n.X, n.Y] != _zoneOf[x, y])
                {
                    _border[x, y] = true;
                    int a = _zoneOf[x, y], b = _zoneOf[n.X, n.Y];
                    neighbours.Add((Math.Min(a, b), Math.Max(a, b)));
                }

        // la mer : des zones de mer, des îles séparées par l'eau (pas sur un continent) ; d'une île à l'autre, une traversée
        // compte comme un passage pour le niveau des zones
        var crossings = new List<(int A, int B)>();
        if (Sailing)
            ShapeSeas(neighbours, startZones);

        // On ne relie que des zones voisines de la même île où un passage peut s'ouvrir ; la côte peut couper toutes les
        // places d'un passage : l'île est alors coupée en deux (avec quelques mers, une zone de mer redevient de la terre)
        Dictionary<(int A, int B), List<(Cell P, Cell Q)>> openings;
        do
            openings = neighbours.ToDictionary(n => n,
                n => SameIsland(n.A, n.B) ? PassageCandidates(n.A, n.B) : new List<(Cell P, Cell Q)>());
        while (Sailing && !IslandsWhole(neighbours, openings, startZones));
        if (Sailing)
        {
            PaintSea();
            crossings = Crossings(neighbours);
        }
        var links = ChooseLinks(neighbours.Where(n => openings[n].Count > 0).ToList(), startZone.Index);
        // l'échelle du danger, comme si le joueur était seul ; puis les vrais niveaux, depuis le départ le plus proche
        ComputeLevels(links.Concat(crossings).ToList(), new List<Zone> { startZone });
        _dangerScale = MaxLevel;
        foreach (var zone in _zones)
            zone.Level = -1;
        ComputeLevels(links.Concat(crossings).ToList(), startZones);
        PaintWalls(neighbours);

        var carved = new HashSet<Cell>();
        foreach (var (a, b) in links)
            OpenPassage(a, b, openings[(a, b)], carved);

        // Les routes : du centre de chaque zone à ses passages (et à la porte du château de départ)
        foreach (var zone in _zones)
        {
            foreach (var passage in _passages.Where(p => p.A == zone.Index || p.B == zone.Index))
                Road(Hub(zone), PassageSide(passage, zone.Index), zone.Index, carved);
            foreach (var start in _map.Starts.Where(s => ZoneAt(s) == zone))
                StartRoad(start, zone, carved);
        }
        // sur une carte où l'on navigue, une zone sans passage (une petite île) : une route de son centre au rivage, où l'on
        // débarque (sinon son centre, et la ville posée près de lui, pourraient rester dans un creux de montagnes)
        if (Sailing)
            foreach (var zone in _zones.Where(z => !z.Sea && !_passages.Any(p => p.A == z.Index || p.B == z.Index)))
            {
                var hub = Hub(zone);
                if (AllCells().Where(c => _zoneOf[c.X, c.Y] == zone.Index && !IsSea(c) && !_border[c.X, c.Y] && !InStartingTown(c)
                            && Orthogonal(c).Any(IsSea))
                        .OrderBy(c => SquaredDistance(c, hub)).ThenBy(c => c.Y).ThenBy(c => c.X).Cast<Cell?>().FirstOrDefault() is { } shore)
                    Road(hub, shore, zone.Index, carved);
            }
    }

    /// <summary>
    /// Les bords qui serpentent peuvent détacher un morceau d'une zone, perdu dans une autre (il serait entouré de murs) :
    /// chaque zone ne garde que le morceau de son centre, les autres rejoignent la zone qui les entoure.
    /// </summary>
    private void JoinZoneIslands()
    {
        var kept = new bool[_map.Width, _map.Height];
        foreach (var zone in _zones)
        {
            _zoneOf[zone.Center.X, zone.Center.Y] = zone.Index; // son centre lui reste
            var queue = new Queue<Cell>(new[] { zone.Center });
            kept[zone.Center.X, zone.Center.Y] = true;
            while (queue.Count > 0)
                foreach (var n in Orthogonal(queue.Dequeue()).Where(_map.InBounds))
                    if (!kept[n.X, n.Y] && _zoneOf[n.X, n.Y] == zone.Index)
                    {
                        kept[n.X, n.Y] = true;
                        queue.Enqueue(n);
                    }
        }
        // les cases perdues prennent, de proche en proche, la zone d'une voisine gardée
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int y = 0; y < _map.Height; y++)
            for (int x = 0; x < _map.Width; x++)
            {
                if (kept[x, y])
                    continue;
                var from = Orthogonal(new Cell(x, y)).Where(_map.InBounds).FirstOrDefault(n => kept[n.X, n.Y], new Cell(-1, -1));
                if (from.X < 0)
                    continue;
                _zoneOf[x, y] = _zoneOf[from.X, from.Y];
                kept[x, y] = true;
                changed = true;
            }
        }
    }

    // ------------------------------------------------------------------- la mer

    /// <summary>Les cases de la mer : les zones de mer et les côtes des îles (null sur un continent).</summary>
    private bool[,]? _sea;

    private bool IsSea(Cell c) => _sea != null && _map.InBounds(c) && _sea[c.X, c.Y];

    private bool SameIsland(int a, int b) => !_zones[a].Sea && !_zones[b].Sea && _zones[a].Island == _zones[b].Island;

    /// <summary>La part des zones toutes de mer, selon le type de la carte.</summary>
    private double SeaZoneShare => Type switch
    {
        MapType.Seas => 0.2,
        MapType.Islands => 0.15,
        MapType.Archipelago => 0.15,
        _ => 0,
    };

    /// <summary>Le nombre de zones d'une île neutre (l'île d'un joueur en a au moins deux : sa zone et la suivante).</summary>
    private int IslandZones => Type switch
    {
        MapType.Islands => 3,
        MapType.Archipelago => 1,
        _ => int.MaxValue,
    };

    /// <summary>
    /// Les zones de mer et les îles (<see cref="MapType"/>), puis les cases de la mer (<see cref="ComputeSeaCells"/>).
    /// </summary>
    private void ShapeSeas(SortedSet<(int A, int B)> neighbours, List<Zone> startZones)
    {
        var adjacent = _zones.ToDictionary(z => z.Index, _ => new List<int>());
        foreach (var (a, b) in neighbours)
        {
            adjacent[a].Add(b);
            adjacent[b].Add(a);
        }
        bool LandConnected()
        {
            var land = _zones.Where(z => !z.Sea).Select(z => z.Index).ToList();
            var seen = new HashSet<int> { land[0] };
            var queue = new Queue<int>(seen);
            while (queue.Count > 0)
                foreach (int n in adjacent[queue.Dequeue()])
                    if (!_zones[n].Sea && seen.Add(n))
                        queue.Enqueue(n);
            return seen.Count == land.Count;
        }

        // les zones de mer : jamais un départ, et chaque départ garde une zone de terre voisine (son île en a deux) ; avec
        // quelques mers, tout reste relié par la terre
        int wanted = (int)Math.Round(_zones.Count * SeaZoneShare);
        var order = _zones.Where(z => !startZones.Contains(z)).Select(z => z.Index).ToList();
        Shuffle(order);
        foreach (int i in order)
        {
            if (wanted == 0)
                break;
            _zones[i].Sea = true;
            bool ok = startZones.All(s => adjacent[s.Index].Any(n => !_zones[n].Sea))
                && (Type != MapType.Seas || LandConnected());
            if (ok)
            {
                wanted--;
                _seaZones.Add(i);
            }
            else
                _zones[i].Sea = false;
        }

        // les îles : celles des joueurs d'abord, qui grandissent d'une zone chacune à tour de rôle, puis les îles neutres
        if (Type is MapType.Islands or MapType.Archipelago)
        {
            foreach (var zone in _zones)
                zone.Island = -1;
            var islands = new List<List<int>>();
            void Grow(List<int> island, int size)
            {
                var next = island.SelectMany(z => adjacent[z]).Distinct()
                    .Where(n => !_zones[n].Sea && _zones[n].Island < 0).OrderBy(n => n).ToList();
                if (island.Count >= size || next.Count == 0)
                    return;
                int chosen = next[_rng.Next(next.Count)];
                _zones[chosen].Island = _zones[island[0]].Island;
                island.Add(chosen);
            }
            foreach (var start in startZones)
            {
                start.Island = islands.Count;
                islands.Add(new List<int> { start.Index });
            }
            int startSize = Math.Max(2, IslandZones);
            for (int round = 1; round < startSize; round++)
                foreach (var island in islands.ToList())
                    Grow(island, startSize);
            var rest = _zones.Where(z => !z.Sea && z.Island < 0).Select(z => z.Index).ToList();
            Shuffle(rest);
            foreach (int i in rest.Where(i => _zones[i].Island < 0))
            {
                _zones[i].Island = islands.Count;
                var island = new List<int> { i };
                islands.Add(island);
                for (int round = 1; round < IslandZones; round++)
                    Grow(island, IslandZones);
            }
        }

        // la mer : les zones de mer en entier, et la côte des îles (sans la peindre : une zone de mer peut redevenir de la
        // terre, voir IslandsWhole)
        _shore = SmoothNoise(5);
        ComputeSeaCells();
    }

    /// <summary>Le bruit doux de la largeur des côtes (tiré une fois, pour que la côte ne change pas d'un essai à l'autre).</summary>
    private Func<int, int, double> _shore = null!;

    /// <summary>Les zones de mer choisies, dans l'ordre (la dernière redevient de la terre s'il le faut).</summary>
    private readonly List<int> _seaZones = new();

    /// <summary>La largeur de la côte d'une île, de 1 à 3 cases (de 1 à 2 dans l'archipel : ses îles sont petites).</summary>
    private double ShoreWidth(int x, int y) =>
        Type == MapType.Archipelago ? 1.5 + 0.7 * _shore(x, y) : 2 + 1.2 * _shore(x, y);

    /// <summary>
    /// Les cases de la mer : les zones de mer, et chaque case de terre à moins de <see cref="ShoreWidth"/> d'un rivage (une
    /// case qui touche une autre île ou une zone de mer) ; sur les îles et l'archipel, aussi le long des bords de la carte
    /// (pas près des départs : le château y reste).
    /// </summary>
    private void ComputeSeaCells()
    {
        int width = _map.Width, height = _map.Height;
        _sea = new bool[width, height];
        int IslandOf(int x, int y) => _zones[_zoneOf[x, y]].Sea ? -1 : _zones[_zoneOf[x, y]].Island;
        bool edges = Type is MapType.Islands or MapType.Archipelago;
        var distance = new int[width, height];
        var queue = new Queue<Cell>();
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            distance[x, y] = int.MaxValue;
            if (IslandOf(x, y) < 0)
            {
                _sea[x, y] = true;
                continue;
            }
            var cell = new Cell(x, y);
            bool coast = Orthogonal(cell).Where(_map.InBounds).Any(n => IslandOf(n.X, n.Y) != IslandOf(x, y));
            bool edge = edges && (x == 0 || y == 0 || x == width - 1 || y == height - 1) && StepsToStart(cell) > 9;
            if (coast || edge)
            {
                distance[x, y] = 0;
                queue.Enqueue(cell);
            }
        }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (distance[c.X, c.Y] >= 3)
                continue;
            foreach (var n in Neighbours(c))
                if (_map.InBounds(n) && !_sea[n.X, n.Y] && distance[n.X, n.Y] == int.MaxValue)
                {
                    distance[n.X, n.Y] = distance[c.X, c.Y] + 1;
                    queue.Enqueue(n);
                }
        }
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (distance[x, y] < ShoreWidth(x, y))
                _sea[x, y] = true;

        // chaque zone ne garde que son plus grand morceau de terre : la côte en détache parfois un bout, qu'on n'atteindrait
        // qu'en bateau (il devient de la mer)
        foreach (var zone in _zones.Where(z => !z.Sea))
        {
            var pieces = new List<List<Cell>>();
            var seen = new HashSet<Cell>();
            bool Land(Cell c) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone.Index && !_sea[c.X, c.Y];
            foreach (var cell in AllCells().Where(Land))
            {
                if (!seen.Add(cell))
                    continue;
                var piece = new List<Cell> { cell };
                for (int i = 0; i < piece.Count; i++)
                    foreach (var n in Orthogonal(piece[i]))
                        if (Land(n) && seen.Add(n))
                            piece.Add(n);
                pieces.Add(piece);
            }
            var kept = pieces.FirstOrDefault(p => p.Any(_map.Starts.Contains)) ?? pieces.MaxBy(p => p.Count);
            foreach (var cell in pieces.Where(p => p != kept).SelectMany(p => p))
                _sea[cell.X, cell.Y] = true;
        }
    }

    /// <summary>
    /// Chaque île est-elle d'un seul tenant, par des passages qui peuvent s'ouvrir ? La côte coupe parfois toutes les places
    /// d'un passage : avec quelques mers (tout doit s'atteindre à pied), la dernière zone de mer redevient de la terre ;
    /// sinon, le morceau coupé devient une île à part, que la mer entoure. Faux : on recommence.
    /// </summary>
    private bool IslandsWhole(SortedSet<(int A, int B)> neighbours, Dictionary<(int A, int B), List<(Cell P, Cell Q)>> openings,
        List<Zone> startZones)
    {
        var parts = new List<List<Zone>>();
        var seen = new HashSet<Zone>();
        foreach (var zone in _zones.Where(z => !z.Sea))
        {
            if (!seen.Add(zone))
                continue;
            var part = new List<Zone> { zone };
            for (int i = 0; i < part.Count; i++)
                foreach (var (a, b) in neighbours.Where(n => (n.A == part[i].Index || n.B == part[i].Index) && openings[n].Count > 0))
                {
                    var other = _zones[a == part[i].Index ? b : a];
                    if (seen.Add(other))
                        part.Add(other);
                }
            parts.Add(part);
        }
        var cut = parts.GroupBy(p => p[0].Island).Where(g => g.Count() > 1).ToList();
        if (cut.Count == 0)
            return true;
        if (Type == MapType.Seas && _seaZones.Count > 0)
        {
            _zones[_seaZones[^1]].Sea = false;
            _seaZones.RemoveAt(_seaZones.Count - 1);
            ComputeSeaCells();
            return false;
        }
        // le morceau qui a un départ (sinon le plus grand) garde l'île ; les autres en deviennent de nouvelles
        int next = _zones.Max(z => z.Island) + 1;
        foreach (var island in cut)
            foreach (var part in island.OrderBy(p => p.Any(startZones.Contains) ? 0 : 1).ThenByDescending(p => p.Count)
                         .ThenBy(p => p.Min(z => z.Index)).Skip(1))
            {
                foreach (var zone in part)
                    zone.Island = next;
                next++;
            }
        // la mer longe aussi les nouveaux rivages
        ComputeSeaCells();
        return false;
    }

    /// <summary>
    /// La mer peinte sur la carte. Le centre d'une zone tombé dans la mer (une petite île) revient à sa case de terre la plus
    /// proche : ses routes y mènent.
    /// </summary>
    private void PaintSea()
    {
        foreach (var cell in AllCells().Where(IsSea))
            _map.SetTerrain(cell, TerrainType.Water);
        foreach (var zone in _zones.Where(z => !z.Sea && (IsSea(z.Center) || _border[z.Center.X, z.Center.Y])))
            if (AllCells().Where(c => _zoneOf[c.X, c.Y] == zone.Index && !IsSea(c) && !_border[c.X, c.Y])
                    .OrderBy(c => SquaredDistance(c, zone.Center)).ThenBy(c => c.Y).ThenBy(c => c.X).Cast<Cell?>().FirstOrDefault() is { } land)
                zone.Center = land;
    }

    /// <summary>
    /// Les traversées : deux zones de terre d'îles différentes, voisines ou reliées par des zones de mer. Une traversée
    /// compte comme un passage pour le niveau des zones (et donc la force des monstres).
    /// </summary>
    private List<(int A, int B)> Crossings(SortedSet<(int A, int B)> neighbours)
    {
        var crossings = new SortedSet<(int A, int B)>();
        void Add(int a, int b)
        {
            if (!_zones[a].Sea && !_zones[b].Sea && _zones[a].Island != _zones[b].Island)
                crossings.Add((Math.Min(a, b), Math.Max(a, b)));
        }
        foreach (var (a, b) in neighbours)
            Add(a, b);
        // chaque mer (des zones de mer qui se touchent) : toutes les zones de terre de ses rivages
        var seen = new HashSet<int>();
        foreach (var sea in _zones.Where(z => z.Sea))
        {
            if (!seen.Add(sea.Index))
                continue;
            var waters = new List<int> { sea.Index };
            var shores = new SortedSet<int>();
            for (int i = 0; i < waters.Count; i++)
                foreach (var (a, b) in neighbours.Where(n => n.A == waters[i] || n.B == waters[i]))
                {
                    int other = a == waters[i] ? b : a;
                    if (!_zones[other].Sea)
                        shores.Add(other);
                    else if (seen.Add(other))
                        waters.Add(other);
                }
            foreach (int a in shores)
                foreach (int b in shores.Where(b => b > a))
                    Add(a, b);
        }
        return crossings.ToList();
    }

    /// <summary>De combien de cases les bords des zones s'écartent au plus d'une ligne droite, et la taille de leurs méandres.</summary>
    private const double ZoneWarp = 3;
    private const int ZoneWarpStep = 7;

    /// <summary>
    /// Un bruit doux sur la carte, de −1 à 1 : des valeurs au hasard toutes les <paramref name="step"/> cases, mêlées en
    /// douceur entre elles.
    /// </summary>
    private Func<int, int, double> SmoothNoise(int step)
    {
        int w = _map.Width / step + 2, h = _map.Height / step + 2;
        var values = new double[w, h];
        for (int j = 0; j < h; j++)
        for (int i = 0; i < w; i++)
            values[i, j] = _rng.NextDouble() * 2 - 1;
        static double Smooth(double t) => t * t * (3 - 2 * t);
        return (x, y) =>
        {
            double gx = (double)x / step, gy = (double)y / step;
            int i = (int)gx, j = (int)gy;
            double fx = Smooth(gx - i), fy = Smooth(gy - j);
            double top = values[i, j] + (values[i + 1, j] - values[i, j]) * fx;
            double bottom = values[i, j + 1] + (values[i + 1, j + 1] - values[i, j + 1]) * fx;
            return top + (bottom - top) * fy;
        };
    }

    private static int SquaredDistance(Cell a, Cell b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static IEnumerable<Cell> Orthogonal(Cell c) =>
        new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) };

    /// <summary>
    /// Les passages à ouvrir : de quoi relier toutes les zones (sur une carte où l'on navigue, celles de chaque île), plus
    /// quelques-uns au hasard.
    /// </summary>
    private List<(int A, int B)> ChooseLinks(List<(int A, int B)> neighbours, int startZone)
    {
        var links = new List<(int, int)>();
        var connected = new HashSet<int> { startZone };
        while (connected.Count < _zones.Count)
        {
            var frontier = neighbours.Where(n => connected.Contains(n.A) != connected.Contains(n.B)).ToList();
            if (frontier.Count == 0 && Sailing && _zones.FirstOrDefault(z => !z.Sea && !connected.Contains(z.Index)) is { } island)
            {
                // une autre île : on relie aussi ses zones entre elles
                connected.Add(island.Index);
                continue;
            }
            if (frontier.Count == 0)
                break;
            var link = frontier[_rng.Next(frontier.Count)];
            links.Add(link);
            connected.Add(link.A);
            connected.Add(link.B);
        }
        foreach (var n in neighbours)
            if (!links.Contains(n) && _rng.NextDouble() < ExtraPassageChance)
                links.Add(n);
        return links;
    }

    /// <summary>Le niveau de chaque zone : le nombre de passages à franchir depuis la zone de départ la plus proche.</summary>
    private void ComputeLevels(List<(int A, int B)> links, List<Zone> startZones)
    {
        foreach (var start in startZones)
            start.Level = 0;
        var queue = new Queue<Zone>(startZones);
        while (queue.Count > 0)
        {
            var zone = queue.Dequeue();
            foreach (var (a, b) in links)
            {
                int other = a == zone.Index ? b : b == zone.Index ? a : -1;
                if (other < 0 || _zones[other].Level >= 0)
                    continue;
                _zones[other].Level = zone.Level + 1;
                queue.Enqueue(_zones[other]);
            }
        }
    }

    /// <summary>Chaque frontière entre deux zones est faite de montagnes ou d'une rivière.</summary>
    private void PaintWalls(IEnumerable<(int A, int B)> neighbours)
    {
        // des murs de montagne, de forêt ou d'eau (moins de montagnes depuis que la forêt bloque aussi)
        var wallOf = neighbours.ToDictionary(n => n, _ => _rng.NextDouble() switch
        {
            < 0.35 => TerrainType.Mountain,
            < 0.7 => TerrainType.Forest,
            _ => TerrainType.Water,
        });
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
        {
            if (!_border[x, y])
                continue;
            var cell = new Cell(x, y);
            if (IsSea(cell))
                continue; // la mer est déjà peinte
            int own = _zoneOf[x, y];
            int other = Orthogonal(cell).Where(_map.InBounds).Select(n => _zoneOf[n.X, n.Y]).Where(z => z != own).Min();
            _map.SetTerrain(cell, wallOf[(Math.Min(own, other), Math.Max(own, other))]);
        }
    }

    /// <summary>
    /// Ouvre un passage dans le mur entre deux zones, au plus près du milieu de leurs centres, loin des
    /// autres zones : on traverse le mur tout droit, sur trois cases de large. Le gardien se place du côté
    /// de la zone la plus lointaine ; toutes les cases ouvertes touchent sa case (on ne passe pas sans combattre).
    /// </summary>
    private void OpenPassage(int a, int b, List<(Cell P, Cell Q)> candidates, HashSet<Cell> carved)
    {
        var (sideA, sideB) = candidates[_rng.Next(candidates.Count)];

        // Le gardien se place du côté de la zone la plus lointaine. On ouvre, sur trois cases de large, les
        // deux cases du mur et la case derrière le gardien : toutes touchent sa case, on ne passe donc pas
        // sans combattre. La case devant le mur, de l'autre côté, est seulement dégagée (pas un mur).
        var (gate, other) = _zones[a].Level > _zones[b].Level ? (sideA, sideB) : (sideB, sideA);
        int dx = gate.X - other.X, dy = gate.Y - other.Y;
        int px = dy, py = dx; // le long du mur
        void Open(Cell c)
        {
            if (!_map.InBounds(c) || (_zoneOf[c.X, c.Y] != a && _zoneOf[c.X, c.Y] != b))
                return;
            _map.SetTerrain(c, TerrainType.Grass);
            carved.Add(c);
        }
        foreach (var cell in new[] { other, gate, gate.Offset(dx, dy) })
            foreach (var opened in new[] { cell, cell.Offset(px, py), cell.Offset(-px, -py) })
                Open(opened);
        var front = other.Offset(-dx, -dy);
        if (_map.InBounds(front) && !_border[front.X, front.Y])
            Open(front);
        foreach (var cell in Around(sideA, 2).Concat(Around(sideB, 2)))
            _reserved.Add(cell);

        _passages.Add(new Passage(a, b, sideA, sideB) { Gate = gate });
    }

    /// <summary>
    /// Les meilleurs endroits où ouvrir un passage entre deux zones (vide s'il n'y en a pas) : P (mur de A)
    /// et Q (mur de B) côte à côte, avec l'intérieur de A derrière P et celui de B derrière Q, et une
    /// ouverture de trois cases de large qui ne touche aucune autre zone. Au plus près du milieu des centres.
    /// </summary>
    private List<(Cell P, Cell Q)> PassageCandidates(int a, int b)
    {
        var middle = new Cell((_zones[a].Center.X + _zones[b].Center.X) / 2, (_zones[a].Center.Y + _zones[b].Center.Y) / 2);
        bool InZones(Cell c) => _map.InBounds(c) && (_zoneOf[c.X, c.Y] == a || _zoneOf[c.X, c.Y] == b) && !IsSea(c);
        bool Inside(Cell c, int zone) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone && !_border[c.X, c.Y] && !IsSea(c);
        bool Valid(Cell p, Cell q)
        {
            int dx = q.X - p.X, dy = q.Y - p.Y;
            int px = dy, py = dx; // le long du mur
            if (!Inside(p.Offset(-dx, -dy), a) || !Inside(q.Offset(dx, dy), b))
                return false;
            var rows = new[] { p.Offset(-dx, -dy), p, q, q.Offset(dx, dy) };
            return rows.All(r => InZones(r) && InZones(r.Offset(px, py)) && InZones(r.Offset(-px, -py)));
        }
        bool Clean(Cell c) => Around(c, 2).All(InZones);

        var pairs = new List<(Cell P, Cell Q)>();
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
        {
            var p = new Cell(x, y);
            if (_zoneOf[x, y] != a || !_border[x, y])
                continue;
            foreach (var q in Orthogonal(p))
                if (InZones(q) && _zoneOf[q.X, q.Y] == b && Valid(p, q))
                    pairs.Add((p, q));
        }
        var clean = pairs.Where(c => Clean(c.P) && Clean(c.Q)).ToList();
        if (clean.Count > 0)
            pairs = clean;
        return pairs
            .OrderBy(c => SquaredDistance(c.P, middle)).ThenBy(c => c.P.Y).ThenBy(c => c.P.X)
            .Take(3).ToList();
    }

    /// <summary>La case du passage du côté de cette zone (là où arrive la route de la zone).</summary>
    private static Cell PassageSide(Passage passage, int zone) => passage.A == zone ? passage.SideA : passage.SideB;

    /// <summary>Les cases du carré de rayon donné autour d'une case (elle comprise).</summary>
    private static IEnumerable<Cell> Around(Cell c, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
            yield return c.Offset(dx, dy);
    }

    /// <summary>
    /// Trace une route (ponts et cols compris) par le plus court chemin à l'intérieur d'une zone,
    /// sans passer par ses murs (sauf les passages ouverts) ni par les cases interdites. Elle va aussi en diagonale
    /// (comme Heroes : des lignes à 45° plutôt que des escaliers ; choix de l'utilisateur, 2026-11-14), au plus près de
    /// la ligne droite entre ses deux bouts ; un pas en diagonale ne passe pas entre deux coins interdits.
    /// </summary>
    private void Road(Cell from, Cell to, int zone, HashSet<Cell> carved, HashSet<Cell>? forbidden = null, bool joinRoad = false)
    {
        bool Allowed(Cell c) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone && !IsSea(c)
            && (!_border[c.X, c.Y] || carved.Contains(c)) && forbidden?.Contains(c) != true && !InStartingTown(c);
        bool Enter(Cell c) => Allowed(c) || c == to;
        bool NextToRoad(Cell c) => Neighbours(c).Any(n => _map.InBounds(n) && _map.HasRoad(n));
        // l'écart à la ligne droite, qui départage les chemins de même longueur (pas pour rejoindre une route)
        double dx = to.X - from.X, dy = to.Y - from.Y, length = Math.Sqrt(dx * dx + dy * dy);
        double OffLine(Cell c) => joinRoad || length == 0 ? 0 : Math.Abs((c.X - from.X) * dy - (c.Y - from.Y) * dx) / length;

        var previous = new Dictionary<Cell, Cell> { [from] = from };
        var cost = new Dictionary<Cell, double> { [from] = 0 };
        var queue = new PriorityQueue<Cell, double>();
        queue.Enqueue(from, 0);
        while (queue.TryDequeue(out var c, out double known))
        {
            if (known > cost[c])
                continue;
            if (c == to)
                break;
            // joinRoad : on s'arrête à la première route rencontrée (la plus proche)
            if (joinRoad && c != from && _map.HasRoad(c))
            {
                to = c;
                break;
            }
            foreach (var next in Neighbours(c))
            {
                bool diagonal = next.X != c.X && next.Y != c.Y;
                if (!Enter(next) || (diagonal && !(Enter(new Cell(next.X, c.Y)) && Enter(new Cell(c.X, next.Y)))))
                    continue;
                // suivre une route déjà tracée coûte moins, longer une route à côté coûte plus : les routes se rejoignent au
                // lieu de courir côte à côte (deux routes voisines faisaient une « échelle »)
                double step = (diagonal ? Math.Sqrt(2) : 1) * (_map.HasRoad(next) ? 0.6 : NextToRoad(next) ? 1.6 : 1);
                double total = known + step + 0.05 * OffLine(next);
                if (cost.TryGetValue(next, out double old) && old <= total)
                    continue;
                cost[next] = total;
                previous[next] = c;
                queue.Enqueue(next, total);
            }
        }
        if (!previous.ContainsKey(to))
            return;
        for (var c = to; c != from; c = previous[c])
        {
            PaveRoad(c);
            var back = previous[c];
            // un pas en diagonale : ses deux coins restent praticables et libres (un héros ne coupe pas un coin bloqué)
            if (back.X != c.X && back.Y != c.Y)
            {
                _diagonalSteps.Add((back, c));
                foreach (var corner in new[] { new Cell(back.X, c.Y), new Cell(c.X, back.Y) })
                {
                    if (_map.GetTerrain(corner) is TerrainType.Water or TerrainType.Mountain or TerrainType.Forest)
                        _map.SetTerrain(corner, TerrainType.Grass);
                    _roadCorners.Add(corner);
                }
            }
        }
        PaveRoad(from);
    }

    /// <summary>
    /// La route d'un départ : de la case d'approche (sous l'entrée du château) au centre de la zone, en contournant les
    /// murs, pour qu'elle arrive à la porte et pas contre le flanc de la ville. Sans château, du départ lui-même.
    /// </summary>
    private void StartRoad(Cell start, Zone zone, HashSet<Cell> carved)
    {
        if (!StartingTowns)
        {
            Road(start, zone.Center, zone.Index, carved);
            return;
        }
        Road(start.Offset(0, 1), Hub(zone), zone.Index, carved);
    }

    /// <summary>Les cases des murs d'un château de départ (posé plus tard, sur chaque départ) : aucune route n'y passe.</summary>
    private bool InStartingTown(Cell c) => StartingTowns && _map.Starts.Any(s => Town.FootprintAt(s).Contains(c));

    /// <summary>
    /// Le carrefour d'une zone, où se rejoignent ses routes : son centre, sauf s'il tombe sous les murs d'un château de
    /// départ (la route s'y arrêterait) : alors la case devant la porte de ce château.
    /// </summary>
    private Cell Hub(Zone zone)
    {
        foreach (var start in _map.Starts)
            if (StartingTowns && Town.FootprintAt(start).Contains(zone.Center))
                return start.Offset(0, 1);
        return zone.Center;
    }

    /// <summary>
    /// Retire les bouts de route qui finissent contre les murs d'une ville (ailleurs que devant sa porte) : une ville posée
    /// au bout d'une route, ou à côté, ne laisse pas de route s'arrêter contre son flanc.
    /// </summary>
    private void TrimRoadsAgainst(IReadOnlyCollection<Cell> walls, Cell approach)
    {
        bool Road(Cell c) => _map.InBounds(c) && _map.HasRoad(c);
        bool trimmed = true;
        while (trimmed)
        {
            trimmed = false;
            foreach (var c in walls.SelectMany(Neighbours).Distinct().ToList())
                if (Road(c) && !walls.Contains(c) && c != approach && Neighbours(c).Count(Road) <= 1)
                {
                    _map.SetRoad(c, false);
                    trimmed = true;
                }
        }
    }

    /// <summary>
    /// Une ville posée sur un coin d'un pas en diagonale d'une route : l'autre coin devient une route, pour qu'on y passe
    /// encore (un petit coude). Les deux coins ne peuvent pas être sous la ville sans les deux bouts du pas.
    /// </summary>
    private void KeepRoadsAround(HashSet<Cell> walls)
    {
        foreach (var (a, b) in _diagonalSteps)
        {
            if (walls.Contains(a) || walls.Contains(b) || !_map.HasRoad(a) || !_map.HasRoad(b))
                continue;
            var corners = new[] { new Cell(a.X, b.Y), new Cell(b.X, a.Y) };
            if (corners.Any(walls.Contains))
                foreach (var corner in corners.Where(c => !walls.Contains(c)))
                    PaveRoad(corner);
        }
    }

    /// <summary>
    /// Pose la route sur une case. Le terrain reste (sable, marais…), sauf l'eau et la montagne, qui deviennent
    /// de l'herbe pour qu'on puisse y passer, et la forêt, pour que les arbres ne cachent pas la route.
    /// </summary>
    private void PaveRoad(Cell c)
    {
        _map.SetRoad(c, true);
        if (_map.GetTerrain(c) is TerrainType.Water or TerrainType.Mountain or TerrainType.Forest)
            _map.SetTerrain(c, TerrainType.Grass);
    }

    /// <summary>Le danger d'une case, de 0 (départ) à 1 (zone la plus lointaine).</summary>
    private double Danger(Cell cell)
    {
        if (MaxLevel > 0)
            return Math.Min(1, (double)ZoneAt(cell).Level / Math.Max(MaxLevel, _dangerScale));
        // Une seule zone : selon la distance, 1 au coin opposé de la carte
        double farthest = Math.Sqrt(_map.Width * _map.Width + _map.Height * _map.Height);
        return DistanceToStart(cell) / farthest;
    }

    /// <summary>
    /// Le danger d'un passage : un peu plus que celui de la zone la plus proche du départ qu'il relie.
    /// On franchit donc un passage avec l'armée qui a vaincu les monstres de sa zone.
    /// </summary>
    private double PassageDanger(Passage passage) =>
        Math.Min(1, (Math.Min(_zones[passage.A].Level, _zones[passage.B].Level) + 0.25) / Math.Max(1, Math.Max(MaxLevel, _dangerScale)));

    // ------------------------------------------------------------------ villes

    /// <summary>Une ville pour trois zones, et au moins une par joueur.</summary>
    private int TownCount => Math.Max(_map.Starts.Count, LandZones / 3);

    /// <summary>
    /// Des villes neutres du Château : la première dans une zone voisine du départ, les autres plus loin,
    /// avec des garnisons d'autant plus fortes. Sur une carte d'une seule zone, une ville à mi-distance.
    /// </summary>
    private void PlaceTowns(GameData data)
    {
        var names = TownNames.ToList();
        Shuffle(names);
        var more = MoreTownNames.ToList();
        var moreRng = new Random(_seed * 31 + 5); // un hasard à part : le reste de la carte ne change pas
        for (int i = more.Count - 1; i > 0; i--)
        {
            int j = moreRng.Next(i + 1);
            (more[i], more[j]) = (more[j], more[i]);
        }
        names.AddRange(more);
        // chaque joueur commence avec son château, l'entrée sur son départ (comme dans Heroes 2 et 3)
        foreach (var (start, i) in (StartingTowns ? _map.Starts : new List<Cell>()).Select((s, i) => (s, i)))
        {
            PlaceStartingTown(data, start, names[0], i < PlayerFactions.Count ? PlayerFactions[i] : DefaultFaction);
            names.RemoveAt(0);
        }
        if (_zones.Count == 1)
        {
            PlaceTownNearStart(data, names[0]);
            return;
        }

        // pour chaque joueur, une ville dans une zone voisine de son départ (une autre que celles des joueurs d'avant), de
        // préférence plus proche de lui que des autres départs : chacun a sa ville, pas une ville à partager. Si la ville ne
        // tient pas dans la zone préférée (5 × 4 cases), la suivante.
        int placed = 0;
        var used = new HashSet<Zone>();
        // sur les îles et l'archipel, une ville au plus par île (choix de l'utilisateur, 2026-11-14) : celle d'un joueur a
        // déjà son château
        bool onePerIsland = Type is MapType.Islands or MapType.Archipelago;
        var townIslands = new HashSet<int>(StartingTowns ? _map.Starts.Select(s => ZoneAt(s).Island) : Enumerable.Empty<int>());
        bool IslandFree(Zone z) => !onePerIsland || !townIslands.Contains(z.Island);
        foreach (var start in _map.Starts)
        {
            bool Own(Zone z) => _map.Starts.All(other => other == start || SquaredDistance(z.Center, start) < SquaredDistance(z.Center, other));
            // sur son île d'abord (sur un continent, toutes les zones sont sur la même)
            var choices = _zones.Where(z => z.Level == 1 && !z.Sea && !used.Contains(z))
                .OrderBy(z => z.Island == ZoneAt(start).Island ? 0 : 1)
                .ThenBy(z => Own(z) ? 0 : 1).ThenBy(z => SquaredDistance(z.Center, start)).ToList();
            // d'abord une place plus proche de lui que des autres départs, dans l'une de ces zones ; sinon n'importe où
            bool done = false;
            foreach (bool ownOnly in new[] { true, false })
                foreach (var zone in choices)
                {
                    if (done || placed == TownCount || placed == names.Count)
                        break;
                    if (!IslandFree(zone))
                        continue;
                    // seul, le joueur a sa ville au milieu de la zone voisine (comme avant les adversaires)
                    if (PlaceTownInZone(data, zone, names[placed], _map.Starts.Count == 1 ? zone.Center : start, ownOnly && _map.Starts.Count > 1))
                    {
                        used.Add(zone);
                        townIslands.Add(zone.Island);
                        placed++;
                        done = true;
                    }
                }
        }
        // les autres, au milieu de leur zone
        var far = _zones.Where(z => z.Level >= 2 && !z.Sea).OrderBy(z => z.Index).ToList();
        Shuffle(far);
        foreach (var zone in far)
        {
            if (placed == TownCount || placed == names.Count)
                break;
            if (IslandFree(zone) && PlaceTownInZone(data, zone, names[placed]))
            {
                townIslands.Add(zone.Island);
                placed++;
            }
        }
    }

    /// <summary>
    /// Le château de départ d'un joueur : l'entrée sur son départ, les murs juste au-dessus (sur l'herbe dégagée autour du
    /// départ, sans route sous les murs), la salle du village et le corps de garde, sans garnison. La partie le donne au
    /// joueur (voir <see cref="GameState"/>).
    /// </summary>
    private void PlaceStartingTown(GameData data, Cell start, string name, string faction)
    {
        foreach (var f in Town.FootprintAt(start).Append(start.Offset(0, 1)))
        {
            _map.SetTerrain(f, TerrainType.Grass);
            if (f != start && f != start.Offset(0, 1))
                _map.SetRoad(f, false);
        }
        TrimRoadsAgainst(Town.FootprintAt(start).ToList(), start.Offset(0, 1));
        KeepRoadsAround(Town.FootprintAt(start).ToHashSet());
        var town = new Town(start, name, faction, data);
        town.AddBuilding(data.Buildings.Get("salle-village"));
        if (town.DwellingOfTier(1) is { } dwelling)
            town.AddBuilding(dwelling);
        _map.AddObject(town);
        _townDistance = null; // une ville de plus
    }

    /// <summary>La faction d'une ville neutre : une des factions qu'on peut jouer, au hasard.</summary>
    private string NeutralTownFaction(GameData data)
    {
        var factions = data.Classes.PlayableFactions;
        return factions.Count == 0 ? DefaultFaction : factions[_factionRng.Next(factions.Count)];
    }

    /// <summary>
    /// Une ville neutre dans la zone, au plus près de <paramref name="towards"/> (sinon du centre de la zone) ;
    /// <paramref name="ownOnly"/> : seulement plus près de ce départ que des autres. Faux si elle n'y tient pas.
    /// </summary>
    /// <summary>L'écart voulu entre les murs de deux villes (en cases).</summary>
    private const int TownSpacing = 12;

    private bool PlaceTownInZone(GameData data, Zone zone, string name, Cell? towards = null, bool ownOnly = false)
    {
        bool Free(Cell c) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone.Index && !_border[c.X, c.Y] && !IsSea(c)
            && !_reserved.Contains(c) && _map.ObjectAt(c) == null && DistanceToStart(c) > 4;
        // au bord de la mer, la case tout autour peut être de l'eau : la ville est un port (un chantier naval s'y bâtit)
        bool Ring(Cell entrance, Cell c) => c != entrance.Offset(0, 1) && !Town.FootprintAt(entrance).Contains(c);
        bool Shore(Cell entrance) => TownMargin(entrance).Any(m => Ring(entrance, m) && IsSea(m));
        // sur une carte où l'on navigue, une ville sur deux au bord de la mer, si elle y tient
        bool port = Sailing && _rng.NextDouble() < 0.5;
        // L'entrée et ses abords (une case de plus tout autour de la ville et de sa case d'approche) restent dans la zone ;
        // de préférence, les murs de la ville ne coupent aucune route (sinon, dans une petite zone que ses routes
        // traversent de partout, la route coupée est détournée jusqu'à la porte)
        var places = AllCells()
            .Where(c => _zoneOf[c.X, c.Y] == zone.Index)
            .Where(c => TownMargin(c).All(m => Free(m) || Ring(c, m) && IsSea(m)))
            .Where(c => !ownOnly || towards is not { } own || _map.Starts.All(s => s == own || SquaredDistance(c, own) < SquaredDistance(c, s)))
            .OrderBy(c => port && !Shore(c) ? 1 : 0)
            .ThenBy(c => SquaredDistance(c, towards ?? zone.Center)).ThenBy(c => c.Y).ThenBy(c => c.X)
            .ToList();
        // de préférence loin des autres villes : chacune a la place de sa scierie et de sa mine de minerai
        var spaced = places.Where(c => Town.FootprintAt(c).Append(c.Offset(0, 1)).Min(DistanceToNearestTown) >= TownSpacing).ToList();
        if (spaced.Count > 0)
            places = spaced;
        // sur une carte où l'on navigue, la porte doit mener par la terre au centre de la zone (pas une plage enfermée entre
        // la ville et l'eau, ni un bout de zone entre la mer et ses murs)
        if (Sailing)
            places = places.Where(ReachesCenter).Take(50).ToList();
        bool ReachesCenter(Cell entrance)
        {
            var walls = Town.FootprintAt(entrance).ToHashSet();
            bool Open(Cell c) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone.Index && !IsSea(c) && !_border[c.X, c.Y]
                && !walls.Contains(c) && !InStartingTown(c);
            var seen = new HashSet<Cell> { entrance.Offset(0, 1) };
            var queue = new Queue<Cell>(seen);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                if (c == zone.Center)
                    return true;
                foreach (var n in Orthogonal(c))
                    if (Open(n) && seen.Add(n))
                        queue.Enqueue(n);
            }
            return false;
        }
        var candidates = places.Where(c => Town.FootprintAt(c).All(f => !_map.HasRoad(f))).Take(5).ToList();
        if (candidates.Count == 0)
            candidates = places.Take(5).ToList();
        if (candidates.Count == 0)
            return false;
        var cell = candidates[_rng.Next(candidates.Count)];

        var footprint = Town.FootprintAt(cell).ToHashSet();
        var approach = cell.Offset(0, 1);
        // les bouts d'une route coupée par les murs : ils rejoindront la porte en contournant la ville
        var cut = footprint.Where(_map.HasRoad).SelectMany(Neighbours)
            .Where(c => !footprint.Contains(c) && _map.InBounds(c) && _map.HasRoad(c)).Distinct().ToList();
        foreach (var f in footprint.Append(approach))
        {
            _map.SetTerrain(f, TerrainType.Grass);
            if (footprint.Contains(f))
                _map.SetRoad(f, false);
        }
        foreach (var end in cut)
            Road(end, approach, zone.Index, new HashSet<Cell>(), footprint);
        // la route part de la porte et rejoint la route la plus proche (sinon le centre de la zone)
        Road(approach, zone.Center, zone.Index, new HashSet<Cell>(), footprint, joinRoad: true);
        // sur une carte où l'on navigue, une petite zone : si la porte ne mène toujours pas à pied au centre de la zone, une
        // route tout droit jusqu'au centre
        if (Sailing && !WalksTo(approach, zone.Center, footprint))
            Road(approach, zone.Center, zone.Index, new HashSet<Cell>(), footprint);
        TrimRoadsAgainst(footprint, approach);
        KeepRoadsAround(footprint);
        AddTown(data, cell, name, zone.Level);
        return true;
    }

    /// <summary>Peut-on aller à pied de <paramref name="from"/> à <paramref name="to"/>, sans passer par <paramref name="blocked"/> ?</summary>
    private bool WalksTo(Cell from, Cell to, HashSet<Cell> blocked)
    {
        var seen = new HashSet<Cell> { from };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == to)
                return true;
            foreach (var n in Orthogonal(c))
                if ((n == to || _map.IsPassable(n) && _map.OccupantAt(n) == null) && !blocked.Contains(n) && seen.Add(n))
                    queue.Enqueue(n);
        }
        return false;
    }

    /// <summary>La ville dont l'entrée serait en <paramref name="entrance"/>, sa case d'approche, et une case tout autour.</summary>
    private static IEnumerable<Cell> TownMargin(Cell entrance)
    {
        for (int dy = -Town.Height; dy <= 1; dy++)
        for (int dx = -Town.Width / 2 - 1; dx <= Town.Width / 2 + 1; dx++)
            yield return entrance.Offset(dx, dy);
    }

    private IEnumerable<Cell> AllCells()
    {
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
            yield return new Cell(x, y);
    }

    /// <summary>Carte d'une seule zone : une ville à mi-distance du départ, comme sur les premières cartes.</summary>
    private void PlaceTownNearStart(GameData data, string name)
    {
        var start = _map.Start;
        var reachable = ReachableCells();
        // L'entrée : ses 20 cases sur un terrain où l'on peut bâtir, et la case d'approche accessible depuis le départ
        var candidates = reachable
            .Where(c => Distance(c, start) is >= 10 and <= 16)
            .Where(c => reachable.Contains(c.Offset(0, 1)))
            .Where(c => Town.FootprintAt(c).All(f => _map.IsPassable(f) && _map.ObjectAt(f) == null))
            .OrderBy(c => c.Y).ThenBy(c => c.X)
            .ToList();
        if (candidates.Count == 0)
            return;
        AddTown(data, candidates[_rng.Next(candidates.Count)], name, 1);
    }

    /// <summary>
    /// Une ville neutre d'une faction au hasard, avec une garnison de ses créatures (niveaux 1 à 4), d'autant plus forte
    /// que sa zone est lointaine. Les nombres sont ceux d'une garnison du Château, ramenés à la force des créatures de la
    /// faction (une garnison de morts-vivants vaut une garnison du Château).
    /// </summary>
    private void AddTown(GameData data, Cell cell, string name, int level)
    {
        var town = new Town(cell, name, NeutralTownFaction(data), data);
        town.AddBuilding(data.Buildings.Get("salle-village"));
        if (town.DwellingOfTier(1) is { } dwelling)
            town.AddBuilding(dwelling);
        level = Math.Max(1, level);
        var counts = new[]
        {
            (Tier: 1, Count: _rng.Next(15, 25) * level), (Tier: 2, Count: _rng.Next(6, 11) * level),
            (Tier: 3, Count: level >= 2 ? _rng.Next(4, 8) * (level - 1) : 0), (Tier: 4, Count: level >= 3 ? _rng.Next(3, 6) * (level - 2) : 0),
        };
        foreach (var (tier, count) in counts.Where(c => c.Count > 0))
        {
            var reference = data.Creatures.All.FirstOrDefault(c => c.Faction == DefaultFaction && c.Tier == tier);
            var type = data.Creatures.All.FirstOrDefault(c => c.Faction == town.Faction && c.Tier == tier);
            if (type == null || reference == null)
                continue;
            double factor = type == reference ? 1
                : CreatureStrength.GoldToBeat(reference, data.Creatures) / CreatureStrength.GoldToBeat(type, data.Creatures);
            town.Garrison.Add(type, Math.Max(1, (int)Math.Round(count * factor)));
        }
        _map.AddObject(town);
        _townDistance = null; // une ville de plus
    }

    private static double Distance(Cell a, Cell b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    /// <summary>Distance « en cases » (déplacement en diagonale compris) jusqu'à la case la plus proche d'une ville (murs compris).</summary>
    private int DistanceToNearestTown(Cell c)
    {
        _townDistance ??= TownDistances();
        return _townDistance[c.X, c.Y];
    }

    /// <summary>Les distances de <see cref="DistanceToNearestTown"/>, calculées une fois pour toute la carte (refaites après chaque ville posée).</summary>
    private int[,]? _townDistance;

    private int[,] TownDistances()
    {
        var distance = new int[_map.Width, _map.Height];
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
            distance[x, y] = int.MaxValue;
        // de proche en proche depuis toutes les cases des villes, dans les 8 directions (la distance « en cases »)
        var queue = new Queue<Cell>();
        foreach (var cell in _map.Objects.OfType<Town>().SelectMany(t => t.Footprint.Append(t.Approach)).Where(_map.InBounds))
            if (distance[cell.X, cell.Y] != 0)
            {
                distance[cell.X, cell.Y] = 0;
                queue.Enqueue(cell);
            }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var n in Neighbours(c))
                if (_map.InBounds(n) && distance[n.X, n.Y] == int.MaxValue)
                {
                    distance[n.X, n.Y] = distance[c.X, c.Y] + 1;
                    queue.Enqueue(n);
                }
        }
        return distance;
    }

    private void PaintTerrain()
    {
        int area = _map.Width * _map.Height;
        var patches = new (TerrainType Terrain, int Count, int Size)[]
        {
            (TerrainType.Forest, area / 250, 25),
            (TerrainType.Sand, area / 400, 20),
            (TerrainType.Swamp, area / 400, 18),
            (TerrainType.Water, area / 300, 30),
            (TerrainType.Mountain, area / 500, 18),
        };
        foreach (var (terrain, count, size) in patches)
            for (int i = 0; i < count; i++)
                Patch(terrain, size);
    }

    /// <summary>Pose une « tache » de terrain par marche aléatoire.</summary>
    private void Patch(TerrainType terrain, int steps)
    {
        int x = _rng.Next(_map.Width), y = _rng.Next(_map.Height);
        for (int i = 0; i < steps; i++)
        {
            for (int dx = 0; dx <= 1; dx++)
            for (int dy = 0; dy <= 1; dy++)
            {
                var c = new Cell(x + dx, y + dy);
                if (_map.InBounds(c))
                    _map.SetTerrain(c, terrain);
            }
            x = Math.Clamp(x + _rng.Next(-1, 2), 0, _map.Width - 1);
            y = Math.Clamp(y + _rng.Next(-1, 2), 0, _map.Height - 1);
        }
    }

    private void ClearStartArea()
    {
        // de l'herbe autour du départ, et au-dessus pour le château (4 cases de haut, l'entrée sur le départ)
        foreach (var start in _map.Starts)
            for (int dy = -Town.Height; dy <= 2; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                var c = start.Offset(dx, dy);
                if (_map.InBounds(c))
                    _map.SetTerrain(c, TerrainType.Grass);
            }
    }

    /// <summary>Toutes les cases accessibles à pied depuis le départ (et en bateau, sur une carte où l'on navigue).</summary>
    private HashSet<Cell> ReachableCells()
    {
        var seen = new HashSet<Cell> { _map.Start };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var next in new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) })
                if (Travelable(next) && seen.Add(next))
                    queue.Enqueue(next);
        }
        return seen;
    }

    /// <summary>
    /// Une case où l'on passe : un terrain où l'on marche, ou l'eau sur les îles et l'archipel (chaque joueur trouve un
    /// bateau sur son île, et le garde : il le laisse au bord de l'eau en débarquant). Avec quelques mers, tout s'atteint
    /// à pied : l'eau ne compte pas.
    /// </summary>
    private bool Travelable(Cell c) => _map.IsPassable(c) || Type is MapType.Islands or MapType.Archipelago && _map.IsWater(c);

    private void PlaceObjects()
    {
        var free = ReachableCells()
            .Where(_map.IsPassable) // pas sur l'eau
            .Where(c => _map.Starts.All(s => Math.Abs(c.X - s.X) + Math.Abs(c.Y - s.Y) > 2))
            .Where(c => !_map.HasRoad(c))
            .Where(c => _map.ObjectAt(c) == null && DistanceToNearestTown(c) > 1) // l'entrée de la ville reste libre
            .Where(c => !_reserved.Contains(c) && !_roadCorners.Contains(c)) // les passages et les coins des routes restent libres
            .OrderBy(c => c.Y).ThenBy(c => c.X) // ordre stable avant le mélange
            .ToList();
        Shuffle(free);
        int next = 0;

        // Sur une petite carte, on pose ce qui rentre : les mines d'abord, puis les coffres, puis les tas
        // Les mines au-delà d'une de chaque ressource, tour à tour : or, rare, bois, or, rare, minerai (le bois et le minerai
        // abondent déjà : chaque zone de départ en a) ; les rares tour à tour, les gemmes et le soufre d'abord (le cristal
        // et le mercure ont déjà une mine près des départs du Château et des morts-vivants)
        var extraPattern = new ResourceType?[] { ResourceType.Gold, null, ResourceType.Wood, ResourceType.Gold, null, ResourceType.Ore };
        var extraRare = new[] { ResourceType.Gems, ResourceType.Sulfur, ResourceType.Crystal, ResourceType.Mercury };
        ResourceType ExtraMine(int i)
        {
            if (extraPattern[i % extraPattern.Length] is { } common)
                return common;
            // la n-ième mine rare de la suite (deux par tour du motif)
            int rare = i / extraPattern.Length * 2 + (i % extraPattern.Length == 1 ? 0 : 1);
            return extraRare[rare % extraRare.Length];
        }
        IEnumerable<ResourceType> mines;
        // Un objet bloque sa case : on n'en pose pas un qui couperait le chemin vers un autre
        bool Place(Func<Cell, MapObject> make)
        {
            while (next < free.Count)
            {
                var cell = free[next++];
                if (_mineCells.Contains(cell) || _map.OccupantAt(cell) != null)
                    continue; // une case d'une mine (ses murs, la case devant son entrée)
                var obj = make(cell);
                _map.AddObject(obj);
                if (AllObjectsReachable())
                    return true;
                _map.RemoveObject(obj);
            }
            return false;
        }

        // Le trésor d'abord, enfoui dans la zone la plus dangereuse, le plus loin possible du départ (sans hasard,
        // pour ne pas changer le reste de la carte). Rien n'est posé sur sa case.
        var reachable = WalkableFromStart();
        var treasureSpot = free.Where(reachable.Contains)
            .Where(c => _zones.Count == 0 || ZoneAt(c).Level == MaxLevel)
            .OrderByDescending(c => _map.Starts.Min(s => SquaredDistance(c, s))).ThenBy(c => c.Y).ThenBy(c => c.X)
            .Cast<Cell?>().FirstOrDefault();
        if (treasureSpot is { } spot)
        {
            _map.Treasure = spot;
            free.Remove(spot);
        }

        // les cases libres au départ : une mine (6 cases, et la case devant son entrée) ne se pose que sur elles
        var freeSet = free.ToHashSet();

        // Dans la zone de départ de chaque joueur (à 4 à 11 pas), comme les cartes aléatoires de Heroes 3 : une mine de
        // bois, une de minerai et une de la ressource rare principale de sa faction (sans elle, il pourrait ne jamais bâtir
        // son habitation du niveau 7). Les cases ne dépendent pas de la faction (la carte reste la même), seulement la
        // ressource de la troisième.
        var startMines = new List<ResourceType>();
        foreach (var (start, i) in _map.Starts.Select((s, i) => (s, i)))
        {
            var main = _data?.Classes.ForFaction(i < PlayerFactions.Count ? PlayerFactions[i] : DefaultFaction)?.MainResource;
            // chaque mine vise sa propre distance, pour ne pas s'entasser
            var wanted = new List<(ResourceType Resource, int SquaredDistance)> { (ResourceType.Wood, 25), (ResourceType.Ore, 49) };
            if (main is { } rare)
                wanted.Add((rare, 36));
            foreach (var (resource, target) in wanted)
            {
                // de préférence dans sa zone à 4 à 11 pas ; sinon (une mine de 3 × 2 cases ne trouve pas toujours sa place)
                // n'importe où dans sa zone, puis au plus près hors des zones de départ des autres
                int Steps(Cell c) => Math.Max(Math.Abs(c.X - start.X), Math.Abs(c.Y - start.Y));
                bool Home(Cell c) => _zones.Count == 0 || ZoneAt(c) == ZoneAt(start);
                var otherStarts = _map.Starts.Where(o => o != start).Select(o => _zones.Count == 0 ? null : ZoneAt(o)).ToList();
                var tries = new[]
                {
                    free.Where(c => Home(c) && Steps(c) is >= 4 and <= 11).OrderBy(c => Math.Abs(SquaredDistance(c, start) - target)),
                    free.Where(c => Home(c) && Steps(c) >= 3).OrderBy(c => SquaredDistance(c, start)),
                    free.Where(c => _zones.Count == 0 || !otherStarts.Contains(ZoneAt(c))).OrderBy(c => SquaredDistance(c, start)),
                };
                foreach (var spots in tries)
                {
                    var placed = spots.ThenBy(c => c.Y).ThenBy(c => c.X).Select(c => TryPlaceMine(c, resource, freeSet)).FirstOrDefault(m => m != null);
                    if (placed == null)
                        continue;
                    startMines.Add(resource);
                    _startZoneMines.Add(placed);
                    break;
                }
            }
        }
        // Une mine de la ressource rare secondaire de chaque joueur dans une zone voisine de sa zone de départ, la plus proche
        // possible (au moins 8 pas) : gardée, c'est un but tout proche. Sur une carte d'une seule zone, à 10 à 16 pas.
        var startZones = _zones.Count == 0 ? new List<Zone>() : _map.Starts.Select(ZoneAt).Distinct().ToList();
        foreach (var (start, i) in _map.Starts.Select((s, i) => (s, i)))
        {
            if (_data?.Classes.ForFaction(i < PlayerFactions.Count ? PlayerFactions[i] : DefaultFaction)?.SecondaryResource is not { } second)
                continue;
            int home = _zones.Count == 0 ? -1 : ZoneAt(start).Index;
            var neighbours = _passages.Where(p => p.A == home || p.B == home).Select(p => p.A == home ? p.B : p.A)
                .Where(z => !startZones.Any(sz => sz.Index == z)).ToHashSet();
            var spots = free
                .Where(c => _zones.Count <= 1
                    ? Math.Max(Math.Abs(c.X - start.X), Math.Abs(c.Y - start.Y)) is >= 10 and <= 16
                    : neighbours.Contains(ZoneAt(c).Index) && Math.Max(Math.Abs(c.X - start.X), Math.Abs(c.Y - start.Y)) >= 8)
                .OrderBy(c => SquaredDistance(c, start)).ThenBy(c => c.Y).ThenBy(c => c.X).ToList();
            foreach (var cell in spots)
                if (TryPlaceMine(cell, second, freeSet) is not null)
                {
                    startMines.Add(second);
                    break;
                }
        }

        // Une scierie et une mine de minerai près de chaque ville neutre (choix de l'utilisateur, 2026-11-14), dans sa zone
        // et à 11 pas au plus de sa porte de préférence, chacune avec son gardien, une petite armée, posé aussitôt : les
        // objets posés ensuite s'arrangent autour de lui (posé à la fin, il boucherait souvent un passage étroit)
        foreach (var town in _map.Objects.OfType<Town>().Where(t => !_map.Starts.Contains(t.Position)).OrderBy(t => t.Id).ToList())
        {
            var gate = town.Approach;
            int Steps(Cell c) => Math.Max(Math.Abs(c.X - gate.X), Math.Abs(c.Y - gate.Y));
            bool Home(Cell c) => _zones.Count == 0 || ZoneAt(c) == ZoneAt(town.Position);
            foreach (var (resource, target) in new[] { (ResourceType.Wood, 36), (ResourceType.Ore, 64) })
            {
                // un abord dégagé de préférence : les deux places du gardien et la case sous celle d'où l'on entre, libres ;
                // sinon une place au moins pour son gardien (à côté de l'entrée, à 4 cases au moins d'une ville)
                bool Open(Cell c) => GuardSpotsFor(c, freeSet).Count == 2 && freeSet.Contains(c.Offset(0, 2)) && _map.IsPassable(c.Offset(0, 2));
                bool Guardable(Cell c) => GuardSpotsFor(c, freeSet).Count > 0;
                var tries = new (Func<Cell, bool> Where, Func<Cell, int> Order)[]
                {
                    (c => Home(c) && Steps(c) <= 11 && Open(c), c => Math.Abs(SquaredDistance(c, gate) - target)),
                    (c => Home(c) && Open(c), c => SquaredDistance(c, gate)),
                    // une ville à l'étroit dans sa zone : au plus près, même hors de sa zone
                    (c => Steps(c) <= 12 && Guardable(c), c => SquaredDistance(c, gate)),
                    (c => Steps(c) <= 14 && Guardable(c), c => SquaredDistance(c, gate)),
                    (c => Steps(c) <= 18 && Guardable(c), c => SquaredDistance(c, gate)),
                };
                bool done = false;
                foreach (var (where, order) in tries)
                {
                    foreach (var cell in free.Where(c => DistanceToNearestTown(c) >= 5 && where(c)).OrderBy(order).ThenBy(c => c.Y).ThenBy(c => c.X).ToList())
                    {
                        var spots = GuardSpotsFor(cell, freeSet);
                        if (TryPlaceMine(cell, resource, freeSet) is not { } mine)
                            continue;
                        if (_data != null && !spots.Any(spot => CanPlaceGuard(spot) && TryAddGuard(_data.Creatures, spot, TownMineGuardFactor)))
                        {
                            // pas de place pour son gardien : la mine va ailleurs
                            _map.RemoveObject(mine);
                            _mineCells.ExceptWith(Mine.FootprintAt(cell).Append(mine.Approach));
                            continue;
                        }
                        startMines.Add(resource);
                        _townMines.Add(mine);
                        // l'autre place de son gardien et la case sous celle d'où l'on entre restent libres : on y passe
                        foreach (var kept in spots.Append(mine.Approach.Offset(0, 1)).Where(freeSet.Contains).ToList())
                        {
                            free.Remove(kept);
                            freeSet.Remove(kept);
                            _keptFree.Add(kept);
                        }
                        done = true;
                        break;
                    }
                    if (done)
                        break;
                }
            }
        }

        if (Sailing)
        {
            PlaceHarbours(free, freeSet);
            PlaceSeaObjects(free, freeSet);
        }

        // Puis les autres : la carte prévoit une mine de chaque ressource, et d'autres tour à tour (voir ExtraMine) ; celles
        // posées près des départs en font partie (elles ne s'y ajoutent que si elles dépassent ce qui était prévu)
        var planned = ResourceInfo.All.Concat(Enumerable.Range(0, MineCount - ResourceInfo.All.Length).Select(ExtraMine)).ToList();
        foreach (var resource in startMines)
            planned.Remove(resource);
        mines = planned;
        foreach (var resource in mines)
        {
            bool placed = false;
            while (!placed && next < free.Count)
                placed = TryPlaceMine(free[next++], resource, freeSet) is not null;
            if (!placed)
                return;
        }
        for (int i = 0; i < Scaled(ChestCount); i++)
            if (!Place(c => new TreasureChest(c, _rng)))
                return;

        // L'or, le bois et le minerai sont plus fréquents que les ressources rares (deux tas sur huit, l'une des quatre)
        var pileTypes = new[] { ResourceType.Gold, ResourceType.Gold, ResourceType.Wood, ResourceType.Wood,
                                ResourceType.Ore, ResourceType.Ore, ResourceType.Gems, ResourceType.Gems };
        for (int i = 0; i < Scaled(PileCount); i++)
        {
            var type = pileTypes[_rng.Next(pileTypes.Length)];
            if (ResourceInfo.IsRare(type))
                type = ResourceInfo.Rare[_rng.Next(ResourceInfo.Rare.Length)];
            if (!Place(c => new ResourcePile(c, type, _rng)))
                return;
        }

        PlaceObelisks(free, next);
        PlaceSites(free.Skip(next).ToList());
    }

    /// <summary>Les cases de terre d'où l'on monte dans un bateau amarré : ni objet ni monstre n'y va.</summary>
    private readonly HashSet<Cell> _landings = new();

    /// <summary>
    /// Les ports d'une carte où l'on navigue : sur l'île de chaque joueur, un chantier naval de la côte et un bateau amarré
    /// à côté, hors de sa zone de départ si l'île en a une autre (on prend la mer après avoir franchi la sortie de sa zone,
    /// comme pour entrer dans la zone voisine) ; un chantier sur une île neutre sur deux. Avec quelques mers, un chantier et
    /// un bateau au bord de chaque mer, hors des zones de départ.
    /// </summary>
    private void PlaceHarbours(List<Cell> free, HashSet<Cell> freeSet)
    {
        var open = OpenSea();
        // Start : sur l'île d'un joueur, il doit les atteindre à pied (pas un bout de côte coupé)
        var harbours = new List<(List<Zone> Zones, bool Boat, Cell? Start)>();
        if (Type == MapType.Seas)
        {
            // chaque mer : les zones de terre de ses rivages
            var seen = new HashSet<Zone>();
            foreach (var sea in _zones.Where(z => z.Sea))
            {
                if (!seen.Add(sea))
                    continue;
                var waters = new List<Zone> { sea };
                var shores = new HashSet<Zone>();
                for (int i = 0; i < waters.Count; i++)
                    foreach (var cell in AllCells().Where(c => _zoneOf[c.X, c.Y] == waters[i].Index))
                        foreach (var n in Orthogonal(cell).Where(_map.InBounds).Select(ZoneAt))
                            if (!n.Sea)
                                shores.Add(n);
                            else if (seen.Add(n))
                                waters.Add(n);
                harbours.Add((shores.Where(z => z.Level >= 1).OrderBy(z => z.Level).ThenBy(z => z.Index).ToList(), true, null));
            }
        }
        else
        {
            var startIslands = _map.Starts.Select(s => ZoneAt(s).Island).ToList();
            foreach (var start in _map.Starts)
                harbours.Add((_zones.Where(z => z.Island == ZoneAt(start).Island && !z.Sea)
                    .OrderBy(z => z.Level == 0 ? 1 : 0).ThenBy(z => z.Level).ThenBy(z => z.Index).ToList(), true, start));
            // un chantier sur une île neutre sur deux (une sur trois dans l'archipel : elles sont nombreuses)
            double yards = Type == MapType.Archipelago ? 0.35 : 0.5;
            foreach (var island in _zones.Where(z => !z.Sea && !startIslands.Contains(z.Island)).GroupBy(z => z.Island).OrderBy(g => g.Key))
                if (_rng.NextDouble() < yards)
                    harbours.Add((island.OrderBy(z => z.Index).ToList(), false, null));
        }

        foreach (var (zones, boat, start) in harbours)
        {
            var yard = PlaceShipyard(zones, open, free, freeSet, start);
            if (boat)
                PlaceMooredBoat(zones, yard, open, free, freeSet, start);
        }
    }

    /// <summary>
    /// Une case d'un port (le chantier, celle d'où l'on monte dans le bateau) : rien ne se pose autour (on y passe), et le
    /// joueur de <paramref name="start"/> doit continuer de l'atteindre à pied.
    /// </summary>
    private void KeepHarbour(Cell cell, Cell? start, List<Cell> free, HashSet<Cell> freeSet)
    {
        foreach (var c in Neighbours(cell).Append(cell))
        {
            free.Remove(c);
            freeSet.Remove(c);
            if (c != cell)
                _landings.Add(c);
        }
        if (start is { } s)
            _harbourChecks.Add((s, cell));
    }

    /// <summary>
    /// Pour 1000 cases de haute mer : épaves flottantes, coffres de la mer, bouées, sirènes, paires de tourbillons, phares
    /// (étape 3 de la mer, 2026-11-14 ; chiffres à régler en jouant).
    /// </summary>
    private const double FlotsamPerSea = 4, SeaChestsPerSea = 2, BuoysPerSea = 1, MermaidsPerSea = 1, WhirlpoolPairsPerSea = 0.5,
        LighthousesPerSea = 0.7;

    /// <summary>
    /// Les objets de la mer, sur la haute mer, loin de la côte (leurs 8 cases voisines sont de l'eau : ils ne gênent ni
    /// l'embarquement ni le débarquement) et à 3 cases au moins de tout autre objet ; les deux tourbillons d'une paire à
    /// 20 cases au moins l'un de l'autre. Les phares sur la côte, hors des zones de départ.
    /// </summary>
    private void PlaceSeaObjects(List<Cell> free, HashSet<Cell> freeSet)
    {
        var open = OpenSea();
        int seaArea = AllCells().Count(c => open[c.X, c.Y]);
        int Count(double perSea) => (int)Math.Round(perSea * seaArea / 1000);
        bool Open(Cell c) => _map.InBounds(c) && open[c.X, c.Y];
        var spots = AllCells().Where(c => open[c.X, c.Y] && Neighbours(c).All(Open)).ToList();
        Shuffle(spots);
        bool Clear(Cell c) => Around(c, 2).All(n => !_map.InBounds(n) || _map.ObjectAt(n) == null && _map.OccupantAt(n) == null);
        int next = 0;
        // sur les îles, le parcours du générateur passe par la mer : un objet ne doit pas fermer un détroit
        bool Placed(MapObject obj)
        {
            _map.AddObject(obj);
            if (Type == MapType.Seas || AllObjectsReachable())
                return true;
            _map.RemoveObject(obj);
            return false;
        }
        // à terre, toujours : il ne doit couper aucun chemin
        bool OnLand(MapObject obj)
        {
            _map.AddObject(obj);
            if (AllObjectsReachable())
                return true;
            _map.RemoveObject(obj);
            return false;
        }
        bool OnWater(Func<Cell, MapObject> make)
        {
            while (next < spots.Count)
            {
                var cell = spots[next++];
                if (Clear(cell) && Placed(make(cell)))
                    return true;
            }
            return false;
        }

        for (int i = 0; i < Count(FlotsamPerSea); i++)
            OnWater(c => new Flotsam(c, _rng));
        for (int i = 0; i < Count(SeaChestsPerSea); i++)
            OnWater(c => new SeaChest(c, _rng));
        for (int i = 0; i < Count(BuoysPerSea); i++)
            OnWater(c => new BlessingSite(c, Blessing.Morale) { Sea = true });
        for (int i = 0; i < Count(MermaidsPerSea); i++)
            OnWater(c => new BlessingSite(c, Blessing.Luck) { Sea = true });
        for (int i = 0; i < Count(WhirlpoolPairsPerSea); i++)
        {
            var a = spots.Skip(next).FirstOrDefault(Clear, new Cell(-1, -1));
            var b = spots.Skip(next).FirstOrDefault(c => Clear(c) && SquaredDistance(a, c) >= 20 * 20, new Cell(-1, -1));
            if (a.X < 0 || b.X < 0)
                break;
            var first = new Whirlpool(a, b);
            if (!Placed(first))
                continue;
            if (!Placed(new Whirlpool(b, a)))
                _map.RemoveObject(first);
        }

        // les phares, sur la côte (un lieu haut : la case au-dessus de la porte est de la terre, libre)
        for (int i = 0; i < Count(LighthousesPerSea); i++)
        {
            var spot = free.Where(c => freeSet.Contains(c) && ZoneAt(c).Level >= 1 && Orthogonal(c).Any(Open)
                    && _map.IsPassable(c.Offset(0, -1)) && !_map.HasRoad(c.Offset(0, -1)) && freeSet.Contains(c.Offset(0, -1))
                    && _map.Objects.OfType<Lighthouse>().All(l => SquaredDistance(l.Position, c) >= 20 * 20))
                .Cast<Cell?>().FirstOrDefault(c => OnLand(new Lighthouse(c!.Value)));
            if (spot is not { } placed)
                break;
            foreach (var c in TallSite.FootprintAt(placed))
            {
                free.Remove(c);
                freeSet.Remove(c);
            }
            _overhang.UnionWith(TallSite.FootprintAt(placed).SelectMany(c => new[] { c.Offset(-1, 0), c.Offset(1, 0) }));
        }
    }

    /// <summary>La haute mer : l'eau d'un seul tenant d'au moins 30 cases (un bateau sur un petit lac ne mène nulle part).</summary>
    private bool[,] OpenSea()
    {
        int width = _map.Width, height = _map.Height;
        var open = new bool[width, height];
        var seen = new bool[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (seen[x, y] || !_map.IsWater(new Cell(x, y)))
                continue;
            var water = new List<Cell> { new(x, y) };
            seen[x, y] = true;
            for (int i = 0; i < water.Count; i++)
                foreach (var n in Orthogonal(water[i]))
                    if (_map.IsWater(n) && !seen[n.X, n.Y])
                    {
                        seen[n.X, n.Y] = true;
                        water.Add(n);
                    }
            if (water.Count >= 30)
                foreach (var c in water)
                    open[c.X, c.Y] = true;
        }
        return open;
    }

    /// <summary>
    /// Un chantier naval sur la côte de l'une de ces zones (dans l'ordre, au plus près de son centre), sa case de bateau
    /// sur la haute mer ; null s'il n'y tient pas.
    /// </summary>
    private Shipyard? PlaceShipyard(List<Zone> zones, bool[,] open, List<Cell> free, HashSet<Cell> freeSet, Cell? start)
    {
        bool Spot(Cell w) => _map.InBounds(w) && open[w.X, w.Y] && _map.ObjectAt(w) == null;
        var walk = start is { } s ? OnFootGrid(s) : null;
        foreach (var zone in zones)
            foreach (var cell in free.Where(c => freeSet.Contains(c) && ZoneAt(c) == zone && walk?[c.X, c.Y] != false
                             && !_mineCells.Contains(c) && _map.OccupantAt(c) == null)
                         .OrderBy(c => SquaredDistance(c, zone.Center)).ThenBy(c => c.Y).ThenBy(c => c.X).ToList())
            {
                if (Orthogonal(cell).Where(Spot).Cast<Cell?>().FirstOrDefault() is not { } spot)
                    continue;
                var yard = new Shipyard(cell, spot);
                _map.AddObject(yard);
                if (start is { } check)
                    _harbourChecks.Add((check, cell));
                if (!AllObjectsReachable())
                {
                    _map.RemoveObject(yard);
                    if (start != null)
                        _harbourChecks.RemoveAt(_harbourChecks.Count - 1);
                    continue;
                }
                KeepHarbour(cell, null, free, freeSet);
                return yard;
            }
        return null;
    }

    /// <summary>
    /// Un bateau amarré sur la haute mer, contre la côte de l'une de ces zones : au plus près du chantier naval s'il y en a
    /// un (pas sur sa case de bateau), sinon du centre de la zone. La case de terre d'où l'on y monte reste libre.
    /// </summary>
    private void PlaceMooredBoat(List<Zone> zones, Shipyard? yard, bool[,] open, List<Cell> free, HashSet<Cell> freeSet, Cell? start)
    {
        // (après le chantier : on ne monte pas à bord à travers lui)
        var walk = start is { } s ? OnFootGrid(s) : null;
        foreach (var zone in zones)
        {
            var near = yard is { } y && ZoneAt(y.Position) == zone ? y.Position : zone.Center;
            var landings = free.Where(c => freeSet.Contains(c) && ZoneAt(c) == zone && walk?[c.X, c.Y] != false && !_mineCells.Contains(c) && _map.OccupantAt(c) == null)
                .OrderBy(c => SquaredDistance(c, near)).ThenBy(c => c.Y).ThenBy(c => c.X);
            foreach (var landing in landings)
            {
                var water = Orthogonal(landing).FirstOrDefault(w => _map.InBounds(w) && open[w.X, w.Y] && _map.ObjectAt(w) == null
                    && w != yard?.BoatSpot, new Cell(-1, -1));
                if (water.X < 0)
                    continue;
                var boat = new Boat(water);
                _map.AddObject(boat);
                if (start is { } check)
                    _harbourChecks.Add((check, landing));
                if (!AllObjectsReachable())
                {
                    _map.RemoveObject(boat);
                    if (start != null)
                        _harbourChecks.RemoveAt(_harbourChecks.Count - 1);
                    continue;
                }
                _landings.Add(landing);
                KeepHarbour(landing, null, free, freeSet);
                return;
            }
        }
    }

    /// <summary>Autels (un de chaque caractéristique, tour à tour), pour une carte de 40 × 30 (plus sur une grande carte).</summary>
    public int ShrineCount { get; init; } = 4;
    public int SageCount { get; init; } = 2;
    /// <summary>Moulins et roues à eau, tour à tour.</summary>
    public int ProducerCount { get; init; } = 2;
    public int TowerCount { get; init; } = 1;

    /// <summary>Temples (+1 moral) et cercles des fées (+1 chance), tour à tour.</summary>
    public int BlessingCount { get; init; } = 2;

    /// <summary>
    /// Les lieux à visiter (après tout le reste : sur une petite carte, on pose ce qui rentre) : tours d'observation,
    /// cabanes du sage, moulins et roues à eau, autels.
    /// </summary>
    /// <summary>La distance au plus proche bord de la carte d'une tour d'observation, au moins (le quart de la carte au plus).</summary>
    public const int TowerEdgeMargin = 15;

    /// <summary>L'écart entre deux tours d'observation, au moins : chacune dévoile sa part de la carte.</summary>
    public const int TowerSpacing = ObservationTower.Radius * 3 / 2;

    private void PlaceSites(List<Cell> free)
    {
        int next = 0;
        bool Fits(MapObject obj)
        {
            var cells = obj.Footprint.ToList();
            if (cells.Any(_overhang.Contains))
                return false;
            // la case au-dessus de la porte : un terrain où l'on marche, sans route ni objet
            if (!cells.Where(c => c != obj.Position).All(c => _map.IsPassable(c) && !_map.HasRoad(c)
                    && _map.OccupantAt(c) == null && !_mineCells.Contains(c) && c != _map.Treasure && !_keptFree.Contains(c)
                    && !_landings.Contains(c)))
                return false;
            return cells.Count == 1 || Sides(cells).All(c => !_map.InBounds(c) || _map.OccupantAt(c) == null);
        }
        static IEnumerable<Cell> Sides(List<Cell> cells) => cells.SelectMany(c => new[] { c.Offset(-1, 0), c.Offset(1, 0) });
        // <where> : seulement sur ces cases ; les autres restent pour les lieux suivants
        bool Place(Func<Cell, MapObject> make, Func<Cell, bool>? where = null)
        {
            for (int j = next; j < free.Count; j++)
            {
                if (where != null && !where(free[j]))
                    continue;
                (free[j], free[next]) = (free[next], free[j]);
                var cell = free[next++];
                if (_mineCells.Contains(cell) || _map.OccupantAt(cell) != null || cell == _map.Treasure)
                    continue;
                var obj = make(cell);
                if (!Fits(obj))
                    continue;
                _map.AddObject(obj);
                if (AllObjectsReachable())
                {
                    var footprint = obj.Footprint.ToList();
                    if (footprint.Count > 1)
                        _overhang.UnionWith(Sides(footprint));
                    return true;
                }
                _map.RemoveObject(obj);
            }
            return false;
        }

        // les tours d'observation loin des bords et loin l'une de l'autre : près d'un bord, une bonne part de ce qu'elles
        // dévoilent tomberait hors de la carte ; proches, elles dévoileraient la même chose (choix de l'utilisateur,
        // 2026-11-14). Celles qui ne trouvent pas de place ne sont pas posées (les autres lieux, si).
        int margin = Math.Min(TowerEdgeMargin, Math.Min(_map.Width, _map.Height) / 4);
        bool Central(Cell c) => c.X >= margin && c.Y >= margin && c.X < _map.Width - margin && c.Y < _map.Height - margin
            && _map.Objects.OfType<ObservationTower>().All(t => SquaredDistance(t.Position, c) >= TowerSpacing * TowerSpacing);
        for (int i = 0; i < Math.Max(1, Scaled(TowerCount)); i++)
            if (!Place(c => new ObservationTower(c), Central))
                break;
        var spells = _data?.Spells.All.Where(sp => sp.Level <= 2).OrderBy(sp => sp.Id).ToList() ?? new List<SpellType>();
        if (spells.Count > 0)
            for (int i = 0; i < Scaled(SageCount); i++)
            {
                var spell = spells[_rng.Next(spells.Count)];
                if (!Place(c => new SageHut(c, spell)))
                    return;
            }
        for (int i = 0; i < Scaled(ProducerCount); i++)
        {
            var resource = i % 2 == 0 ? ResourceType.Gold : ResourceInfo.Rare[_rng.Next(ResourceInfo.Rare.Length)];
            if (!Place(c => new WeeklyProducer(c, resource)))
                return;
        }
        var stats = Enum.GetValues<HeroStat>();
        for (int i = 0; i < Scaled(ShrineCount); i++)
            if (!Place(c => new Shrine(c, stats[i % stats.Length])))
                return;
        // en dernier : les cartes d'avant gardent leurs autres lieux
        for (int i = 0; i < Scaled(BlessingCount); i++)
            if (!Place(c => new BlessingSite(c, i % 2 == 0 ? Blessing.Morale : Blessing.Luck)))
                return;
    }

    /// <summary>Nombre d'obélisques : autant de morceaux de la carte au trésor.</summary>
    public int ObeliskCount { get; init; } = 6;

    /// <summary>
    /// Les obélisques, chacun dans une zone différente si possible (des plus proches du départ aux plus lointaines),
    /// mais pas dans celle du trésor. Parmi les cases libres qui restent, sans nouveau tirage au hasard.
    /// </summary>
    private void PlaceObelisks(List<Cell> free, int next)
    {
        var remaining = free.Skip(next).ToList();
        int? treasureZone = _map.Treasure is { } t && _zones.Count > 0 ? ZoneAt(t).Index : null;
        var zones = _zones.Where(z => z.Index != treasureZone).OrderBy(z => z.Level).ThenBy(z => z.Index).Select(z => (int?)z.Index).ToList();
        if (zones.Count == 0)
            zones.Add(null);
        int placed = 0;
        // un tour par zone, puis d'autres tours s'il manque des obélisques
        for (int round = 0; round < ObeliskCount && placed < ObeliskCount; round++)
            foreach (var zone in zones)
            {
                if (placed == ObeliskCount)
                    break;
                foreach (var cell in remaining.Where(c => zone == null || ZoneAt(c).Index == zone).ToList())
                {
                    remaining.Remove(cell);
                    if (_mineCells.Contains(cell) || _map.OccupantAt(cell) != null)
                        continue;
                    var obelisk = new Obelisk(cell);
                    _map.AddObject(obelisk);
                    if (AllObjectsReachable())
                    {
                        placed++;
                        break;
                    }
                    _map.RemoveObject(obelisk);
                }
            }
    }

    /// <summary>Nombre moyen de créatures dans une troupe de monstres, selon leur niveau (1 à 7).</summary>
    internal static readonly int[] GuardBaseCount = { 0, 14, 10, 7, 5, 4, 2, 1 };

    /// <summary>Les gardiens des passages sont plus nombreux que les autres monstres de leur zone.</summary>
    public double PassageGuardFactor { get; init; } = 1.5;

    /// <summary>
    /// Un monstre dans chaque passage entre deux zones, un à côté de chaque mine et de chaque coffre,
    /// plus quelques monstres errants. Plus la zone est loin du départ, plus ils sont forts.
    /// </summary>
    private void PlaceGuards(CreatureCatalog creatures)
    {
        // Les sorties d'une zone de départ : à la portée de l'armée de départ (passages ouverts, avec des adversaires), ou
        // d'une armée de quelques semaines (normaux, fermés : on ne prend pas trop tôt la zone voisine)
        double others = Access == ZoneAccess.Closed ? 2 * PassageGuardFactor : PassageGuardFactor;
        foreach (var passage in _passages)
            if (_map.ObjectAt(passage.Gate) == null)
            {
                bool leavesStart = _zones[passage.A].Level == 0 || _zones[passage.B].Level == 0;
                var guard = AddGuard(creatures, passage.Gate, leavesStart ? 1 : others, PassageDanger(passage));
                if (leavesStart && guard != null && (Access != ZoneAccess.Open || _map.Starts.Count > 1))
                    FitToStartingArmy(creatures, guard, StartExitArmies(Access));
            }

        // les mines de la zone de départ ne sont pas gardées (comme dans Heroes 3) : elles assurent à chacun de quoi bâtir,
        // sans combat risqué dès les premiers jours
        var guarded = _map.Objects.Where(o => o is Mine or TreasureChest && !_startZoneMines.Contains(o))
            .OrderBy(o => o.Position.Y).ThenBy(o => o.Position.X)
            .ToList();
        // le plus à l'étroit d'abord (le moins de places possibles pour son gardien) : dans une zone où les objets se
        // serrent, un gardien ne prend pas la seule place d'un voisin
        while (guarded.Count > 0)
        {
            var target = guarded.MinBy(o => Neighbours(o.Position).Count(CanPlaceGuard))!;
            guarded.Remove(target);
            if (_map.IsGuarded(target.Position))
                continue; // déjà sous la garde d'un monstre voisin
            var spots = Neighbours(target.Position).ToList();
            Shuffle(spots);
            // une mine près d'une ville sans son gardien (pas de créatures au moment de la poser) : une petite armée
            double size = _townMines.Contains(target) ? TownMineGuardFactor : 1;
            foreach (var spot in spots)
            {
                if (CanPlaceGuard(spot) && TryAddGuard(creatures, spot, size))
                    break;
            }
        }

        var wandering = ReachableCells().Where(CanPlaceGuard)
            .OrderBy(c => c.Y).ThenBy(c => c.X)
            .ToList();
        Shuffle(wandering);
        int placed = 0;
        foreach (var cell in wandering)
        {
            if (placed == Scaled(WanderingGuardCount))
                break;
            if (CanPlaceGuard(cell) && TryAddGuard(creatures, cell))
                placed++;
        }
    }

    private static IEnumerable<Cell> Neighbours(Cell c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0)
                yield return c.Offset(dx, dy);
    }

    /// <summary>
    /// Une case libre, assez loin du départ et des villes, et assez loin des autres monstres
    /// pour que leurs zones de contrôle ne se chevauchent pas.
    /// </summary>
    private bool CanPlaceGuard(Cell c)
    {
        if (!_map.IsPassable(c) || _map.OccupantAt(c) != null || _reserved.Contains(c) || c == _map.Treasure || _mineCells.Contains(c)
            || _overhang.Contains(c) || _landings.Contains(c))
            return false;
        if (StepsToStart(c) < 4)
            return false;
        if (DistanceToNearestTown(c) < 4)
            return false;
        // les mines de la zone de départ restent sans gardien (pas même un monstre errant à côté)
        // (ni à côté de son entrée, ni à côté de la case d'où l'on y entre)
        if (_startZoneMines.OfType<Mine>().Any(m => new[] { m.Position, m.Approach }
                .Any(p => Math.Max(Math.Abs(p.X - c.X), Math.Abs(p.Y - c.Y)) <= 1)))
            return false;
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
            if (_map.ObjectAt(c.Offset(dx, dy)) is MonsterGuard)
                return false;
        return true;
    }

    /// <summary>Pose un monstre, sauf s'il couperait le chemin vers un objet (il bloque sa case) : renvoie false.</summary>
    private bool TryAddGuard(CreatureCatalog creatures, Cell cell, double sizeFactor = 1)
    {
        if (AddGuard(creatures, cell, sizeFactor) is not { } guard)
            return false;
        if (AllObjectsReachable())
            return true;
        _map.RemoveObject(guard);
        return false;
    }

    /// <summary>Peut-on encore atteindre tous les objets (les monstres compris) et le trésor enfoui depuis le départ ?</summary>
    private bool AllObjectsReachable()
    {
        var reachable = WalkableGrid();
        bool Reached(Cell c) => reachable[c.X, c.Y];
        // les monstres aussi : un monstre ne doit pas en enfermer un autre (on ne passe pas à travers un monstre) ; un
        // bateau, depuis la terre à côté
        // (avec des mers, le parcours reste à terre : un objet sur l'eau s'atteint en bateau, il ne coupe aucun chemin)
        return _map.Objects.All(o => Reached(o.Position) || o is Boat && Neighbours(o.Position).Any(n => _map.InBounds(n) && Reached(n))
                                     || Type == MapType.Seas && _map.IsWater(o.Position))
            && (_map.Treasure is not { } treasure || Reached(treasure))
            && _map.Starts.All(Reached)
            && _harbourChecks.GroupBy(h => h.Start).All(g => OnFootGrid(g.Key) is var walk && g.All(h => walk[h.Cell.X, h.Cell.Y]));
    }

    /// <summary>
    /// Sur les îles : chaque joueur doit atteindre à pied (en battant les monstres) son chantier naval et la case d'où l'on
    /// monte dans son bateau. Le parcours depuis le premier départ passe aussi par la mer : il ne le vérifie pas.
    /// </summary>
    private readonly List<(Cell Start, Cell Cell)> _harbourChecks = new();

    /// <summary>Les cases atteintes à pied depuis un départ, comme <see cref="WalkableGrid"/>, à travers les monstres (on les bat).</summary>
    private bool[,] OnFootGrid(Cell start)
    {
        int width = _map.Width, height = _map.Height;
        var seen = new bool[width, height];
        var from = start.Offset(0, 1); // devant la porte de son château
        if (!_map.InBounds(from))
            from = start;
        seen[from.X, from.Y] = true;
        var queue = new Queue<Cell>(new[] { from });
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c != from && _map.ObjectAt(c) is { } obj && obj is not MonsterGuard)
                continue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var n = c.Offset(dx, dy);
                if ((dx == 0 && dy == 0) || !_map.IsPassable(n) || seen[n.X, n.Y])
                    continue;
                if (dx != 0 && dy != 0 && !(_map.IsPassable(c.Offset(dx, 0)) && _map.IsPassable(c.Offset(0, dy))))
                    continue;
                seen[n.X, n.Y] = true;
                queue.Enqueue(n);
            }
        }
        return seen;
    }

    /// <summary>
    /// Les cases qu'un héros atteint depuis le départ, comme le <see cref="Pathfinder"/> : en diagonale
    /// sans couper de coin, et sans traverser d'objet (on peut seulement s'y arrêter).
    /// </summary>
    private HashSet<Cell> WalkableFromStart()
    {
        var grid = WalkableGrid();
        var seen = new HashSet<Cell>();
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
            if (grid[x, y])
                seen.Add(new Cell(x, y));
        return seen;
    }

    /// <summary>Les cases de <see cref="WalkableFromStart"/>, en tableau (bien plus rapide : on le refait après chaque objet posé).</summary>
    private bool[,] WalkableGrid()
    {
        int width = _map.Width, height = _map.Height;
        var passable = new bool[width, height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            passable[x, y] = Travelable(new Cell(x, y));
        var seen = new bool[width, height];
        var queue = new Queue<Cell>();
        seen[_map.Start.X, _map.Start.Y] = true;
        queue.Enqueue(_map.Start);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c != _map.Start && _map.ObjectAt(c) != null)
                continue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = c.X + dx, ny = c.Y + dy;
                if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= width || ny >= height || seen[nx, ny] || !passable[nx, ny])
                    continue;
                if (dx != 0 && dy != 0 && !(passable[nx, c.Y] && passable[c.X, ny]))
                    continue;
                seen[nx, ny] = true;
                queue.Enqueue(new Cell(nx, ny));
            }
        }
        return seen;
    }

    /// <summary>Les pertes que l'armée de départ peut subir en franchissant la sortie de sa zone (en valeur).</summary>
    public const double StartExitLosses = 0.35;

    /// <summary>
    /// Le gardien d'une sortie de zone de départ, à la mesure de <paramref name="armies"/> fois l'armée de départ (selon la
    /// prévision d'un combat rapide, en perdant au plus <see cref="StartExitLosses"/> de sa valeur). Une fois l'armée de
    /// départ (passages ouverts, avec des adversaires qui jouent en combats rapides) : il est seulement réduit jusqu'à ce
    /// qu'elle le batte, pour que chacun puisse sortir de son coin et prendre sa ville. Plus (passages normaux ou fermés) :
    /// il prend la plus grande taille que cette armée bat ; l'armée de départ seule ne passe pas.
    /// </summary>
    private void FitToStartingArmy(CreatureCatalog creatures, MonsterGuard guard, double armies = 1)
    {
        var army = new Army();
        foreach (var stack in GameState.StartingArmy(creatures).Stacks)
            army.Add(stack.Type, (int)Math.Round(stack.Count * armies));
        int value = Battle.ExperienceFor(army.Stacks);
        bool Beaten(int count)
        {
            var enemy = new Army();
            enemy.Add(guard.Type, count);
            var report = Battle.Resolve(army.Clone(), enemy);
            return report.AttackerWon && Battle.ExperienceFor(report.AttackerLosses) <= StartExitLosses * value;
        }
        int count = guard.Count;
        if (armies <= 1)
            while (count > 1 && !Beaten(count))
                count = Math.Max(1, count * 4 / 5);
        else
        {
            // la plus grande taille battue : on double jusqu'à ne plus gagner, puis on coupe en deux
            int low = 1, high = Math.Max(2, count);
            while (Beaten(high) && high < 100000)
            {
                low = high;
                high *= 2;
            }
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (Beaten(middle))
                    low = middle;
                else
                    high = middle;
            }
            count = low;
        }
        if (count == guard.Count)
            return;
        _map.RemoveObject(guard);
        _map.AddObject(new MonsterGuard(guard.Position, guard.Type, count));
    }

    private MonsterGuard? AddGuard(CreatureCatalog creatures, Cell cell, double sizeFactor = 1, double? dangerOverride = null)
    {
        double danger = dangerOverride ?? Danger(cell);
        int tier = Math.Clamp(1 + (int)(danger * 6), 1, MaxGuardTier);

        // S'il n'y a pas de monstre de ce niveau, on prend le niveau inférieur le plus proche
        var monsters = creatures.All.Where(c => MonsterFactions.Contains(c.Faction) && c.Tier <= tier).ToList();
        if (monsters.Count == 0)
            return null;
        tier = monsters.Max(c => c.Tier);
        var candidates = monsters.Where(c => c.Tier == tier).OrderBy(c => c.Id).ToList();
        var type = candidates[_rng.Next(candidates.Count)];
        int baseCount = GuardBaseCount[tier];
        int count = _rng.Next(baseCount, 2 * baseCount + 1);
        // Les troupes lointaines sont aussi plus nombreuses
        count = (int)Math.Round(count * (1 + GuardSizeGrowth * danger) * sizeFactor);
        (type, count) = SizeGuard(creatures, type, tier, count);
        var guard = new MonsterGuard(cell, type, count);
        _map.AddObject(guard);
        return guard;
    }

    /// <summary>
    /// Les tailles des gardiens sont réglées pour les créatures neutres d'avant les factions. Pour une autre créature, on
    /// la compare à la force de référence de son niveau (<see cref="GuardReferenceStrength"/>) par sa force mesurée
    /// (combien de paysans il faut pour en vaincre une, capacités comprises) : la troupe a autant de force que le
    /// gardien de référence. Le Troll et le Dragon rouge, encore neutres, gardent leurs tailles.
    /// </summary>
    /// <summary>
    /// La créature et la taille d'un gardien de ce niveau, à partir de sa taille comptée en gardiens neutres : une créature
    /// d'une faction a autant de force qu'un gardien neutre de ce niveau ; si une seule de ses créatures est déjà bien
    /// trop forte pour la place (<see cref="SmallestGuard"/>), le gardien prend celle du niveau en dessous de sa faction,
    /// à la même force.
    /// </summary>
    internal static (CreatureType Type, int Count) SizeGuard(CreatureCatalog creatures, CreatureType type, int tier, int count)
    {
        double sized = count * StrengthFactor(creatures, type, tier);
        if (sized < SmallestGuard && type.Faction != "neutre"
            && creatures.All.FirstOrDefault(c => c.Faction == type.Faction && c.Tier == type.Tier - 1) is { } lower)
        {
            sized = count * ReferenceStrength(creatures, tier) / CreatureStrength.GoldToBeat(lower, creatures);
            type = lower;
        }
        return (type, Math.Max(1, (int)Math.Round(sized)));
    }

    internal static double StrengthFactor(CreatureCatalog creatures, CreatureType type, int tier)
    {
        if (type.Faction == "neutre")
            return 1;
        return ReferenceStrength(creatures, tier) / CreatureStrength.GoldToBeat(type, creatures);
    }

    /// <summary>
    /// La force du gardien de référence d'un niveau, contre une vraie armée (<see cref="CreatureStrength.GoldToBeat"/>) :
    /// sa force en paysans (<see cref="GuardReferenceStrength"/>), convertie par la créature de référence du niveau.
    /// </summary>
    internal static double ReferenceStrength(CreatureCatalog creatures, int tier)
    {
        if (creatures.All.FirstOrDefault(c => c.Id == GuardReferenceCreature[tier]) is not { } reference)
            return GuardReferenceStrength[tier];
        return GuardReferenceStrength[tier] * CreatureStrength.GoldToBeat(reference, creatures)
               / CreatureStrength.PeasantsToBeat(reference, creatures);
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
