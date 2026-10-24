using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// Génère une carte d'aventure aléatoire. Même graine = même carte.
/// C'est le point de départ de la génération automatique des cartes.
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

    /// <summary>Monstres placés n'importe où, en plus de ceux qui gardent les mines et les coffres.</summary>
    public int WanderingGuardCount { get; init; } = 5;

    /// <summary>
    /// Niveau maximal des monstres. Limité à 4 tant que le joueur ne peut pas recruter
    /// (étape 2 : villes), sinon les monstres lointains seraient imbattables.
    /// </summary>
    public int MaxGuardTier { get; init; } = 4;

    public int MineCount => ResourceInfo.All.Length;
    public int ChestCount { get; init; } = 6;
    public int PileCount { get; init; } = 28;

    public AdventureMap Generate(int width = 40, int height = 30)
    {
        _map = new AdventureMap(width, height) { Start = new Cell(4, 4) };
        PaintTerrain();
        ClearStartArea();
        PaintRoads();
        if (_data != null)
            PlaceTown(_data);
        PlaceObjects();
        if (_data != null)
            PlaceGuards(_data.Creatures);
        return _map;
    }

    /// <summary>
    /// Une ville neutre du Château, à mi-distance du départ : il faut la trouver,
    /// puis vaincre sa petite garnison pour la prendre.
    /// </summary>
    private void PlaceTown(GameData data)
    {
        var start = _map.Start;
        var candidates = ReachableCells()
            .Where(c => Distance(c, start) is >= 10 and <= 16)
            .OrderBy(c => c.Y).ThenBy(c => c.X)
            .ToList();
        if (candidates.Count == 0)
            return;
        var cell = candidates[_rng.Next(candidates.Count)];

        var town = new Town(cell, TownNames[_rng.Next(TownNames.Length)], "chateau", data);
        town.AddBuilding(data.Buildings.Get("salle-village"));
        town.AddBuilding(data.Buildings.Get("corps-de-garde"));
        town.Garrison.Add(data.Creatures.Get("piquier"), _rng.Next(10, 17));
        town.Garrison.Add(data.Creatures.Get("archer"), _rng.Next(4, 9));
        _map.AddObject(town);
    }

    private static double Distance(Cell a, Cell b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    /// <summary>Distance « en cases » (déplacement en diagonale compris) jusqu'à la ville la plus proche.</summary>
    private int DistanceToNearestTown(Cell c) => _map.Objects.OfType<Town>()
        .Select(t => Math.Max(Math.Abs(c.X - t.Position.X), Math.Abs(c.Y - t.Position.Y)))
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

    /// <summary>Des routes relient le départ au centre, puis le centre aux coins (ponts et cols compris).</summary>
    private void PaintRoads()
    {
        var center = new Cell(_map.Width / 2, _map.Height / 2);
        Road(_map.Start, center);
        Road(center, new Cell(_map.Width - 5, _map.Height - 5));
        Road(center, new Cell(_map.Width - 5, 4));
        Road(center, new Cell(5, _map.Height - 4));
    }

    private void Road(Cell from, Cell to)
    {
        var c = from;
        while (c != to)
        {
            _map.SetTerrain(c, TerrainType.Road);
            if (_rng.NextDouble() < 0.6)
                c = c.Offset(Math.Sign(to.X - c.X), 0);
            else
                c = c.Offset(0, Math.Sign(to.Y - c.Y));
        }
        _map.SetTerrain(c, TerrainType.Road);
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
            .Where(c => _map.GetTerrain(c) != TerrainType.Road)
            .Where(c => _map.ObjectAt(c) == null && DistanceToNearestTown(c) > 1) // l'entrée de la ville reste libre
            .OrderBy(c => c.Y).ThenBy(c => c.X) // ordre stable avant le mélange
            .ToList();
        Shuffle(free);
        int next = 0;

        // Sur une petite carte, on pose ce qui rentre : les mines d'abord, puis les coffres, puis les tas
        foreach (var resource in ResourceInfo.All)
        {
            if (next >= free.Count) return;
            _map.AddObject(new Mine(free[next++], resource));
        }
        for (int i = 0; i < ChestCount; i++)
        {
            if (next >= free.Count) return;
            _map.AddObject(new TreasureChest(free[next++], _rng));
        }

        // L'or, le bois et le minerai sont plus fréquents que les gemmes
        var pileTypes = new[] { ResourceType.Gold, ResourceType.Gold, ResourceType.Wood, ResourceType.Wood,
                                ResourceType.Ore, ResourceType.Ore, ResourceType.Gems };
        for (int i = 0; i < PileCount; i++)
        {
            if (next >= free.Count) return;
            _map.AddObject(new ResourcePile(free[next++], pileTypes[_rng.Next(pileTypes.Length)], _rng));
        }
    }

    /// <summary>Nombre moyen de créatures dans une troupe de monstres, selon leur niveau (1 à 7).</summary>
    private static readonly int[] GuardBaseCount = { 0, 14, 10, 7, 5, 4, 2, 1 };

    /// <summary>
    /// Un monstre à côté de chaque mine et de chaque coffre, plus quelques monstres errants.
    /// Plus on s'éloigne du départ, plus ils sont forts.
    /// </summary>
    private void PlaceGuards(CreatureCatalog creatures)
    {
        var guarded = _map.Objects.Where(o => o is Mine or TreasureChest)
            .OrderBy(o => o.Position.Y).ThenBy(o => o.Position.X)
            .ToList();
        foreach (var target in guarded)
        {
            var spots = Neighbours(target.Position).ToList();
            Shuffle(spots);
            foreach (var spot in spots)
            {
                if (!CanPlaceGuard(spot))
                    continue;
                AddGuard(creatures, spot);
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
            if (placed == WanderingGuardCount)
                break;
            if (!CanPlaceGuard(cell))
                continue;
            AddGuard(creatures, cell);
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
        if (!_map.IsPassable(c) || _map.ObjectAt(c) != null)
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

    private void AddGuard(CreatureCatalog creatures, Cell cell)
    {
        // 0 au départ, 1 au coin opposé de la carte
        double farthest = Math.Sqrt(_map.Width * _map.Width + _map.Height * _map.Height);
        double distance = Math.Sqrt(Math.Pow(cell.X - _map.Start.X, 2) + Math.Pow(cell.Y - _map.Start.Y, 2)) / farthest;
        int tier = Math.Clamp(1 + (int)(distance * 6), 1, MaxGuardTier);

        var candidates = creatures.All.Where(c => c.Tier == tier).OrderBy(c => c.Id).ToList();
        if (candidates.Count == 0)
            return;
        var type = candidates[_rng.Next(candidates.Count)];
        int baseCount = GuardBaseCount[tier];
        _map.AddObject(new MonsterGuard(cell, type, _rng.Next(baseCount, 2 * baseCount + 1)));
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
