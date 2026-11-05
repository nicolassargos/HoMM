using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// L'intelligence des adversaires (étape 8) : elle joue avec les mêmes méthodes de <see cref="GameState"/> que le joueur
/// humain, sans attendre l'affichage, puis l'appelant finit le tour.
/// Chaque héros va ramasser ce qu'il peut prendre sans combat (tas, coffres, mines, obélisques), attaque les monstres et
/// les villes neutres qu'il est sûr de battre sans trop de pertes (point 3), et sinon explore. Il ne connaît que ce que
/// son joueur a exploré (pas de triche) et n'entre dans la zone d'un monstre que pour l'attaquer, lui seul.
/// Il construira et recrutera (point 4), affrontera les autres joueurs (point 5).
/// </summary>
public static class ComputerPlayer
{
    /// <summary>Assez de tentatives pour une journée de marche, sans risquer de tourner en rond.</summary>
    private const int MaxMovesPerHero = 30;

    /// <summary>Un combat vaut la peine s'il est gagné en perdant au plus cette part de la valeur de l'armée.</summary>
    public const double AcceptableLosses = 0.3;

    /// <summary>
    /// Le combat contre cette armée est-il gagné, avec des pertes acceptables (en valeur, l'or des créatures) ?
    /// Selon la même prévision que l'interface (une graine fixe) : le vrai combat peut un peu différer.
    /// </summary>
    internal static bool WorthFighting(Hero hero, Army enemy)
    {
        if (enemy.IsEmpty)
            return true;
        var report = Battle.Resolve(hero.Army.Clone(), enemy.Clone(), hero);
        return report.AttackerWon
            && Battle.ExperienceFor(report.AttackerLosses) <= AcceptableLosses * Battle.ExperienceFor(hero.Army.Stacks);
    }

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
            // le chemin de la partie doit rester sûr (il peut passer ailleurs que par les cases connues) : seule la dernière
            // case peut être dans la zone d'un monstre, et d'un seul (celui que le héros va attaquer)
            if (state.Path is not { Count: > 0 } path || path.Take(path.Count - 1).Any(state.Map.IsGuarded)
                || state.Map.GuardsAround(path[^1]).Count() > 1)
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
    /// La case où aller : le meilleur but (le plus proche, pondéré : une ville neutre à prendre d'abord, puis une mine
    /// qui rapporte chaque jour, les tas et coffres, les obélisques, et un monstre qu'il bat facilement), sinon la case
    /// connue la plus proche au bord de l'inconnu ; null s'il n'y a rien. Pour un monstre, la case est une case de sa
    /// zone que lui seul garde : y entrer déclenche le combat contre lui seul.
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

        // une ville neutre connue : sa garnison doit être battue sans trop de pertes
        foreach (var town in map.Objects.OfType<Town>().Where(t => t.Owner == null).OrderBy(t => t.Id))
        {
            if (!costs.TryGetValue(town.Position, out int cost) || map.IsGuarded(town.Position) || cost * 0.3 >= bestScore)
                continue;
            if (WorthFighting(hero, town.Garrison))
                (best, bestScore) = (town.Position, cost * 0.3);
        }

        // un monstre connu, par une case de sa zone que lui seul garde
        foreach (var guard in map.Objects.OfType<MonsterGuard>().Where(g => player.Fog.IsExplored(g.Position)).OrderBy(g => g.Id))
        {
            var approach = ApproachCells(state, hero, guard, costs).OrderBy(a => a.Cost).ThenBy(a => a.Cell.Y).ThenBy(a => a.Cell.X).FirstOrDefault();
            if (approach.Cost == 0 || approach.Cost * 2.0 >= bestScore)
                continue;
            if (WorthFighting(hero, guard.Army))
                (best, bestScore) = (approach.Cell, approach.Cost * 2.0);
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

    /// <summary>
    /// Les cases de la zone d'un monstre que lui seul garde, avec le coût pour y arriver depuis une case sûre voisine.
    /// </summary>
    private static IEnumerable<(Cell Cell, int Cost)> ApproachCells(GameState state, Hero hero, MonsterGuard guard, Dictionary<Cell, int> costs)
    {
        var map = state.Map;
        var heroes = state.AllHeroes.Where(h => h != hero).Select(h => h.Position).ToHashSet();
        foreach (var cell in Neighbours(guard.Position))
        {
            if (!map.IsPassable(cell) || !hero.Owner.Fog.IsExplored(cell) || map.ObjectAt(cell) != null || heroes.Contains(cell)
                || map.GuardsAround(cell).Any(g => g != guard))
                continue;
            int best = int.MaxValue;
            foreach (var from in Neighbours(cell))
            {
                if (!costs.TryGetValue(from, out int cost) || (from != hero.Position && map.ObjectAt(from) != null))
                    continue;
                int dx = cell.X - from.X, dy = cell.Y - from.Y;
                if (dx != 0 && dy != 0 && !(map.IsPassable(from.Offset(dx, 0)) && map.IsPassable(from.Offset(0, dy))))
                    continue;
                best = Math.Min(best, cost + Pathfinder.StepCost(map, from, cell));
            }
            if (best != int.MaxValue)
                yield return (cell, best);
        }
    }

    private static IEnumerable<Cell> Neighbours(Cell c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0)
                yield return c.Offset(dx, dy);
    }
}
