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
    private AdventureMap _map = null!;

    public MapGenerator(int seed) => _rng = new Random(seed);

    public int MineCount => ResourceInfo.All.Length;
    public int ChestCount { get; init; } = 6;
    public int PileCount { get; init; } = 28;

    public AdventureMap Generate(int width = 40, int height = 30)
    {
        _map = new AdventureMap(width, height) { Start = new Cell(4, 4) };
        PaintTerrain();
        ClearStartArea();
        PaintRoads();
        PlaceObjects();
        return _map;
    }

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
            .OrderBy(c => c.Y).ThenBy(c => c.X) // ordre stable avant le mélange
            .ToList();
        Shuffle(free);
        int next = 0;

        foreach (var resource in ResourceInfo.All)
            _map.AddObject(new Mine(free[next++], resource));
        for (int i = 0; i < ChestCount; i++)
            _map.AddObject(new TreasureChest(free[next++], _rng));

        // L'or, le bois et le minerai sont plus fréquents que les gemmes
        var pileTypes = new[] { ResourceType.Gold, ResourceType.Gold, ResourceType.Wood, ResourceType.Wood,
                                ResourceType.Ore, ResourceType.Ore, ResourceType.Gems };
        for (int i = 0; i < PileCount; i++)
            _map.AddObject(new ResourcePile(free[next++], pileTypes[_rng.Next(pileTypes.Length)], _rng));
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
