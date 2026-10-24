namespace HoMM.Core;

/// <summary>Une case de la carte, repérée par sa colonne (X) et sa ligne (Y).</summary>
public readonly record struct Cell(int X, int Y)
{
    public Cell Offset(int dx, int dy) => new(X + dx, Y + dy);
    public override string ToString() => $"({X}, {Y})";
}

public enum TerrainType { Grass, Road, Forest, Sand, Swamp, Water, Mountain }

public static class TerrainInfo
{
    /// <summary>Coût pour quitter une case de ce terrain (100 = une case d'herbe, comme dans Heroes).</summary>
    public static int MoveCost(TerrainType terrain) => terrain switch
    {
        TerrainType.Road => 75,
        TerrainType.Grass => 100,
        TerrainType.Forest => 150,
        TerrainType.Sand => 150,
        TerrainType.Swamp => 175,
        _ => 0,
    };

    public static bool IsPassable(TerrainType terrain) =>
        terrain is not (TerrainType.Water or TerrainType.Mountain);

    public static string Name(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => "Herbe",
        TerrainType.Road => "Route",
        TerrainType.Forest => "Forêt",
        TerrainType.Sand => "Sable",
        TerrainType.Swamp => "Marais",
        TerrainType.Water => "Eau",
        TerrainType.Mountain => "Montagne",
        _ => terrain.ToString(),
    };
}
