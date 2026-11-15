using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>
/// Ce qu'il faut accomplir pour gagner la partie. On peut en choisir plusieurs : le premier accompli donne la victoire.
/// (Dans une sauvegarde : « CaptureTowns, FindTreasure ».)
/// </summary>
[System.Flags]
public enum VictoryCondition
{
    None = 0,
    /// <summary>
    /// Vaincre tous les monstres : retiré (choix de l'utilisateur, 2026-11-14 : sur les grandes cartes de Heroes 3, il n'a
    /// plus de sens). Gardé pour lire les anciennes sauvegardes, où il devient « prendre toutes les villes ».
    /// </summary>
    ClearGuards = 1,
    CaptureTowns = 2,
    FindTreasure = 4,

    /// <summary>Tous les adversaires (joueurs ordinateur) ont perdu : ni héros ni ville (étape 8).</summary>
    DefeatOpponents = 8,
}

/// <summary>Pourquoi la partie est finie (l'affichage choisit le texte).</summary>
/// <summary>Pourquoi la partie s'arrête. <c>Eliminated</c> : la dernière ville du joueur est prise alors qu'il n'avait plus de héros.</summary>
public enum GameEndReason { TownsCaptured, HeroesLost, TimeOut, TreasureFound, OpponentsDefeated, Eliminated }

/// <summary>La force des adversaires joués par l'ordinateur (voir <see cref="ComputerProfile"/>).</summary>
public enum Difficulty { Easy, Normal, Hard }

/// <summary>
/// Les réglages de l'intelligence des adversaires pour un niveau de difficulté (étape 8). Normal est le niveau joué
/// jusqu'ici (jugé bien par l'utilisateur) ; Facile est plus prudent, attaque le joueur plus tard, a moins de héros et
/// moins d'or ; Difficile prend plus de risques, a plus de héros et plus d'or (comme les adversaires de Heroes).
/// </summary>
/// <param name="AcceptableLosses">Un combat vaut la peine s'il est gagné en perdant au plus cette part de la valeur de l'armée.</param>
/// <param name="AttackPlayerFromDay">Le premier jour où l'adversaire attaque les héros et les villes du joueur humain.</param>
/// <param name="MaxHeroes">Le nombre de héros au plus.</param>
/// <param name="GoldIncomePercent">Le revenu d'or de chaque jour, en pourcentage du revenu normal.</param>
public sealed record ComputerProfile(double AcceptableLosses, int AttackPlayerFromDay, int MaxHeroes, int GoldIncomePercent)
{
    public static ComputerProfile For(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => new(0.2, 21, 2, 75),
        Difficulty.Hard => new(0.4, 0, 4, 125),
        _ => new(0.3, 0, 3, 100),
    };
}

/// <summary>Une partie de la carte, vue comme une grille de 3 × 3 (l'affichage en fait « au nord-est »…).</summary>
public enum Compass { North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest, Center }

/// <summary>
/// Conditions de victoire et de défaite, choisies au lancement de la partie : une ou plusieurs façons de gagner
/// (la première accomplie l'emporte), et peut-être une limite de jours.
/// <paramref name="DayLimit"/> : dernier jour où la victoire est encore possible (null = pas de limite).
/// Perdre tous ses héros est toujours une défaite. <paramref name="Difficulty"/> : la force des adversaires (étape 8).
/// </summary>
public sealed record GameRules(VictoryCondition Victory, int? DayLimit = null, Difficulty Difficulty = Difficulty.Normal)
{
    /// <summary>La limite de jours proposée au lancement.</summary>
    public const int StandardDayLimit = 150;

    /// <summary>Les façons de gagner proposées au lancement, dans l'ordre de la fenêtre.</summary>
    public static readonly IReadOnlyList<VictoryCondition> Choices =
        new[] { VictoryCondition.CaptureTowns, VictoryCondition.FindTreasure, VictoryCondition.DefeatOpponents };

    /// <summary>Prendre toutes les villes, comme Heroes 3 (« vaincre tous les monstres » avant le 2026-11-14).</summary>
    public static GameRules Default => new(VictoryCondition.CaptureTowns);

    /// <summary>Les règles d'une ancienne sauvegarde : « vaincre tous les monstres » devient « prendre toutes les villes ».</summary>
    public GameRules Updated() => Has(VictoryCondition.ClearGuards)
        ? this with { Victory = (Victory & ~VictoryCondition.ClearGuards) | VictoryCondition.CaptureTowns }
        : this;

    /// <summary>Cette façon de gagner fait-elle partie des conditions ?</summary>
    public bool Has(VictoryCondition condition) => (Victory & condition) != 0;
}
