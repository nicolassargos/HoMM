using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

public class MapGeneratorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Tous_les_objets_sont_accessibles_depuis_le_depart(int seed)
    {
        var map = new MapGenerator(seed).Generate();
        foreach (var obj in map.Objects)
            Assert.NotNull(Pathfinder.FindPath(map, map.Start, obj.Position));
    }

    [Fact]
    public void La_meme_graine_donne_la_meme_carte()
    {
        var a = new MapGenerator(123).Generate();
        var b = new MapGenerator(123).Generate();
        for (int y = 0; y < a.Height; y++)
        for (int x = 0; x < a.Width; x++)
            Assert.Equal(a.GetTerrain(new Cell(x, y)), b.GetTerrain(new Cell(x, y)));
        Assert.Equal(a.Objects.Select(o => o.Position).OrderBy(p => p.Y).ThenBy(p => p.X),
                     b.Objects.Select(o => o.Position).OrderBy(p => p.Y).ThenBy(p => p.X));
    }

    [Fact]
    public void Une_petite_carte_pose_ce_qui_rentre_sans_planter()
    {
        var map = new MapGenerator(5).Generate(6, 6);
        Assert.NotEmpty(map.Objects);
        Assert.True(map.Objects.Count < ResourceInfo.All.Length + 6 + 28);
    }
}

public class PathfinderTests
{
    private static AdventureMap FlatMap()
    {
        var map = new AdventureMap(10, 10);
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
            map.SetTerrain(new Cell(x, y), TerrainType.Grass);
        return map;
    }

    [Fact]
    public void Une_diagonale_coute_plus_cher_qu_un_pas_droit()
    {
        var map = FlatMap();
        Assert.Equal(100, Pathfinder.StepCost(map, new Cell(1, 1), new Cell(2, 1)));
        Assert.Equal(141, Pathfinder.StepCost(map, new Cell(1, 1), new Cell(2, 2)));
    }

    [Fact]
    public void Le_chemin_contourne_l_eau()
    {
        var map = FlatMap();
        for (int y = 0; y < 9; y++)
            map.SetTerrain(new Cell(5, y), TerrainType.Water);
        var path = Pathfinder.FindPath(map, new Cell(2, 2), new Cell(8, 2));
        Assert.NotNull(path);
        Assert.DoesNotContain(path!, c => map.GetTerrain(c) == TerrainType.Water);
        Assert.Contains(new Cell(5, 9), path!);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Le_chemin_trouve_est_le_moins_cher(int seed)
    {
        var map = new MapGenerator(seed).Generate();
        var best = CheapestCosts(map, map.Start);
        foreach (var obj in map.Objects)
        {
            var path = Pathfinder.FindPath(map, map.Start, obj.Position)!;
            int cost = 0;
            var previous = map.Start;
            foreach (var cell in path)
            {
                cost += Pathfinder.StepCost(map, previous, cell);
                previous = cell;
            }
            Assert.Equal(best[obj.Position], cost);
        }
    }

    /// <summary>Dijkstra simple (sans estimation), avec les mêmes règles que le Pathfinder.</summary>
    private static Dictionary<Cell, int> CheapestCosts(AdventureMap map, Cell start)
    {
        var best = new Dictionary<Cell, int> { [start] = 0 };
        var open = new PriorityQueue<Cell, int>();
        open.Enqueue(start, 0);
        while (open.TryDequeue(out var cell, out int cost))
        {
            if (cost > best[cell] || (cell != start && map.ObjectAt(cell) != null))
                continue;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var next = cell.Offset(dx, dy);
                if ((dx == 0 && dy == 0) || !map.IsPassable(next))
                    continue;
                if (dx != 0 && dy != 0 && !(map.IsPassable(cell.Offset(dx, 0)) && map.IsPassable(cell.Offset(0, dy))))
                    continue;
                int newCost = cost + Pathfinder.StepCost(map, cell, next);
                if (newCost < best.GetValueOrDefault(next, int.MaxValue))
                {
                    best[next] = newCost;
                    open.Enqueue(next, newCost);
                }
            }
        }
        return best;
    }

    [Fact]
    public void Pas_de_chemin_vers_une_montagne()
    {
        var map = FlatMap();
        map.SetTerrain(new Cell(4, 4), TerrainType.Mountain);
        Assert.Null(Pathfinder.FindPath(map, new Cell(0, 0), new Cell(4, 4)));
    }
}

public class GameStateTests
{
    private static Player Human(GameState state) => state.Players[0];
    private static Hero Hero(GameState state) => state.CurrentHero!;

    /// <summary>Le tas de ressources le plus proche qu'on peut atteindre sans croiser de monstre.</summary>
    private static ResourcePile NearestSafePile(GameState state) => state.Map.Objects.OfType<ResourcePile>()
        .Select(p => (Pile: p, Path: Pathfinder.FindPath(state.Map, Hero(state).Position, p.Position)))
        .Where(x => x.Path != null && !x.Path.Any(state.Map.IsGuarded))
        .OrderBy(x => x.Path!.Count)
        .First().Pile;

