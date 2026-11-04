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
/// Les dimensions de chaque taille de carte. Le générateur découpe la carte en zones de
/// <see cref="MapGenerator.ZoneWidth"/> × <see cref="MapGenerator.ZoneHeight"/> cases environ :
/// 4, 6, 9, 16 et 25 zones. La force des monstres suit le niveau de la zone par rapport à la plus lointaine,
/// les coffres, tas et monstres errants la surface de la carte.
/// </summary>
public static class MapSizes
{
    public static readonly MapSize[] All = Enum.GetValues<MapSize>();

    public static (int Width, int Height) Dimensions(MapSize size) => size switch
    {
        MapSize.Tiny => (36, 27),
        MapSize.Small => (54, 27),
        MapSize.Normal => (54, 40),
        MapSize.Large => (72, 53),
        MapSize.Huge => (90, 66),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    /// <summary>La taille la plus proche de ces dimensions (pour décrire une sauvegarde).</summary>
    public static MapSize Closest(int width, int height) =>
        All.OrderBy(s => Math.Abs(Dimensions(s).Width * Dimensions(s).Height - width * height)).First();
}
