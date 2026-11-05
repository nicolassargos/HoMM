using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// L'intelligence des adversaires (étape 8) : elle joue avec les mêmes méthodes de <see cref="GameState"/> que le joueur
/// humain, sans attendre l'affichage, puis l'appelant finit le tour.
/// Point 2 : chaque héros va ramasser ce qu'il peut prendre sans combat (tas, coffres, mines, obélisques), et sinon
/// explore. Il ne connaît que ce que son joueur a exploré (pas de triche) et n'entre jamais dans la zone d'un monstre.
/// Il construira et recrutera (point 3), combattra (point 4).
/// </summary>
public static class ComputerPlayer
{
    /// <summary>Assez de tentatives pour une journée de marche, sans risquer de tourner en rond.</summary>
    private const int MaxMovesPerHero = 30;

    /// <summary>Joue le tour du joueur courant, s'il est joué par l'ordinateur (sans finir le tour).</summary>
    public static void PlayTurn(GameState state)
    {
        if (!state.CurrentPlayer.IsComputer || state.Outcome != GameOutcome.Playing)
            return;
        bool quick = state.QuickBattles;
        state.QuickBattles = true; // un combat imprévu ne doit pas attendre l'écran de combat
        try
        {
            ChooseLevels(state);
            foreach (var hero in state.CurrentPlayer.Heroes.ToList())
            {
                if (state.Outcome != GameOutcome.Playing)
                    return;
                if (hero.Owner.Heroes.Contains(hero))
                    MoveHero(state, hero);
            }
        }
        finally
        {
            state.QuickBattles = quick;
        }
    }

    /// <summary>Les niveaux gagnés : l'attaque d'abord, puis la défense (des troupes plus fortes).</summary>
    private static void ChooseLevels(GameState state)
    {
        foreach (var choice in state.PendingChoices.Where(c => c.Hero.Owner == state.CurrentPlayer).ToList())
        {
            var stat = choice.Options.Contains(HeroStat.Attack) ? HeroStat.Attack
                : choice.Options.Contains(HeroStat.Defense) ? HeroStat.Defense : choice.Options[0];
            state.ChooseStat(choice, stat);
        }
    }

    private static void MoveHero(GameState state, Hero hero)
    {
        state.SelectHero(hero);
        for (int attempt = 0; attempt < MaxMovesPerHero && hero.Movement > 0; attempt++)
        {
            if (ChooseTarget(state, hero) is not { } target)
                return;
            state.SetDestination(target);
            // le chemin de la partie doit rester sûr (il peut passer ailleurs que par les cases connues)
            if (state.Path is not { Count: > 0 } path || path.Any(state.Map.IsGuarded))
            {
                state.ClearPath();
                return;
            }
            var before = hero.Position;
            while (state.StepAlongPath()) { }
            ChooseLevels(state);
            if (state.Outcome != GameOutcome.Playing || !hero.Owner.Heroes.Contains(hero))
                return;
            if (hero.Position == before) // plus assez de mouvement pour le premier pas
                return;
        }
    }

    /// <summary>
    /// La case où aller : le meilleur objet à ramasser (le plus proche ; une mine vaut deux fois plus, elle rapporte
    /// chaque jour), sinon la case connue la plus proche au bord de l'inconnu ; null s'il n'y a rien.
    /// </summary>
    internal static Cell? ChooseTarget(GameState state, Hero hero)
    {
        var player = hero.Owner;
        var map = state.Map;
        var costs = SafeCosts(state, hero);
        Cell? best = null;
        double bestScore = double.MaxValue;
        foreach (var obj in map.Objects.OrderBy(o => o.Position.Y).ThenBy(o => o.Position.X))
        {
            double weight = obj switch
            {
                ResourcePile or TreasureChest => 1,
                Mine mine when mine.Owner != player => 0.5,
                Obelisk obelisk when !player.Obelisks.Contains(obelisk.Id) => 1.5,
                _ => 0,
            };
            if (weight == 0 || !costs.TryGetValue(obj.Position, out int cost))
                continue;
            if (cost * weight < bestScore)
                (best, bestScore) = (obj.Position, cost * weight);
        }
        if (best != null)
            return best;

        // rien à prendre : explorer, vers la case connue la plus proche qui touche l'inconnu
        foreach (var (cell, cost) in costs.OrderBy(c => c.Value).ThenBy(c => c.Key.Y).ThenBy(c => c.Key.X))
            if (cell != hero.Position && map.ObjectAt(cell) == null && Neighbours(cell).Any(n => map.InBounds(n) && !player.Fog.IsExplored(n)))
                return cell;
        return null;
    }

    /// <summary>
    /// Le coût du chemin le plus court jusqu'à chaque case qu'on peut atteindre sans combat, par les cases que le joueur
    /// connaît : ni zone de monstre, ni objet à traverser (on peut seulement s'y arrêter), ni autre héros.
    /// </summary>
    private static Dictionary<Cell, int> SafeCosts(GameState state, Hero hero)
    {
        var map = state.Map;
        var fog = hero.Owner.Fog;
        var heroes = state.AllHeroes.Where(h => h != hero).Select(h => h.Position).ToHashSet();
        var costs = new Dictionary<Cell, int> { [hero.Position] = 0 };
        var open = new PriorityQueue<Cell, int>();
        open.Enqueue(hero.Position, 0);
        while (open.TryDequeue(out var cell, out int cost))
        {
            if (cost > costs[cell])
                continue;
            if (cell != hero.Position && map.ObjectAt(cell) != null)
                continue; // on visite un objet, on ne le traverse pas
            foreach (var next in Neighbours(cell))
            {
                if (!map.IsPassable(next) || !fog.IsExplored(next) || heroes.Contains(next) || map.IsGuarded(next))
                    continue;
                if (map.ObjectAt(next) is MonsterGuard)
                    continue;
                int dx = next.X - cell.X, dy = next.Y - cell.Y;
                if (dx != 0 && dy != 0 && !(map.IsPassable(cell.Offset(dx, 0)) && map.IsPassable(cell.Offset(0, dy))))
                    continue;
                int nextCost = cost + Pathfinder.StepCost(map, cell, next);
                if (nextCost < costs.GetValueOrDefault(next, int.MaxValue))
                {
                    costs[next] = nextCost;
                    open.Enqueue(next, nextCost);
                }
            }
        }
        return costs;
    }

    private static IEnumerable<Cell> Neighbours(Cell c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0)
                yield return c.Offset(dx, dy);
    }
}