    [Fact]
    public void Une_mine_capturee_rapporte_a_chaque_fin_de_tour()
    {
        var state = new GameState(7, TestData.Creatures);
        var mine = state.Map.Objects.OfType<Mine>().First(m => m.Resource == ResourceType.Gold);
        mine.Visit(Hero(state));
        int gold = Human(state).Resources[ResourceType.Gold];

        state.EndTurn();

        Assert.Equal(gold + 1000, Human(state).Resources[ResourceType.Gold]);
    }

    [Fact]
    public void La_fin_du_tour_rend_les_points_de_mouvement()
    {
        var state = new GameState(7, TestData.Creatures);
        state.SetDestination(NearestSafePile(state).Position);
        while (state.StepAlongPath()) { }
        Assert.True(Hero(state).Movement < Hero(state).MaxMovement);

        state.EndTurn();

        Assert.Equal(Hero(state).MaxMovement, Hero(state).Movement);
        Assert.Equal(1, state.Day);
    }

    [Fact]
    public void Ramasser_un_tas_ajoute_la_ressource_et_le_retire_de_la_carte()
    {
        var state = new GameState(7, TestData.Creatures);
        var pile = NearestSafePile(state);
        int before = Human(state).Resources[pile.Resource];

        state.SetDestination(pile.Position);
        for (int day = 0; day < 10 && Hero(state).Position != pile.Position; day++)
        {
            while (state.StepAlongPath()) { }
            state.EndTurn();
        }

        Assert.Equal(before + pile.Amount, Human(state).Resources[pile.Resource]);
        Assert.Null(state.Map.ObjectAt(pile.Position));
    }

    [Fact]
    public void Entrer_a_cote_d_un_monstre_declenche_le_combat()
    {
        var state = new GameState(7, TestData.Creatures);
        var start = Hero(state).Position;
        var guard = new MonsterGuard(start.Offset(2, 0), TestData.Creatures.Get("gobelin"), 3);
        state.Map.AddObject(guard);

        state.SetDestination(start.Offset(1, 0));
        state.TakeEvents();
        state.StepAlongPath();

        var events = state.TakeEvents();
        Assert.IsType<HeroMoved>(events[0]);
        var battle = Assert.IsType<BattleFought>(events[1]);
        Assert.True(battle.Report.AttackerWon);
        Assert.Equal(3, battle.EnemyCount);
        Assert.Same(guard, battle.Guard);
        Assert.Null(state.Map.ObjectAt(guard.Position));
        Assert.Equal(GameOutcome.Playing, state.Outcome);
    }

    [Fact]
    public void Les_evenements_ne_sont_rendus_qu_une_fois()
    {
        var state = new GameState(7, TestData.Creatures);
        state.EndTurn();

        Assert.Equal(new DayStarted(1, false), Assert.Single(state.TakeEvents()));
        Assert.Empty(state.TakeEvents());
    }

    [Fact]
    public void Chaque_pas_et_chaque_visite_donnent_un_evenement()
    {
        var state = new GameState(7, TestData.Creatures);
        var pile = NearestSafePile(state);
        state.SetDestination(pile.Position);
        int steps = state.Path!.Count;
        while (state.StepAlongPath()) { }

        var events = state.TakeEvents();
        Assert.Equal(pile.Position, Hero(state).Position); // le tas le plus proche est atteint dès le premier jour
        Assert.Equal(steps, events.OfType<HeroMoved>().Count());
        var found = Assert.IsType<ResourcesFound>(events[^1]);
        Assert.Same(pile, found.Object);
        Assert.Equal(pile.Resource, found.Resource);
        Assert.Equal(pile.Amount, found.Amount);
        Assert.Null(state.Map.ObjectAt(pile.Position));
    }

    [Fact]
    public void Vaincre_le_dernier_monstre_fait_gagner_la_partie()
    {
        var state = new GameState(7, TestData.Creatures);
        foreach (var g in state.Map.Objects.OfType<MonsterGuard>().ToList())
            state.Map.RemoveObject(g);
        var start = Hero(state).Position;
        state.Map.AddObject(new MonsterGuard(start.Offset(2, 0), TestData.Creatures.Get("gobelin"), 1));

        state.SetDestination(start.Offset(1, 0));
        state.StepAlongPath();

        Assert.Equal(GameOutcome.Victory, state.Outcome);
        Assert.Equal(new GameEnded(GameOutcome.Victory), state.TakeEvents().OfType<GameEnded>().Single());
    }

    [Fact]
    public void Perdre_son_seul_heros_fait_perdre_la_partie()
    {
        var state = new GameState(7, TestData.Creatures);
        var start = Hero(state).Position;
        state.Map.AddObject(new MonsterGuard(start.Offset(2, 0), TestData.Creatures.Get("dragon"), 50));

        state.SetDestination(start.Offset(1, 0));
        state.StepAlongPath();

        var events = state.TakeEvents();
        Assert.False(events.OfType<BattleFought>().Single().Report.AttackerWon);
        Assert.Single(events.OfType<HeroDefeated>());
        Assert.Equal(new GameEnded(GameOutcome.Defeat), events[^1]);
        Assert.Null(state.CurrentHero);
        Assert.Equal(GameOutcome.Defeat, state.Outcome);
    }

