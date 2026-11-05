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
/// Dans ses villes, il construit, recrute et engage des héros (point 4) ; un héros vient chercher la garnison. Il
/// attaque aussi les héros et les villes des autres joueurs qu'il bat sans trop de pertes (point 5).
/// </summary>
public static class ComputerPlayer
{
    /// <summary>Assez de tentatives pour une journée de marche, sans risquer de tourner en rond.</summary>
    private const int MaxMovesPerHero = 30;

    /// <summary>Le nombre de prévisions d'un combat avant de l'engager (avec des tirages au sort différents).</summary>
    private const int EstimateRuns = 3;

    /// <summary>Les réglages du niveau de difficulté de la partie.</summary>
    private static ComputerProfile Profile(GameState state) => ComputerProfile.For(state.Rules.Difficulty);

    /// <summary>
    /// Le combat contre cette armée est-il gagné, avec des pertes acceptables (en valeur, l'or des créatures ; la part
    /// acceptée dépend de la difficulté) ? Selon la même prévision que l'interface (une graine fixe) : le vrai combat
    /// peut un peu différer.
    /// </summary>
    internal static bool WorthFighting(GameState state, Hero hero, Army enemy, Hero? enemyHero = null, bool stuck = false)
    {
        if (enemy.IsEmpty)
            return true;
        // sans ville, il ne peut pas remplacer ses pertes : il ne fait que les combats faciles, et garde son armée pour
        // ceux qui le font avancer ; bloqué (rien d'autre à faire), il accepte des pertes deux fois plus lourdes, s'il gagne
        double normal = hero.Owner.Towns.Count == 0 ? Math.Min(0.1, Profile(state).AcceptableLosses) : Profile(state).AcceptableLosses;
        double acceptable = stuck ? Math.Min(0.7, 2 * Profile(state).AcceptableLosses) : normal;
        // le vrai combat tire ses dégâts au sort : on le prévoit trois fois ; il faut gagner chaque fois, et la moyenne des
        // pertes doit rester acceptable (une victoire de justesse à la prévision peut être une défaite)
        int value = Battle.ExperienceFor(hero.Army.Stacks), lost = 0;
        for (int i = 0; i < EstimateRuns; i++)
        {
            var report = Battle.Resolve(hero.Army.Clone(), enemy.Clone(), hero, rng: new GameRandom(Battle.EstimateSeed + i), defenderHero: enemyHero);
            if (!report.AttackerWon)
                return false;
            lost += Battle.ExperienceFor(report.AttackerLosses);
        }
        return lost <= acceptable * value * EstimateRuns;
    }

    /// <summary>Peut-on déjà attaquer ce joueur ? Le joueur humain est épargné au début en Facile.</summary>
    private static bool MayAttack(GameState state, Player? target) =>
        target == null || target.IsComputer || state.Day >= Profile(state).AttackPlayerFromDay;

