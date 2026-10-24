using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>La carte d'aventure : terrain et objets. Le brouillard appartient à chaque joueur (<see cref="FogOfWar"/>).</summary>
public sealed class AdventureMap
{
    private readonly TerrainType[,] _terrain;
    private readonly Dictionary<Cell, MapObject> _objects = new();   // par case de visite
    private readonly Dictionary<Cell, MapObject> _occupied = new();  // toutes les cases couvertes

    public AdventureMap(int width, int height)
    {
        Width = width;
        Height = height;
        _terrain = new TerrainType[width, height];
    }

    public int Width { get; }
    public int Height { get; }
    public Cell Start { get; set; }

    public IReadOnlyCollection<MapObject> Objects => _objects.Values;

    public bool InBounds(Cell c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

    public TerrainType GetTerrain(Cell c) => _terrain[c.X, c.Y];
    public void SetTerrain(Cell c, TerrainType terrain) => _terrain[c.X, c.Y] = terrain;

    public int MoveCost(Cell c) => TerrainInfo.MoveCost(GetTerrain(c));

    /// <summary>
    /// Peut-on marcher sur cette case ? Non pour l'eau, la montagne, et les cases occupées
    /// par un grand objet en dehors de son entrée (les murs d'une ville).
    /// </summary>
    public bool IsPassable(Cell c) =>
        InBounds(c) && TerrainInfo.IsPassable(GetTerrain(c))
        && !(OccupantAt(c) is { } occupant && occupant.Position != c);

    /// <summary>L'objet qu'on visite sur cette case (la case d'entrée, pour une ville).</summary>
    public MapObject? ObjectAt(Cell c) => _objects.GetValueOrDefault(c);

    /// <summary>L'objet qui occupe cette case, même s'il ne se visite pas par là (les murs d'une ville).</summary>
    public MapObject? OccupantAt(Cell c) => _occupied.GetValueOrDefault(c);

    public void AddObject(MapObject obj)
    {
        _objects[obj.Position] = obj;
        foreach (var cell in obj.Footprint)
            _occupied[cell] = obj;
    }

    public void RemoveObject(MapObject obj)
    {
        _objects.Remove(obj.Position);
        foreach (var cell in obj.Footprint)
            _occupied.Remove(cell);
    }

    /// <summary>
    /// Les monstres sur les 8 cases autour de <paramref name="c"/>. Comme dans Heroes, un monstre
    /// attaque le héros qui entre dans sa « zone de contrôle ».
    /// </summary>
    public IEnumerable<MonsterGuard> GuardsAround(Cell c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if ((dx != 0 || dy != 0) && ObjectAt(c.Offset(dx, dy)) is MonsterGuard guard)
                yield return guard;
    }

    public bool IsGuarded(Cell c) => GuardsAround(c).Any();
}