    [Fact]
    public void Les_monstres_grossissent_chaque_semaine()
    {
        var state = new GameState(7, TestData.Creatures);
        var guard = state.Map.Objects.OfType<MonsterGuard>().First();
        int before = guard.Count;

        for (int i = 0; i < 7; i++)
            state.EndTurn();

        Assert.True(guard.Count > before);
    }
}

public class GuardPlacementTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Les_monstres_gardent_mines_et_coffres_sans_bloquer_le_depart(int seed)
    {
        var map = new MapGenerator(seed, TestData.Creatures).Generate();
        var guards = map.Objects.OfType<MonsterGuard>().ToList();
        Assert.NotEmpty(guards);

        // Presque toutes les mines et tous les coffres ont un gardien à côté
        var valuables = map.Objects.Where(o => o is Mine or TreasureChest).ToList();
        Assert.True(valuables.Count(o => map.IsGuarded(o.Position)) >= valuables.Count - 2);

        foreach (var g in guards)
        {
            Assert.True(System.Math.Max(System.Math.Abs(g.Position.X - map.Start.X), System.Math.Abs(g.Position.Y - map.Start.Y)) >= 4);
            Assert.True(g.Type.Tier <= 4);
        }
        Assert.False(map.IsGuarded(map.Start));
    }

    [Fact]
    public void Le_chemin_contourne_un_monstre_quand_c_est_possible()
    {
        var map = new AdventureMap(10, 10);
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
            map.SetTerrain(new Cell(x, y), TerrainType.Grass);
        map.AddObject(new MonsterGuard(new Cell(5, 5), TestData.Creatures.Get("loup"), 10));

        var path = Pathfinder.FindPath(map, new Cell(0, 5), new Cell(9, 5));

        Assert.NotNull(path);
        Assert.DoesNotContain(path!, map.IsGuarded);
    }
}

public class BattleTests
{
    private static Army ArmyOf(string id, int count)
    {
        var army = new Army();
        army.Add(TestData.Creatures.Get(id), count);
        return army;
    }

    [Fact]
    public void Les_creatures_sont_lues_depuis_le_fichier_de_donnees()
    {
        var piquier = TestData.Creatures.Get("piquier");
        Assert.Equal("Piquiers", piquier.Plural);
        Assert.Equal(10, piquier.HitPoints);
        Assert.Equal(60, piquier.Cost[ResourceType.Gold]);
        Assert.Equal(7, TestData.Creatures.All.Count(c => c.Faction == "chateau"));
    }

    [Fact]
    public void Une_grosse_armee_bat_une_petite_avec_des_pertes()
    {
        var hero = ArmyOf("piquier", 50);
        var monsters = ArmyOf("gobelin", 20);

        var report = Battle.Resolve(hero, monsters);

        Assert.True(report.AttackerWon);
        Assert.True(monsters.IsEmpty);
        Assert.InRange(hero.TotalCount, 1, 49);
        Assert.Equal(50 - hero.TotalCount, report.AttackerLosses.Sum(s => s.Count));
        Assert.Equal(20, report.DefenderLosses.Sum(s => s.Count));
    }

    [Fact]
    public void Le_combat_donne_toujours_le_meme_resultat()
    {
        var a = Battle.Resolve(ArmyOf("archer", 15), ArmyOf("loup", 12));
        var b = Battle.Resolve(ArmyOf("archer", 15), ArmyOf("loup", 12));
        Assert.Equal(a.AttackerWon, b.AttackerWon);
        Assert.Equal(a.AttackerLosses.Sum(s => s.Count), b.AttackerLosses.Sum(s => s.Count));
    }

    [Fact]
    public void L_attaque_et_la_defense_modifient_les_degats()
    {
        Assert.Equal(1.0, Battle.DamageModifier(5, 5));
        Assert.Equal(1.5, Battle.DamageModifier(15, 5), 3);
        Assert.Equal(0.75, Battle.DamageModifier(5, 15), 3);
        Assert.Equal(0.3, Battle.DamageModifier(0, 100), 3);
    }

    [Fact]
    public void Une_armee_a_7_troupes_au_maximum()
    {
        var army = new Army();
        var types = TestData.Creatures.All.Take(8).ToList();
        for (int i = 0; i < 7; i++)
            Assert.True(army.Add(types[i], 1));
        Assert.False(army.Add(types[7], 1));
        Assert.True(army.Add(types[0], 5));
        Assert.Equal(6, army.Stacks[0].Count);
    }
}

internal static class TestData
{
    public static readonly CreatureCatalog Creatures = CreatureCatalog.FromJson(
        System.IO.File.ReadAllText(System.IO.Path.Combine(System.AppContext.BaseDirectory, "data", "creatures.json")));
}
