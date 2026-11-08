using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

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
    /// <summary>
    /// L'ordre de construction d'une ville, pour toutes les factions : l'habitation du niveau 2, la taverne, l'hôtel de
    /// ville, les habitations des niveaux 3 à 5, la guilde, le niveau 6, la guilde 2, le grand hôtel de ville, le niveau 7,
    /// puis la fin de la guilde (comme la simulation d'équilibrage pour le Château).
    /// </summary>
    private static IEnumerable<string> BuildOrder(Town town)
    {
        string? Dwelling(int tier) => town.DwellingOfTier(tier)?.Id;
        return new[]
        {
            Dwelling(2), "taverne", "hotel-ville", Dwelling(3), "marche", Dwelling(4), Dwelling(5), "guilde-mages-1",
            Dwelling(6), "guilde-mages-2", "grand-hotel-ville", Dwelling(7), "guilde-mages-3", "guilde-mages-4",
        }.OfType<string>();
    }

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
        // sans aucun héros, il en engage un avant de bâtir, et ne bâtit pas tant qu'il met l'or de côté pour lui : sinon le
        // bâtiment du jour prend l'or du héros, chaque jour, et il ne joue plus (il ne peut ni prendre ni défendre)
        bool heroless = player.Heroes.Count == 0 && player.Towns.Any(t => t.Buildings.Any(b => b.Tavern && t.Has(b.Id)));
        if (heroless && state.CanHireHero(town) == HireResult.Ok)
            state.HireHero(town);
        if (!heroless || player.Heroes.Count > 0)
            BuildNext(state, town, player);
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
    /// Un bâtiment par jour, dans l'ordre de construction : le premier qu'on peut bâtir (ses bâtiments requis sont là).
    /// S'il manque de quoi le payer, on peut bâtir un bâtiment plus loin dans l'ordre, mais seulement s'il ne retarde pas
    /// le premier : il n'utilise aucune des ressources qui lui manquent, et laisse assez des autres (sinon la guilde des
    /// mages, par exemple, prenait le bois et le minerai que la caserne attendait).
    /// </summary>
    private static void BuildNext(GameState state, Town town, Player player)
    {
        BuildingType? waiting = null;
        foreach (var id in BuildOrder(town))
        {
            if (town.Buildings.FirstOrDefault(b => b.Id == id) is not { } building || town.Has(id))
                continue;
            var result = town.CanBuild(building, player);
            if (result == BuildResult.AlreadyBuiltToday)
                return;
            if (result == BuildResult.MissingRequirement)
                continue;
            if (waiting == null && result == BuildResult.NotEnoughResources)
            {
                waiting = building;
                continue;
            }
            if (result == BuildResult.Ok && (waiting == null || LeavesEnoughFor(player, building, waiting)))
            {
                state.Build(town, id);
                return;
            }
        }
        // rien n'a pu être bâti : au marché, ce qui manque au bâtiment attendu contre ce qu'il a en trop
        if (waiting != null && Market.Count(player) > 0 && TradeFor(state, player, waiting) && town.CanBuild(waiting, player) == BuildResult.Ok)
            state.Build(town, waiting.Id);
    }

    /// <summary>
    /// Échange au marché ce qui manque à <paramref name="building"/> contre ce que le joueur a en trop : le bois, le
    /// minerai et les ressources rares au-delà d'une petite réserve, et l'or au-delà de ce que demande le bâtiment et de
    /// quoi recruter. Seulement si tout ce qui manque peut être obtenu (sinon il garde ses ressources). Renvoie true s'il a
    /// tout obtenu.
    /// </summary>
    /// <summary>Le dernier jour où chaque joueur a échangé au marché pour bâtir (un par jour au plus).</summary>
    private static readonly ConditionalWeakTable<Player, StrongBox<int>> TradedOn = new();

    /// <summary>L'or qu'il garde pour recruter quand il échange au marché pour bâtir.</summary>
    private const int RecruitReserve = 1000;

    private static bool TradeFor(GameState state, Player player, BuildingType building)
    {
        int markets = Market.Count(player);
        int Have(ResourceType r) => player.Resources.GetValueOrDefault(r);
        // l'or : celui du bâtiment, plus de quoi recruter ensuite, et de quoi engager un héros s'il lui en manque (sinon tout
        // son or passait dans les bâtiments : il gardait l'or du héros pour plus tard, sans jamais l'atteindre, et ne
        // recrutait plus rien)
        int reserve = RecruitReserve + (player.Heroes.Count < WantedHeroes(state, player) ? GameState.HeroCost : 0);
        int Needed(ResourceType r) => building.Cost.GetValueOrDefault(r) + (r == ResourceType.Gold ? reserve : 0);
        int Missing(ResourceType r) => Math.Max(0, Needed(r) - Have(r));
        int Spare(ResourceType r) => Math.Max(0, Have(r) - building.Cost.GetValueOrDefault(r)
            - (r == ResourceType.Gold ? 3000 : ResourceInfo.IsRare(r) ? 2 : 10));
        var missing = building.Cost.Keys.Append(ResourceType.Gold).Distinct().Where(r => Missing(r) > 0).ToList();
        if (missing.Count == 0)
            return true;
        // le marché lui permettait de bâtir chaque jour dans chaque ville, et tout son or y passait (une armée qui ne
        // grandissait plus de six semaines) : un seul bâtiment par jour grâce au marché, et il garde de quoi recruter
        // une fois le bâtiment payé (Needed)
        if (TradedOn.TryGetValue(player, out var day) && day.Value == state.Day)
            return false;
        // d'abord vérifier qu'on peut tout obtenir, sans rien échanger
        var spare = ResourceInfo.All.ToDictionary(r => r, Spare);
        foreach (var need in missing)
        {
            int left = Missing(need);
            foreach (var give in ResourceInfo.All.Where(r => r != need && !missing.Contains(r)).OrderByDescending(r => spare[r] * Market.Value(r)))
            {
                int take = Math.Min(left, Market.MaxReceivable(give, need, spare[give], markets));
                if (take <= 0)
                    continue;
                spare[give] -= Market.Cost(give, need, take, markets);
                left -= take;
                if (left == 0)
                    break;
            }
            if (left > 0)
                return false;
        }
        // puis échanger pour de bon
        TradedOn.AddOrUpdate(player, new StrongBox<int>(state.Day));
        foreach (var need in missing)
            foreach (var give in ResourceInfo.All.Where(r => r != need && !missing.Contains(r)).OrderByDescending(r => Spare(r) * Market.Value(r)))
            {
                int take = Math.Min(Missing(need), Market.MaxReceivable(give, need, Spare(give), markets));
                if (take > 0)
                    state.Trade(give, need, take);
                if (Missing(need) == 0)
                    break;
            }
        return missing.All(r => Missing(r) == 0);
    }

    /// <summary>Après avoir payé <paramref name="building"/>, pourra-t-on encore payer <paramref name="waiting"/> dès qu'il ne manquera plus que ce qui lui manque déjà ?</summary>
    private static bool LeavesEnoughFor(Player player, BuildingType building, BuildingType waiting)
    {
        foreach (var (resource, needed) in waiting.Cost)
        {
            int have = player.Resources.GetValueOrDefault(resource), spent = building.Cost.GetValueOrDefault(resource);
            if (spent > 0 && (have < needed || have - spent < needed))
                return false;
        }
        return true;
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

    /// <summary>
    /// Les coffres : l'or tant que le joueur en manque pour recruter et bâtir (moins de 5000), sinon l'expérience. Puis
    /// les niveaux gagnés : l'attaque d'abord, puis la défense (des troupes plus fortes).
    /// </summary>
    private static void ChooseLevels(GameState state)
    {
        foreach (var chest in state.PendingChests.Where(c => c.Hero.Owner == state.CurrentPlayer).ToList())
            state.ChooseChest(chest, gold: state.CurrentPlayer.Resources.GetValueOrDefault(ResourceType.Gold) < 5000);
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
                // les lieux : seulement ce qu'ils donnent encore à ce héros ou à son joueur
                Shrine shrine when !shrine.Visitors.Contains(hero.Id) => 0.8,
                SageHut hut when !hero.Knows(hut.Spell) => 1.2,
                WeeklyProducer { Stocked: true } => 1,
                ObservationTower tower when !tower.Visitors.Contains(player.Id) => 1.2,
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
