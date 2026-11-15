namespace HoMM.Core;

/// <summary>Une case de la carte, repérée par sa colonne (X) et sa ligne (Y).</summary>
public readonly record struct Cell(int X, int Y)
{
    public Cell Offset(int dx, int dy) => new(X + dx, Y + dy);
    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// Le terrain d'une case. Les numéros sont fixes : ils sont enregistrés dans les sauvegardes. Le 1 était la route,
/// qui est maintenant une couche posée sur le terrain (<see cref="AdventureMap.HasRoad"/>) ; il n'est plus utilisé.
/// </summary>
public enum TerrainType { Grass = 0, Forest = 2, Sand = 3, Swamp = 4, Water = 5, Mountain = 6 }

/// <summary>
/// La rivière d'une case, posée sur le terrain comme la route (2026-11-15, choix de l'utilisateur). Les numéros sont
/// enregistrés dans les sauvegardes. Un ruisseau se traverse à pied (un simple décor, comme les rivières de Heroes 3) ;
/// une rivière (entre deux zones) bloque le passage, sauf sur un pont.
/// </summary>
public enum RiverKind { None = 0, Stream = 1, River = 2, Bridge = 3 }

/// <summary>
/// La sorte d'une route (2026-11-15, choix de l'utilisateur, comme Heroes 3) : terre, gravier ou pavés, une par zone. Elle ne
/// change que l'image ; les numéros sont enregistrés dans les sauvegardes.
/// </summary>
public enum RoadKind { None = 0, Dirt = 1, Gravel = 2, Cobbles = 3 }

public static class TerrainInfo
{
    /// <summary>Coût pour quitter une case de route, quel que soit le terrain dessous.</summary>
    public const int RoadCost = 75;

    /// <summary>Coût pour quitter une case de ce terrain sans route (100 = une case d'herbe, comme dans Heroes).</summary>
    public static int MoveCost(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => 100,
        // en bateau (la mer : la vitesse ordinaire du héros, comme Heroes 3)
        TerrainType.Water => 100,
        TerrainType.Sand => 150,
        TerrainType.Swamp => 175,
        _ => 0,
    };

    /// <summary>
    /// L'eau, la montagne et la forêt bloquent le passage : comme dans Heroes 1 à 3, les arbres sont des obstacles qu'on
    /// contourne (choix de l'utilisateur, 2026-11-08 : traverser une forêt cachait le héros et les objets).
    /// </summary>
    public static bool IsPassable(TerrainType terrain) =>
        terrain is not (TerrainType.Water or TerrainType.Mountain or TerrainType.Forest);
}
