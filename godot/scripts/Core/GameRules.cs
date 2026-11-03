using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>
/// Ce qu'il faut accomplir pour gagner la partie. On peut en choisir plusieurs : le premier accompli donne la victoire.
/// (Dans une sauvegarde : « ClearGuards, FindTreasure ».)
/// </summary>
[System.Flags]
public enum VictoryCondition
{
    None = 0,
    ClearGuards = 1,
    CaptureTowns = 2,
    FindTreasure = 4,
}

/// <summary>Pourquoi la partie est finie (l'affichage choisit le texte).</summary>
public enum GameEndReason { GuardsCleared, TownsCaptured, HeroesLost, TimeOut, TreasureFound }

/// <summary>Une partie de la carte, vue comme une grille de 3 × 3 (l'affichage en fait « au nord-est »…).</summary>
public enum Compass { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest, Center }

/// <summary>
/// Conditions de victoire et de défaite, choisies au lancement de la partie : une ou plusieurs façons de gagner
/// (la première accomplie l'emporte), et peut-être une limite de jours.
/// <paramref name="DayLimit"/> : dernier jour où la victoire est encore possible (null = pas de limite).
/// Perdre tous ses héros est toujours une défaite.
/// </summary>
public sealed record GameRules(VictoryCondition Victory, int? DayLimit = null)
{
    /// <summary>La limite de jours proposée au lancement.</summary>
    public const int StandardDayLimit = 150;

    /// <summary>Les façons de gagner proposées au lancement, dans l'ordre de la fenêtre.</summary>
    public static readonly IReadOnlyList<VictoryCondition> Choices =
        new[] { VictoryCondition.ClearGuards, VictoryCondition.CaptureTowns, VictoryCondition.FindTreasure };

    public static GameRules Default => new(VictoryCondition.ClearGuards);

    /// <summary>Cette façon de gagner fait-elle partie des conditions ?</summary>
    public bool Has(VictoryCondition condition) => (Victory & condition) != 0;
}
