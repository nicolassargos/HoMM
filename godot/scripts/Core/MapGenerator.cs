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
    private readonly GameData? _data;
    private AdventureMap _map = null!;

    private static readonly string[] TownNames = { "Bellerive", "Hautemont", "Valombre", "Clairefont", "Roc-Fleuri", "Mirebois" };

    /// <param name="data">Les données de jeu, pour les villes et les monstres. Sans données : ni ville ni monstre.</param>
    public MapGenerator(int seed, GameData? data = null)
    {
        _rng = new Random(seed);
        _data = data;
    }

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
    public int MineCount => Math.Max(ResourceInfo.All.Length, _zones.Count);

    /// <summary>Les monstres de la carte sont des créatures neutres (les créatures des factions se recrutent en ville).</summary>
    public const string MonsterFaction = "neutre";

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

    public AdventureMap Generate(int width = 54, int height = 40)
    {
        _map = new AdventureMap(width, height) { Start = new Cell(4, 4) };
        PaintTerrain();
        MakeZones();
        ClearStartArea();
        if (_data != null)
            PlaceTowns(_data);
        PlaceObjects();
        if (_data != null)
            PlaceGuards(_data.Creatures);
        return _map;
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

    /// <summary>Cases des passages et leurs abords : ni objet, ni ville, ni autre monstre.</summary>
    private readonly HashSet<Cell> _reserved = new();

    private int MaxLevel => _zones.Count == 0 ? 0 : _zones.Max(z => z.Level);

    private Zone ZoneAt(Cell c) => _zones[_zoneOf[c.X, c.Y]];

    /// <summary>
    /// Découpe la carte en zones (les cases vont au centre de zone le plus proche), dresse des murs
    /// entre elles, ouvre des passages pour que tout soit relié, et trace les routes de chaque zone.
    /// </summary>
    private void MakeZones()
    {
        int cols = Math.Max(1, _map.Width / ZoneWidth), rows = Math.Max(1, _map.Height / ZoneHeight);
        double cellWidth = (double)_map.Width / cols, cellHeight = (double)_map.Height / rows;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            // La première zone (celle du départ) n'est pas décalée, pour que le départ soit bien à l'intérieur
            double jx = r + c == 0 ? 0 : (_rng.NextDouble() - 0.5) * cellWidth * 0.4;
            double jy = r + c == 0 ? 0 : (_rng.NextDouble() - 0.5) * cellHeight * 0.4;
            var center = new Cell((int)((c + 0.5) * cellWidth + jx), (int)((r + 0.5) * cellHeight + jy));
            _zones.Add(new Zone { Index = _zones.Count, Center = center });
        }

        _zoneOf = new int[_map.Width, _map.Height];
        _border = new bool[_map.Width, _map.Height];
        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
            _zoneOf[x, y] = _zones.OrderBy(z => SquaredDistance(z.Center, new Cell(x, y))).First().Index;

        var startZone = ZoneAt(_map.Start);
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
        ComputeLevels(links, startZone);
        PaintWalls(neighbours);

        var carved = new HashSet<Cell>();
        foreach (var (a, b) in links)
            OpenPassage(a, b, openings[(a, b)], carved);

        // Les routes : du centre de chaque zone à ses passages (et au départ)
        foreach (var zone in _zones)
        {
            foreach (var passage in _passages.Where(p => p.A == zone.Index || p.B == zone.Index))
                Road(zone.Center, PassageSide(passage, zone.Index), zone.Index, carved);
            if (zone == startZone)
                Road(_map.Start, zone.Center, zone.Index, carved);
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

    /// <summary>Le niveau de chaque zone : le nombre de passages à franchir depuis la zone de départ.</summary>
    private void ComputeLevels(List<(int A, int B)> links, Zone startZone)
    {
        startZone.Level = 0;
        var queue = new Queue<Zone>(new[] { startZone });
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
            return (double)ZoneAt(cell).Level / MaxLevel;
        // Une seule zone : selon la distance, 1 au coin opposé de la carte
        double farthest = Math.Sqrt(_map.Width * _map.Width + _map.Height * _map.Height);
        return Distance(cell, _map.Start) / farthest;
    }

    /// <summary>
    /// Le danger d'un passage : un peu plus que celui de la zone la plus proche du départ qu'il relie.
    /// On franchit donc un passage avec l'armée qui a vaincu les monstres de sa zone.
    /// </summary>
    private double PassageDanger(Passage passage) =>
        (Math.Min(_zones[passage.A].Level, _zones[passage.B].Level) + 0.25) / Math.Max(1, MaxLevel);

    // ------------------------------------------------------------------ villes

    /// <summary>Une ville pour trois zones (au moins une).</summary>
    private int TownCount => Math.Max(1, _zones.Count / 3);

    /// <summary>
    /// Des villes neutres du Château : la première dans une zone voisine du départ, les autres plus loin,
    /// avec des garnisons d'autant plus fortes. Sur une carte d'une seule zone, une ville à mi-distance.
    /// </summary>
    private void PlaceTowns(GameData data)
    {
        var names = TownNames.ToList();
        Shuffle(names);
        if (_zones.Count == 1)
        {
            PlaceTownNearStart(data, names[0]);
            return;
        }

        var near = _zones.Where(z => z.Level == 1).OrderBy(z => SquaredDistance(z.Center, _map.Start)).Take(1);
        var far = _zones.Where(z => z.Level >= 2).OrderBy(z => z.Index).ToList();
        Shuffle(far);
        int placed = 0;
        foreach (var zone in near.Concat(far))
        {
            if (placed == TownCount || placed == names.Count)
                break;
            if (PlaceTownInZone(data, zone, names[placed]))
                placed++;
        }
    }

    private bool PlaceTownInZone(GameData data, Zone zone, string name)
    {
        bool Free(Cell c) => _map.InBounds(c) && _zoneOf[c.X, c.Y] == zone.Index && !_border[c.X, c.Y]
            && !_reserved.Contains(c) && _map.ObjectAt(c) == null && Distance(c, _map.Start) > 4;
        // Les murs de la ville ne coupent aucune route ; l'entrée et ses abords restent dans la zone
        var candidates = AllCells()
            .Where(c => _zoneOf[c.X, c.Y] == zone.Index)
            .Where(c => Around(c.Offset(0, -1), 2).All(Free))
            .Where(c => Town.FootprintAt(c).All(f => !_map.HasRoad(f)))
            .OrderBy(c => SquaredDistance(c, zone.Center)).ThenBy(c => c.Y).ThenBy(c => c.X)
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
        // L'entrée : ses 9 cases sur un terrain où l'on peut bâtir, et la case d'approche accessible depuis le départ
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

    /// <summary>Une ville neutre, avec une garnison d'autant plus forte que sa zone est lointaine (niveau 1 et plus).</summary>
    private void AddTown(GameData data, Cell cell, string name, int level)
    {
        var town = new Town(cell, name, "chateau", data);
        town.AddBuilding(data.Buildings.Get("salle-village"));
        town.AddBuilding(data.Buildings.Get("corps-de-garde"));
        level = Math.Max(1, level);
        town.Garrison.Add(data.Creatures.Get("paysan"), _rng.Next(15, 25) * level);
        town.Garrison.Add(data.Creatures.Get("archer"), _rng.Next(6, 11) * level);
        if (level >= 2)
            town.Garrison.Add(data.Creatures.Get("griffon"), _rng.Next(4, 8) * (level - 1));
        if (level >= 3)
            town.Garrison.Add(data.Creatures.Get("epeiste"), _rng.Next(3, 6) * (level - 2));
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
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            var c = _map.Start.Offset(dx, dy);
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
        var start = _map.Start;
        var free = ReachableCells()
            .Where(c => Math.Abs(c.X - start.X) + Math.Abs(c.Y - start.Y) > 2)
            .Where(c => !_map.HasRoad(c))
            .Where(c => _map.ObjectAt(c) == null && DistanceToNearestTown(c) > 1) // l'entrée de la ville reste libre
            .Where(c => !_reserved.Contains(c)) // les passages restent libres
            .OrderBy(c => c.Y).ThenBy(c => c.X) // ordre stable avant le mélange
            .ToList();
        Shuffle(free);
        int next = 0;

        // Sur une petite carte, on pose ce qui rentre : les mines d'abord, puis les coffres, puis les tas
        // Une de chaque ressource, puis surtout du bois et du minerai (pour bâtir dans plusieurs villes)
        var extraMines = new[] { ResourceType.Wood, ResourceType.Ore, ResourceType.Gold };
        var mines = ResourceInfo.All.Concat(Enumerable.Range(0, MineCount - ResourceInfo.All.Length).Select(i => extraMines[i % extraMines.Length]));
        // Un objet bloque sa case : on n'en pose pas un qui couperait le chemin vers un autre
        bool Place(Func<Cell, MapObject> make)
        {
            while (next < free.Count)
            {
                var obj = make(free[next++]);
                _map.AddObject(obj);
                if (AllObjectsReachable())
                    return true;
                _map.RemoveObject(obj);
            }
            return false;
        }

        foreach (var resource in mines)
            if (!Place(c => new Mine(c, resource)))
                return;
        for (int i = 0; i < Scaled(ChestCount); i++)
            if (!Place(c => new TreasureChest(c, _rng)))
                return;

        // L'or, le bois et le minerai sont plus fréquents que les gemmes
        var pileTypes = new[] { ResourceType.Gold, ResourceType.Gold, ResourceType.Wood, ResourceType.Wood,
                                ResourceType.Ore, ResourceType.Ore, ResourceType.Gems };
        for (int i = 0; i < Scaled(PileCount); i++)
            if (!Place(c => new ResourcePile(c, pileTypes[_rng.Next(pileTypes.Length)], _rng)))
                return;
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
                AddGuard(creatures, passage.Gate, leavesStart ? 1 : PassageGuardFactor, PassageDanger(passage));
            }

        var guarded = _map.Objects.Where(o => o is Mine or TreasureChest)
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
        if (!_map.IsPassable(c) || _map.ObjectAt(c) != null || _reserved.Contains(c))
            return false;
        if (Math.Max(Math.Abs(c.X - _map.Start.X), Math.Abs(c.Y - _map.Start.Y)) < 4)
            return false;
        if (DistanceToNearestTown(c) < 4)
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

    /// <summary>Peut-on encore atteindre tous les objets (sauf les monstres) depuis le départ ?</summary>
    private bool AllObjectsReachable()
    {
        var reachable = WalkableFromStart();
        return _map.Objects.All(o => o is MonsterGuard || reachable.Contains(o.Position));
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

    private MonsterGuard? AddGuard(CreatureCatalog creatures, Cell cell, double sizeFactor = 1, double? dangerOverride = null)
    {
        double danger = dangerOverride ?? Danger(cell);
        int tier = Math.Clamp(1 + (int)(danger * 6), 1, MaxGuardTier);

        // S'il n'y a pas de monstre de ce niveau, on prend le niveau inférieur le plus proche
        var monsters = creatures.All.Where(c => c.Faction == MonsterFaction && c.Tier <= tier).ToList();
        if (monsters.Count == 0)
            return null;
        tier = monsters.Max(c => c.Tier);
        var candidates = monsters.Where(c => c.Tier == tier).OrderBy(c => c.Id).ToList();
        var type = candidates[_rng.Next(candidates.Count)];
        int baseCount = GuardBaseCount[tier];
        int count = _rng.Next(baseCount, 2 * baseCount + 1);
        // Les troupes lointaines sont aussi plus nombreuses
        count = (int)Math.Round(count * (1 + GuardSizeGrowth * danger) * sizeFactor);
        var guard = new MonsterGuard(cell, type, count);
        _map.AddObject(guard);
        return guard;
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
