using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>Résultat d'un combat : vainqueur et pertes de chaque camp.</summary>
public sealed class BattleReport
{
    public required bool AttackerWon { get; init; }
    public required List<CreatureStack> AttackerLosses { get; init; }
    public required List<CreatureStack> DefenderLosses { get; init; }
    public int Rounds { get; init; }

    /// <summary>Le héros s'est enfui (combat tactique) : il garde ce qui reste de son armée, sans expérience.</summary>
    public bool Fled { get; init; }

    /// <summary>Troupes révoltées avant le combat : elles combattent dans le camp ennemi.</summary>
    public List<CreatureStack> Rebels { get; init; } = new();

    /// <summary>Survivants des troupes révoltées : ils quittent l'armée après le combat.</summary>
    public List<CreatureStack> RebelSurvivors { get; init; } = new();

    /// <summary>Expérience gagnée : la valeur en or des créatures ennemies tuées.</summary>
    public int Experience => Fled ? 0 : Battle.ExperienceFor(DefenderLosses);
}

/// <summary>
/// Le combat rapide : le combat tactique (<see cref="TacticalBattle"/>), joué d'un coup par l'IA pour les deux camps
/// (<see cref="BattleAI"/>). Les dégâts sont tirés au sort avec <c>rng</c> (le hasard de la partie, pour qu'une partie
/// chargée continue pareil) ; sans <c>rng</c>, avec une graine fixe (une estimation qui ne change pas d'un survol à l'autre).
/// </summary>
public static class Battle
{
    /// <summary>La graine des combats joués sans le hasard de la partie (estimations, tests).</summary>
    public const int EstimateSeed = 12345;

    /// <summary>Bonus ou malus de dégâts selon l'attaque et la défense (règle de Heroes 3).</summary>
    public static double DamageModifier(int attack, int defense) =>
        attack >= defense
            ? Math.Min(1 + 0.05 * (attack - defense), 4.0)
            : Math.Max(1 - 0.025 * (defense - attack), 0.3);

    /// <summary>
    /// Fait combattre les deux armées (combat rapide) et les modifie (créatures tuées retirées).
    /// <paramref name="hero"/> (celui de l'armée attaquante) ajoute son attaque et sa défense à ses troupes.
    /// <paramref name="rebels"/> sont des troupes révoltées : elles combattent avec le camp défenseur,
    /// sans le bonus du héros. Leurs survivants sont retirés de <paramref name="rebels"/>.
    /// </summary>
    public static BattleReport Resolve(Army attacker, Army defender, Hero? hero = null, Army? rebels = null, GameRandom? rng = null,
        Hero? defenderHero = null)
    {
        var battle = new TacticalBattle(attacker, defender, rng ?? new GameRandom(EstimateSeed), hero, rebels, defenderHero);
        BattleAI.PlayOut(battle);
        return battle.Report();
    }

    /// <summary>Valeur en or des créatures tuées : c'est l'expérience gagnée par le héros.</summary>
    public static int ExperienceFor(IEnumerable<CreatureStack> losses) =>
        losses.Sum(l => l.Count * l.Type.Cost.GetValueOrDefault(ResourceType.Gold));
}

/// <summary>
/// Un combat a eu lieu contre <paramref name="Defender"/> (un monstre, ou la garnison d'une ville).
/// <paramref name="DefenderArmy"/> est l'armée adverse au début du combat (elle a pu changer depuis).
/// </summary>
/// <paramref name="DefenderOwner"/> : le joueur qui se défendait (le propriétaire de la ville ou du héros attaqué), s'il
/// y en a un : il voit aussi le bilan.
public sealed record BattleFought(Hero Hero, MapObject Defender, IReadOnlyList<CreatureStack> DefenderArmy, BattleReport Report,
    Player? DefenderOwner = null) : GameEvent;
