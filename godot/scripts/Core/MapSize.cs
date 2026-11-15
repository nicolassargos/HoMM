using System;
using System.Linq;

namespace HoMM.Core;

/// <summary>Les tailles de carte proposées pour une nouvelle partie (étape 7).</summary>
public enum MapSize
{
    Tiny,
    Small,
    Normal,
    Large,
    Huge,
}

/// <summary>
/// Les dimensions de chaque taille de carte : celles de Heroes 3 (36, 72, 108 et 144 cases de côté, choix de
/// l'utilisateur, 2026-11-14 ; avant : de 54 × 27 à 90 × 66), et une toute petite pour les tests. Le générateur découpe
/// la carte en zones de <see cref="MapGenerator.ZoneWidth"/> × <see cref="MapGenerator.ZoneHeight"/> cases environ :
/// 4, 4, 20, 48 et 88 zones. La force des monstres suit le niveau de la zone par rapport à la plus lointaine,
/// les coffres, tas et monstres errants la surface de la carte.
/// </summary>
public static class MapSizes
{
    public static readonly MapSize[] All = Enum.GetValues<MapSize>();

    public static (int Width, int Height) Dimensions(MapSize size) => size switch
    {
        MapSize.Tiny => (36, 27),
        MapSize.Small => (36, 36),
        MapSize.Normal => (72, 72),
        MapSize.Large => (108, 108),
        MapSize.Huge => (144, 144),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    /// <summary>
    /// Le nombre d'adversaires au plus : chaque joueur a son coin et une zone voisine avec une ville (1 sur les petites
    /// cartes de 4 zones, 3 sur les autres).
    /// </summary>
    public static int MaxOpponents(MapSize size) => size switch
    {
        MapSize.Tiny or MapSize.Small => 1,
        _ => 3,
    };

    /// <summary>La taille la plus proche de ces dimensions (pour décrire une sauvegarde).</summary>
    public static MapSize Closest(int width, int height) =>
        All.OrderBy(s => Math.Abs(Dimensions(s).Width * Dimensions(s).Height - width * height)).First();
}
