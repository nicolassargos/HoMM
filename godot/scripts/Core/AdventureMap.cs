using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>La carte d'aventure : terrain, objets et brouillard de guerre.</summary>
public sealed class AdventureMap
{
    private readonly TerrainType[,] _terrain;
    private readonly bool[,] _explored;
    private readonly Dictionary<Cell, MapObject> _objects = new();

    public AdventureMap(int width, int height)
    {
        Width = width;
        Height = height;
        _terrain = new TerrainType[width, height];
        _explored = new bool[width, height];
    }

    public int Width { get; }
    public int Height { get; }
    public Cell Start { get; set; }

    /// <summary>Augmente à chaque fois que le brouillard recule (pour savoir quand redessiner).</summary>
    public int FogVersion { get; private set; }

    public IReadOnlyCollection<MapObject> Objects => _objects.Values;

    public bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

    public TerrainType GetTerrain(Cell c) => _terrain[c.X, c.Y];
    public void SetTerrain(Cell c, TerrainType terrain) => _terrain[c.X, c.Y] = terrain;

    public int MoveCost(Cell c) => TerrainInfo.MoveCost(GetTerrain(c));
    public bool IsPassable(Cell c) => InBounds(c) && TerrainInfo.IsPassable(GetTerrain(c));

    public MapObject? ObjectAt(Cell c) => _objects.GetValueOrDefault(c);
    public void AddObject(MapObject obj) => _objects[obj.Position] = obj;
    public void RemoveObject(MapObject obj) => _objects.Remove(obj.Position);

    public bool IsExplored(Cell c) => InBounds(c) && _explored[c.X, c.Y];

    public void Reveal(Cell center, int radius)
    {
        bool changed = false;
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        for (int x = center.X - radius; x <= center.X + radius; x++)
        {
            var c = new Cell(x, y);
            int dx = x - center.X, dy = y - center.Y;
            if (InBounds(c) && !_explored[x, y] && dx * dx + dy * dy <= radius * radius + 1)
            {
                _explored[x, y] = true;
                changed = true;
            }
        }
        if (changed)
            FogVersion++;
    }
}
