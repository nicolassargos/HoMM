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
    /// <summary>Le hasard de la faction des villes neutres, à part : il ne change pas le reste de la carte.</summary>
    private readonly Random _factionRng;
    private readonly GameData? _data;
    private AdventureMap _map = null!;

    private static readonly string[] TownNames =
    {
        "Bellerive", "Hautemont", "Valombre", "Clairefont", "Roc-Fleuri", "Mirebois",
        "Pierrelune", "Bois-Doré", "Montclair", "Sombreval", "Aiguerive", "Combelaine", "Brunehaut", "Fontargent",
    };

    /// <param name="data">Les données de jeu, pour les villes et les monstres. Sans données : ni ville ni monstre.</param>
    public MapGenerator(int seed, GameData? data = null)
    {
        _rng = new Random(seed);
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
    public int MineCount => Math.Max(ResourceInfo.All.Length, (int)Math.Round(_zones.Count * MinesPerZone) + ResourceInfo.Rare.Length - 1);

    /// <summary>Mines par zone (la carte paraissait vide avec une seule ; Heroes 3 en met souvent 2 à 4).</summary>
    public double MinesPerZone { get; init; } = 1.5;

    /// <summary>
    /// Les factions dont viennent les monstres de la carte : les neutres, et les morts-vivants (étape 9, en attendant
    /// qu'ils aient leur ville : on les rencontre déjà sur la carte).
    /// </summary>
    public static readonly string[] MonsterFactions = { "neutre", "morts-vivants" };

    /// <summary>Coffres et tas de ressources, pour une carte de 40 × 30 (plus sur une grande carte).</summary>
    public int ChestCount { get; init; } = 6;
    public int PileCount { get; init; } = 28;

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

    /// <summary>Les départs : à 4 cases des bords, dans l'ordre des joueurs.</summary>
    private static IEnumerable<Cell> StartCells(int width, int height, int players) =>
        new[] { new Cell(4, 4), new Cell(width - 5, height - 5), new Cell(width - 5, 4), new Cell(4, height - 5) }.Take(players);

    /// <summary>La distance (à vol d'oiseau) jusqu'au départ le plus proche.</summary>
    private double DistanceToStart(Cell c) => _map.Starts.Min(s => Distance(c, s));

    /// <summary>La distance en cases (diagonales comprises) jusqu'au départ le plus proche.</summary>
    private int StepsToStart(Cell c) => _map.Starts.Min(s => Math.Max(Math.Abs(c.X - s.X), Math.Abs(c.Y - s.Y)));

    public AdventureMap Generate(int width = 54, int height = 40)
    {
        _map = new AdventureMap(width, height);
        _map.Starts.AddRange(StartCells(width, height, Math.Clamp(Players, 1, 4)));
        PaintTerrain();
        MakeZones();
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
                if (!_map.IsPassable(next) || seen.Contains(next))
                    continue;
                // pas de diagonale qui coupe un coin infranchissable (comme le Pathfinder)
                if (dx != 0 && dy != 0 && !(_map.IsPassable(cell.Offset(dx, 0)) && _map.IsPassable(cell.Offset(0, dy))))
                    continue;
                seen.Add(next);
                queue.Enqueue(next);
            }
        }
        foreach (var guard in _map.Objects.OfType<MonsterGuard>().Where(g => !seen.Contains(g.Position)).ToList())
            _map.RemoveObject(guard);
    }

    /// <summary>Nombre d'objets à poser pour cette carte, à partir du nombre prévu pour la carte de référence.</summary>
    private int Scaled(int count) => (int)Math.Round((double)count * _map.Width * _map.Height / ReferenceArea);

    // ------------------------------------------------------------------- zones

    /// <summary>
    /// Une zone de la carte. Les zones sont séparées par des montagnes ou des rivières,
    /// et reliées par des passages gardés. <see cref="Level"/> : nombre de passages à franchir depuis le départ.
    /// </summary>
    private sealed class Zone
    {
        public int Index { get; init; }
        public Cell Center { get; init; }
        public int Level { get; set; } = -1;
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

    /// <summary>Cases des passages et leurs abords : ni objet, ni ville, ni autre monstre.</summary>
    private readonly HashSet<Cell> _reserved = new();

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
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
            _zoneOf[x, y] = _zones.OrderBy(z => SquaredDistance(z.Center, new Cell(x, y))).First().Index;

        var startZone = ZoneAt(_map.Start);
        var startZones = _map.Starts.Select(ZoneAt).Distinct().ToList();
        if (_zones.Count == 1)
        {
            startZone.Level = 0;
            Road(_map.Start, startZone.Center, startZone.Index, new HashSet<Cell>());
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

        // On ne relie que des zones voisines où un passage peut s'ouvrir
        var openings = neighbours.ToDictionary(n => n, n => PassageCandidates(n.A, n.B));
        var links = ChooseLinks(neighbours.Where(n => openings[n].Count > 0).ToList(), startZone.Index);
        // l'échelle du danger, comme si le joueur était seul ; puis les vrais niveaux, depuis le départ le plus proche
        ComputeLevels(links, new List<Zone> { startZone });
        _dangerScale = MaxLevel;
        foreach (var zone in _zones)
            zone.Level = -1;
        ComputeLevels(links, startZones);
        PaintWalls(neighbours);

        var carved = new HashSet<Cell>();
        foreach (var (a, b) in links)
            OpenPassage(a, b, openings[(a, b)], carved);

        // Les routes : du centre de chaque zone à ses passages (et au départ)
        foreach (var zone in _zones)
        {
            foreach (var passage in _passages.Where(p => p.A == zone.Index || p.B == zone.Index))
                Road(zone.Center, PassageSide(passage, zone.Index), zone.Index, carved);
            foreach (var start in _map.Starts.Where(s => ZoneAt(s) == zone))
                Road(start, zone.Center, zone.Index, carved);
        }
    }

    private static int SquaredDistance(Cell a, Cell b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

    private static IEnumerable<Cell> Orthogonal(Cell c) =>
        new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) };

    /// <summary>Les passages à ouvrir : de quoi relier toutes les zones, plus quelques-uns au hasard.</summary>
    private List<(int A, int B)> ChooseLinks(List<(int A, int B)> neighbours, int startZone)
    {
        var links = new List<(int, int)>();
        var connected = new HashSet<int> { startZone };
        while (connected.Count < _zones.Count)
        {
            var frontier = neighbours.Where(n => connected.Contains(n.A) != connected.Contains(n.B)).ToList();
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
        var wallOf = neighbours.ToDictionary(n => n, _ => _rng.NextDouble() < 0.6 ? TerrainType.Mountain : TerrainType.Water);
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
        {
            if (!_border[x, y])
                continue;
            var cell = new Cell(x, y);
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
        bool InZones(Cell c) => _map.InBounds(c) && (_zoneOf[c.X, c.Y] == a || _zoneOf[c.X, c.Y] == b);
        bool Inside(Cell c, int zone) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone && !_border[c.X, c.Y];
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
    /// sans passer par ses murs (sauf les passages ouverts) ni par les cases interdites.
    /// </summary>
    private void Road(Cell from, Cell to, int zone, HashSet<Cell> carved, HashSet<Cell>? forbidden = null)
    {
        bool Allowed(Cell c) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone
            && (!_border[c.X, c.Y] || carved.Contains(c)) && forbidden?.Contains(c) != true;

        var previous = new Dictionary<Cell, Cell> { [from] = from };
        var queue = new Queue<Cell>(new[] { from });
        while (queue.Count > 0 && !previous.ContainsKey(to))
        {
            var c = queue.Dequeue();
            foreach (var next in Orthogonal(c))
                if ((Allowed(next) || next == to) && previous.TryAdd(next, c))
                    queue.Enqueue(next);
        }
        if (!previous.ContainsKey(to))
            return;
        for (var c = to; c != from; c = previous[c])
            PaveRoad(c);
        PaveRoad(from);
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
    private int TownCount => Math.Max(_map.Starts.Count, _zones.Count / 3);

    /// <summary>
    /// Des villes neutres du Château : la première dans une zone voisine du départ, les autres plus loin,
    /// avec des garnisons d'autant plus fortes. Sur une carte d'une seule zone, une ville à mi-distance.
    /// </summary>
    private void PlaceTowns(GameData data)
    {
        var names = TownNames.ToList();
        Shuffle(names);
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
        foreach (var start in _map.Starts)
        {
            bool Own(Zone z) => _map.Starts.All(other => other == start || SquaredDistance(z.Center, start) < SquaredDistance(z.Center, other));
            var choices = _zones.Where(z => z.Level == 1 && !used.Contains(z))
                .OrderBy(z => Own(z) ? 0 : 1).ThenBy(z => SquaredDistance(z.Center, start)).ToList();
            // d'abord une place plus proche de lui que des autres départs, dans l'une de ces zones ; sinon n'importe où
            bool done = false;
            foreach (bool ownOnly in new[] { true, false })
                foreach (var zone in choices)
                {
                    if (done || placed == TownCount || placed == names.Count)
                        break;
                    // seul, le joueur a sa ville au milieu de la zone voisine (comme avant les adversaires)
                    if (PlaceTownInZone(data, zone, names[placed], _map.Starts.Count == 1 ? zone.Center : start, ownOnly && _map.Starts.Count > 1))
                    {
                        used.Add(zone);
                        placed++;
                        done = true;
                    }
                }
        }
        // les autres, au milieu de leur zone
        var far = _zones.Where(z => z.Level >= 2).OrderBy(z => z.Index).ToList();
        Shuffle(far);
        foreach (var zone in far)
        {
            if (placed == TownCount || placed == names.Count)
                break;
            if (PlaceTownInZone(data, zone, names[placed]))
                placed++;
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
        var town = new Town(start, name, faction, data);
        town.AddBuilding(data.Buildings.Get("salle-village"));
        if (town.DwellingOfTier(1) is { } dwelling)
            town.AddBuilding(dwelling);
        _map.AddObject(town);
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
    private bool PlaceTownInZone(GameData data, Zone zone, string name, Cell? towards = null, bool ownOnly = false)
    {
        bool Free(Cell c) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone.Index && !_border[c.X, c.Y]
            && !_reserved.Contains(c) && _map.ObjectAt(c) == null && DistanceToStart(c) > 4;
        // Les murs de la ville ne coupent aucune route ; l'entrée et ses abords (une case de plus tout autour de la ville et
        // de sa case d'approche) restent dans la zone
        var candidates = AllCells()
            .Where(c => _zoneOf[c.X, c.Y] == zone.Index)
            .Where(c => TownMargin(c).All(Free))
            .Where(c => !ownOnly || towards is not { } own || _map.Starts.All(s => s == own || SquaredDistance(c, own) < SquaredDistance(c, s)))
            .Where(c => Town.FootprintAt(c).All(f => !_map.HasRoad(f)))
            .OrderBy(c => SquaredDistance(c, towards ?? zone.Center)).ThenBy(c => c.Y).ThenBy(c => c.X)
            .Take(5).ToList();
        if (candidates.Count == 0)
            return false;
        var cell = candidates[_rng.Next(candidates.Count)];

        var footprint = Town.FootprintAt(cell).ToHashSet();
        foreach (var f in footprint.Append(cell.Offset(0, 1)))
            _map.SetTerrain(f, TerrainType.Grass);
        Road(cell.Offset(0, 1), zone.Center, zone.Index, new HashSet<Cell>(), footprint);
        AddTown(data, cell, name, zone.Level);
        return true;
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
                : (double)CreatureStrength.PeasantsToBeat(reference, data.Creatures) / CreatureStrength.PeasantsToBeat(type, data.Creatures);
            town.Garrison.Add(type, Math.Max(1, (int)Math.Round(count * factor)));
        }
        _map.AddObject(town);
    }

    private static double Distance(Cell a, Cell b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    /// <summary>Distance « en cases » (déplacement en diagonale compris) jusqu'à la case la plus proche d'une ville (murs compris).</summary>
    private int DistanceToNearestTown(Cell c) => _map.Objects.OfType<Town>()
        .SelectMany(t => t.Footprint.Append(t.Approach))
        .Select(f => Math.Max(Math.Abs(c.X - f.X), Math.Abs(c.Y - f.Y)))
        .DefaultIfEmpty(int.MaxValue).Min();

    private void PaintTerrain()
    {
        int area = _map.Width * _map.Height;
        var patches = new (TerrainType Terrain, int Count, int Size)[]
        {
            (TerrainType.Forest, area / 120, 25),
            (TerrainType.Sand, area / 400, 20),
            (TerrainType.Swamp, area / 400, 18),
            (TerrainType.Water, area / 300, 30),
            (TerrainType.Mountain, area / 200, 18),
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

    /// <summary>Toutes les cases accessibles à pied depuis le départ.</summary>
    private HashSet<Cell> ReachableCells()
    {
        var seen = new HashSet<Cell> { _map.Start };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var next in new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) })
                if (_map.IsPassable(next) && seen.Add(next))
                    queue.Enqueue(next);
        }
        return seen;
    }

    private void PlaceObjects()
    {
        var free = ReachableCells()
            .Where(c => _map.Starts.All(s => Math.Abs(c.X - s.X) + Math.Abs(c.Y - s.Y) > 2))
            .Where(c => !_map.HasRoad(c))
            .Where(c => _map.ObjectAt(c) == null && DistanceToNearestTown(c) > 1) // l'entrée de la ville reste libre
            .Where(c => !_reserved.Contains(c)) // les passages restent libres
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
    private static readonly int[] GuardBaseCount = { 0, 14, 10, 7, 5, 4, 2, 1 };

    /// <summary>Les gardiens des passages sont plus nombreux que les autres monstres de leur zone.</summary>
    public double PassageGuardFactor { get; init; } = 1.5;

    /// <summary>
    /// Un monstre dans chaque passage entre deux zones, un à côté de chaque mine et de chaque coffre,
    /// plus quelques monstres errants. Plus la zone est loin du départ, plus ils sont forts.
    /// </summary>
    private void PlaceGuards(CreatureCatalog creatures)
    {
        // Il n'y a pas de ville dans la zone de départ : ses sorties restent à la portée de l'armée de départ
        foreach (var passage in _passages)
            if (_map.ObjectAt(passage.Gate) == null)
            {
                bool leavesStart = _zones[passage.A].Level == 0 || _zones[passage.B].Level == 0;
                var guard = AddGuard(creatures, passage.Gate, leavesStart ? 1 : PassageGuardFactor, PassageDanger(passage));
                if (leavesStart && guard != null && _map.Starts.Count > 1)
                    FitToStartingArmy(creatures, guard);
            }

        // les mines de la zone de départ ne sont pas gardées (comme dans Heroes 3) : elles assurent à chacun de quoi bâtir,
        // sans combat risqué dès les premiers jours
        var guarded = _map.Objects.Where(o => o is Mine or TreasureChest && !_startZoneMines.Contains(o))
            .OrderBy(o => o.Position.Y).ThenBy(o => o.Position.X)
            .ToList();
        foreach (var target in guarded)
        {
            var spots = Neighbours(target.Position).ToList();
            Shuffle(spots);
            foreach (var spot in spots)
            {
                if (CanPlaceGuard(spot) && TryAddGuard(creatures, spot))
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
        if (!_map.IsPassable(c) || _map.OccupantAt(c) != null || _reserved.Contains(c) || c == _map.Treasure || _mineCells.Contains(c))
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
    private bool TryAddGuard(CreatureCatalog creatures, Cell cell)
    {
        if (AddGuard(creatures, cell) is not { } guard)
            return false;
        if (AllObjectsReachable())
            return true;
        _map.RemoveObject(guard);
        return false;
    }

    /// <summary>Peut-on encore atteindre tous les objets (sauf les monstres) et le trésor enfoui depuis le départ ?</summary>
    private bool AllObjectsReachable()
    {
        var reachable = WalkableFromStart();
        return _map.Objects.All(o => o is MonsterGuard || reachable.Contains(o.Position))
            && (_map.Treasure is not { } treasure || reachable.Contains(treasure))
            && _map.Starts.All(reachable.Contains);
    }

    /// <summary>
    /// Les cases qu'un héros atteint depuis le départ, comme le <see cref="Pathfinder"/> : en diagonale
    /// sans couper de coin, et sans traverser d'objet (on peut seulement s'y arrêter).
    /// </summary>
    private HashSet<Cell> WalkableFromStart()
    {
        var seen = new HashSet<Cell> { _map.Start };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c != _map.Start && _map.ObjectAt(c) != null)
                continue;
            foreach (var next in Neighbours(c))
            {
                int dx = next.X - c.X, dy = next.Y - c.Y;
                if (!_map.IsPassable(next))
                    continue;
                if (dx != 0 && dy != 0 && !(_map.IsPassable(c.Offset(dx, 0)) && _map.IsPassable(c.Offset(0, dy))))
                    continue;
                if (seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }

    /// <summary>Les pertes que l'armée de départ peut subir en franchissant la sortie de sa zone (en valeur).</summary>
    private const double StartExitLosses = 0.35;

    /// <summary>
    /// Avec des adversaires (chacun joue en combats rapides) : le gardien d'une sortie de zone de départ est réduit jusqu'à
    /// ce que l'armée de départ le batte en perdant au plus <see cref="StartExitLosses"/> de sa valeur (selon la prévision),
    /// pour que chacun puisse sortir de son coin et prendre sa ville.
    /// </summary>
    private void FitToStartingArmy(CreatureCatalog creatures, MonsterGuard guard)
    {
        var army = GameState.StartingArmy(creatures);
        int value = Battle.ExperienceFor(army.Stacks);
        int count = guard.Count;
        while (count > 1)
        {
            var enemy = new Army();
            enemy.Add(guard.Type, count);
            var report = Battle.Resolve(army.Clone(), enemy);
            if (report.AttackerWon && Battle.ExperienceFor(report.AttackerLosses) <= StartExitLosses * value)
                break;
            count = Math.Max(1, count * 4 / 5);
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
        // une créature d'une faction (les morts-vivants) : autant de force qu'un gardien neutre de ce niveau
        count = Math.Max(1, (int)Math.Round(count * StrengthFactor(creatures, type, tier)));
        var guard = new MonsterGuard(cell, type, count);
        _map.AddObject(guard);
        return guard;
    }

    /// <summary>
    /// Les tailles des gardiens sont réglées pour les créatures neutres. Pour une créature d'une autre faction, on la
    /// compare à la créature neutre du même niveau (ou du niveau en dessous) par sa force mesurée (combien de paysans il
    /// faut pour en vaincre une, capacités comprises) : la troupe a autant de force que le gardien neutre.
    /// </summary>
    private static double StrengthFactor(CreatureCatalog creatures, CreatureType type, int tier)
    {
        if (type.Faction == "neutre")
            return 1;
        var reference = creatures.All.Where(c => c.Faction == "neutre" && c.Tier <= tier).OrderByDescending(c => c.Tier).FirstOrDefault();
        if (reference == null)
            return 1;
        return (double)CreatureStrength.PeasantsToBeat(reference, creatures) / CreatureStrength.PeasantsToBeat(type, creatures);
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
