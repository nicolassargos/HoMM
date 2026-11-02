using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>Ce qu'il faut accomplir pour gagner la partie.</summary>
public enum VictoryCondition { ClearGuards, CaptureTowns }

/// <summary>Pourquoi la partie est finie (l'affichage choisit le texte).</summary>
public enum GameEndReason { GuardsCleared, TownsCaptured, HeroesLost, TimeOut }

/// <summary>
/// Conditions de victoire et de défaite, choisies au lancement de la partie.
/// <paramref name="DayLimit"/> : dernier jour où la victoire est encore possible (null = pas de limite).
/// Perdre tous ses héros est toujours une défaite.
/// </summary>
public sealed record GameRules(VictoryCondition Victory, int? DayLimit = null)
{
    /// <summary>Les choix proposés au lancement ; le premier est celui par défaut.</summary>
    public static readonly IReadOnlyList<GameRules> Options = new[]
    {
        new GameRules(VictoryCondition.ClearGuards),
        new GameRules(VictoryCondition.CaptureTowns),
        new GameRules(VictoryCondition.ClearGuards, DayLimit: 150),
    };

    public static GameRules Default => Options[0];
}