    /// <summary>
    /// Joue le tour du joueur courant, s'il est joué par l'ordinateur (sans finir le tour). S'arrête si un combat attend
    /// (un joueur humain dirige sa défense, voir <see cref="GameState.HumansDefend"/>) : rappelée après ce combat, elle
    /// reprend le tour là où il en était (ce qui est fait ne se refait pas : un bâtiment par jour, les troupes achetées).
    /// </summary>
    public static void PlayTurn(GameState state)
    {
        if (!state.CurrentPlayer.IsComputer || state.Outcome != GameOutcome.Playing || state.CurrentBattle != null)
            return;
        bool quick = state.QuickBattles;
        state.QuickBattles = true; // un combat imprévu ne doit pas attendre l'écran de combat
        try
        {
            ChooseLevels(state);
            foreach (var town in state.CurrentPlayer.Towns.ToList())
                ManageTown(state, town);
            foreach (var hero in state.CurrentPlayer.Heroes.ToList())
            {
                if (state.Outcome != GameOutcome.Playing || state.CurrentBattle != null)
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

    /// <summary>
    /// L'ordre de construction : les habitations et la taverne d'abord (des troupes, un deuxième héros), puis l'or
    /// (comme la simulation d'équilibrage).
    /// </summary>
    private static readonly string[] BuildOrder =
    {
        "tour-archers", "taverne", "hotel-ville", "tour-griffons", "caserne", "monastere",
        "terrain-entrainement", "grand-hotel-ville", "portail-gloire",
    };

    /// <summary>Le nombre de héros voulus : un de plus que de villes, et pas plus que ne le permet la difficulté.</summary>
    private static int WantedHeroes(GameState state, Player player) => Math.Min(Profile(state).MaxHeroes, player.Towns.Count + 1);

    /// <summary>
    /// Une ville du joueur : un bâtiment (le premier possible dans l'ordre de construction), un héros de plus s'il en
    /// manque (l'or est gardé pour lui), puis les troupes qu'il peut payer (les plus fortes d'abord) ; enfin, un héros
    /// présent emporte la garnison.
    /// </summary>
    private static void ManageTown(GameState state, Town town)
    {
        var player = state.CurrentPlayer;
        foreach (var id in BuildOrder)
            if (town.Buildings.FirstOrDefault(b => b.Id == id) is { } building && town.CanBuild(building, player) == BuildResult.Ok)
            {
                state.Build(town, id);
                break;
            }
        bool wantsHero = player.Heroes.Count < WantedHeroes(state, player) && town.Buildings.Any(b => b.Tavern && town.Has(b.Id));
        if (wantsHero && player.Resources[ResourceType.Gold] >= GameState.HeroCost && state.CanHireHero(town) == HireResult.Ok)
        {
            state.HireHero(town);
            wantsHero = player.Heroes.Count < WantedHeroes(state, player);
        }
        // s'il manque un héros, on garde de quoi l'engager (sinon les troupes achetées chaque jour prendraient tout l'or)
        int reserve = wantsHero ? GameState.HeroCost : 0;
        foreach (var type in town.Available.Keys.OrderByDescending(t => t.Tier).ToList())
        {
            int gold = type.Cost.GetValueOrDefault(ResourceType.Gold);
            int affordable = gold > 0 ? Math.Max(0, player.Resources[ResourceType.Gold] - reserve) / gold : int.MaxValue;
            if (Math.Min(town.MaxRecruitable(type, player), affordable) is > 0 and var count)
                state.Recruit(town, type, count);
        }
        if (state.VisitingHero(town) is { } hero && hero.Owner == player)
            TakeGarrison(state, town, hero);
    }

    /// <summary>
    /// Le héros présent emporte la garnison, les troupes les plus fortes d'abord, sans dépasser ce que son charisme
    /// commande (une troupe trop nombreuse risquerait de se révolter) : le reste attend dans la ville.
    /// </summary>
    private static void TakeGarrison(GameState state, Town town, Hero hero)
    {
        int limit = Command.Limit(hero.Charisma);
        foreach (var type in town.Garrison.Stacks.Select(s => s.Type).Distinct().OrderByDescending(t => t.Tier).ToList())
        {
            int index = IndexOf(town.Garrison, type);
            if (index < 0)
                continue;
            int slot = IndexOf(hero.Army, type);
            int already = slot >= 0 ? hero.Army.Stacks[slot].Count : 0;
            if (slot < 0)
            {
                if (hero.Army.Stacks.Count >= Army.MaxStacks)
                    continue;
                slot = hero.Army.Stacks.Count;
            }
            int room = limit / Math.Max(1, type.CommandCost) - already;
            int count = Math.Min(room, town.Garrison.Stacks[index].Count);
            if (count > 0)
                state.MoveTroop(town, fromGarrison: true, index, toGarrison: false, slot, count);
        }
    }

    private static int IndexOf(Army army, CreatureType type)
    {
        for (int i = 0; i < army.Stacks.Count; i++)
            if (army.Stacks[i].Type == type)
                return i;
        return -1;
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
            // le chemin de la partie doit rester sûr, pour ce que le joueur sait : seule la dernière case peut être dans la
            // zone d'un monstre connu, et d'un seul (celui que le héros va attaquer)
            if (state.Path is not { Count: > 0 } path || path.Take(path.Count - 1).Any(c => KnownGuards(state, hero, c).Any())
                || KnownGuards(state, hero, path[^1]).Count() > 1)
            {
                state.ClearPath();
                return;
            }
            var before = hero.Position;
            // pas à pas : un monstre découvert en chemin (le brouillard se lève à chaque pas) fait revoir le plan, sauf
            // s'il vaut la peine d'être combattu
            while (state.Path is { Count: > 0 } next)
            {
                // (le combat visé, sur la dernière case, a déjà été jugé : on ne le juge pas une seconde fois)
                var guards = KnownGuards(state, hero, next[0]).ToList();
                bool planned = next.Count == 1 && next[0] == target;
                if (guards.Count > 1 || guards.Count == 1 && !planned && !WorthFighting(state, hero, guards[0].Army))
                {
                    state.ClearPath();
                    break;
                }
                if (!state.StepAlongPath())
                    break;
            }
            if (state.CurrentBattle != null) // le joueur humain se défend : on attendra la fin du combat
                return;
            ChooseLevels(state);
            if (state.Map.ObjectAt(hero.Position) is Town own && own.Owner == hero.Owner && own.Position == hero.Position)
                TakeGarrison(state, own, hero);
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
    internal static Cell? ChooseTarget(GameState state, Hero hero) =>
        ChooseTarget(state, hero, stuck: false) ?? ChooseTarget(state, hero, stuck: true);

    /// <param name="stuck">
    /// Rien d'autre à faire (ni à ramasser, ni à explorer, ni de combat sûr) : il accepte un combat plus coûteux, pour
    /// sortir de sa zone (un gardien de passage) ou prendre une ville, plutôt que d'attendre que les monstres grossissent.
    /// </param>
    private static Cell? ChooseTarget(GameState state, Hero hero, bool stuck)
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
            if (weight == 0 || obj.Position == hero.Position || !costs.TryGetValue(obj.Position, out int cost))
                continue;
            if (cost * weight < bestScore)
                (best, bestScore) = (obj.Position, cost * weight);
        }

        // une de ses villes, si la garnison qui l'attend vaut au moins la moitié de son armée
        foreach (var town in player.Towns.OrderBy(t => t.Id))
        {
            if (town.Position == hero.Position || town.Garrison.IsEmpty || !costs.TryGetValue(town.Position, out int cost))
                continue;
            if (Battle.ExperienceFor(town.Garrison.Stacks) * 2 >= Battle.ExperienceFor(hero.Army.Stacks) && cost * 0.4 < bestScore)
                (best, bestScore) = (town.Position, cost * 0.4);
        }

        // une ville neutre ou d'un autre joueur, connue : sa garnison (et le héros qui s'y trouve) doit être battue sans
        // trop de pertes
        foreach (var town in map.Objects.OfType<Town>().Where(t => t.Owner != player).OrderBy(t => t.Id))
        {
            if (!costs.TryGetValue(town.Position, out int cost) || map.IsGuarded(town.Position) || cost * 0.3 >= bestScore)
                continue;
            var guardian = state.EnemyHeroAt(town.Position, player);
            if (!MayAttack(state, town.Owner) || guardian != null && !MayAttack(state, guardian.Owner))
                continue;
            if (WorthFighting(state, hero, town.Garrison, stuck: stuck)
                && (guardian == null || WorthFighting(state, hero, guardian.Army, guardian, stuck)))
                (best, bestScore) = (town.Position, cost * 0.3);
        }

        // un héros d'un autre joueur, là où le joueur a exploré
        foreach (var enemy in state.AllHeroes.Where(h => h.Owner != player && player.Fog.IsExplored(h.Position)).OrderBy(h => h.Id))
        {
            if (!costs.TryGetValue(enemy.Position, out int cost) || cost * 0.5 >= bestScore || !MayAttack(state, enemy.Owner))
                continue;
            if (WorthFighting(state, hero, enemy.Army, enemy, stuck))
                (best, bestScore) = (enemy.Position, cost * 0.5);
        }

        // un monstre connu, par une case de sa zone que lui seul garde
        foreach (var guard in map.Objects.OfType<MonsterGuard>().Where(g => player.Fog.IsExplored(g.Position)).OrderBy(g => g.Id))
        {
            // bloqué, un combat coûteux ne vaut que s'il ouvre du pays nouveau (de l'inconnu juste derrière le monstre,
            // comme un gardien de passage) : pas pour un monstre qui garde une mine au milieu de sa zone
            if (stuck && !OpensNewGround(state, player, guard))
                continue;
            var approach = ApproachCells(state, hero, guard, costs).OrderBy(a => a.Cost).ThenBy(a => a.Cell.Y).ThenBy(a => a.Cell.X).FirstOrDefault();
            if (approach.Cost == 0 || approach.Cost * 2.0 >= bestScore)
                continue;
            if (WorthFighting(state, hero, guard.Army, stuck: stuck))
                (best, bestScore) = (approach.Cell, approach.Cost * 2.0);
        }
        if (best != null)
            return best;

        // rien à prendre : explorer, vers la case connue la plus proche qui touche l'inconnu
        foreach (var (cell, cost) in costs.OrderBy(c => c.Value).ThenBy(c => c.Key.Y).ThenBy(c => c.Key.X))
            if (cell != hero.Position && map.ObjectAt(cell) == null && state.EnemyHeroAt(cell, player) == null
                && Neighbours(cell).Any(n => map.InBounds(n) && !player.Fog.IsExplored(n)))
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
        var heroes = state.AllHeroes.Where(h => h != hero && h.Owner == hero.Owner).Select(h => h.Position).ToHashSet();
        var enemies = state.AllHeroes.Where(h => h.Owner != hero.Owner).Select(h => h.Position).ToHashSet();
        var costs = new Dictionary<Cell, int> { [hero.Position] = 0 };
        var open = new PriorityQueue<Cell, int>();
        open.Enqueue(hero.Position, 0);
        while (open.TryDequeue(out var cell, out int cost))
        {
            if (cost > costs[cell])
                continue;
            if (cell != hero.Position && (map.ObjectAt(cell) != null || enemies.Contains(cell)))
                continue; // on visite un objet (on attaque un héros ennemi), on ne le traverse pas
            foreach (var next in Neighbours(cell))
            {
                if (!map.IsPassable(next) || !fog.IsExplored(next) || heroes.Contains(next) || KnownGuards(state, hero, next).Any())
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

    /// <summary>Y a-t-il de l'inconnu (une case franchissable pas encore explorée) à deux pas du monstre ?</summary>
    private static bool OpensNewGround(GameState state, Player player, MonsterGuard guard)
    {
        for (int dy = -2; dy <= 2; dy++)
        for (int dx = -2; dx <= 2; dx++)
        {
            var c = guard.Position.Offset(dx, dy);
            if (state.Map.InBounds(c) && !player.Fog.IsExplored(c) && state.Map.IsPassable(c))
                return true;
        }
        return false;
    }

    /// <summary>Les monstres connus du joueur (sur une case explorée) dont la zone couvre cette case.</summary>
    private static IEnumerable<MonsterGuard> KnownGuards(GameState state, Hero hero, Cell cell) =>
        state.Map.GuardsAround(cell).Where(g => hero.Owner.Fog.IsExplored(g.Position));

    private static IEnumerable<Cell> Neighbours(Cell c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0)
                yield return c.Offset(dx, dy);
    }
}
