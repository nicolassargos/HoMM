using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Équilibrage : une partie simplifiée est jouée par une stratégie simple, sur plusieurs cartes,
/// pour vérifier que la victoire est possible, ni trop vite ni trop lentement.
/// La simulation est optimiste (pas de trajets, choix parfait des combats) : une vraie partie
/// prend plus de temps. Si un réglage casse ces tests, c'est que l'équilibre a beaucoup changé.
/// </summary>
public class BalanceTests
{
    private static readonly int[] Seeds = { 1, 7, 42, 99, 2026 };

    [Fact]
    public void La_victoire_est_possible_en_un_a_cinq_mois()
    {
        foreach (int seed in Seeds)
        {
            var result = BalanceSimulation.Play(seed);
            Assert.True(result.TownTaken, $"graine {seed} : aucune ville prise");
            Assert.True(result.VictoryDay is >= 20 and <= 150, $"graine {seed} : victoire au jour {result.VictoryDay}");
        }
    }

    [Fact]
    public void L_armee_de_depart_bat_les_monstres_les_plus_proches()
    {
        foreach (int seed in Seeds)
        {
            var map = new MapGenerator(seed, TestData.Data).Generate();
            var nearest = MapGeneratorTests.GuardsReachableWithoutFighting(map)
                .OrderBy(g => Math.Abs(g.Position.X - map.Start.X) + Math.Abs(g.Position.Y - map.Start.Y))
                .Take(2);
            foreach (var guard in nearest)
                Assert.True(Battle.Resolve(BalanceSimulation.StartingArmy(), guard.Army.Clone()).AttackerWon,
                    $"graine {seed} : {guard.Count} {guard.Type.Id} trop forts au départ");
        }
    }
}

/// <summary>
/// Une partie jouée par une stratégie simple, sans déplacements (voir <see cref="BalanceTests"/>).
/// Le héros attaque le monstre ou la ville la moins coûteuse, où qu'ils soient : il ne recrute
/// qu'une fois une ville prise.
/// </summary>
internal static class BalanceSimulation
{
    public sealed record Result(bool TownTaken, int VictoryDay, List<int> GoldByWeek);

    /// <summary>Ordre de construction : les habitations d'abord, puis l'économie.</summary>
    private static readonly string[] BuildOrder =
    {
        "tour-archers", "taverne", "hotel-ville", "tour-griffons", "caserne", "monastere",
        "terrain-entrainement", "grand-hotel-ville", "portail-gloire",
    };

    private const int FightEveryDays = 2;      // un combat tous les deux jours (le temps d'y aller)
    private const double MaxLossRatio = 0.3;    // on n'attaque pas si on perd plus de 30 % de son armée
    private const int MaxDays = 200;

    public static Army StartingArmy()
    {
        var army = new Army();
        army.Add(TestData.Creatures.Get("paysan"), 30);
        army.Add(TestData.Creatures.Get("archer"), 15);
        army.Add(TestData.Creatures.Get("griffon"), 4);
        return army;
    }

    public static Result Play(int seed, MapSize size = MapSize.Normal)
    {
        var (width, height) = MapSizes.Dimensions(size);
        var map = new MapGenerator(seed, TestData.Data).Generate(width, height);
        var player = new Player("Rouge", map);
        var hero = new Hero("Test", player, map.Start);
        foreach (var stack in StartingArmy().Stacks)
            hero.Army.Add(stack.Type, stack.Count);
        // Le héros doit d'abord se frayer un chemin jusqu'à une ville : avant, il ne peut pas recruter
        var goldByWeek = new List<int>();
        for (int day = 1; day <= MaxDays; day++)
        {
            NewDay(map, player, hero, day);
            if (day % 7 == 0)
                goldByWeek.Add(player.Resources[ResourceType.Gold]);
            foreach (var town in player.Towns)
            {
                BuildAndRecruit(town, player);
                if (day % 7 == 1) // une fois par semaine, le héros passe prendre la garnison
                    while (town.Garrison.Stacks.Count > 0 && town.Garrison.MoveStackTo(0, hero.Army)) { }
            }

            if (day % FightEveryDays != 0)
                continue;
            if (!map.Objects.OfType<MonsterGuard>().Any())
                return new Result(player.Towns.Count > 0, day, goldByWeek);
            FightSafest(map, hero);
        }
        return new Result(player.Towns.Count > 0, -1, goldByWeek);
    }

    private static void NewDay(AdventureMap map, Player player, Hero hero, int day)
    {
        hero.NewDay();
        foreach (var town in map.Objects.OfType<Town>())
        {
            town.NewDay();
            if (day % 7 == 0)
                town.NewWeek();
        }
        if (day % 7 == 0)
            foreach (var guard in map.Objects.OfType<MonsterGuard>())
                guard.GrowWeekly();
        foreach (var (resource, amount) in player.DailyIncome())
            player.Resources[resource] += amount;
    }

    private static void BuildAndRecruit(Town town, Player player)
    {
        foreach (var id in BuildOrder)
        {
            var building = TestData.Data.Buildings.Get(id);
            if (town.CanBuild(building, player) == BuildResult.Ok)
            {
                town.Build(building, player);
                break;
            }
        }
        foreach (var type in town.Available.Keys.OrderByDescending(t => t.Tier).ToList())
        {
            int count = town.MaxRecruitable(type, player);
            if (count <= 0 || !town.Garrison.CanReceive(type))
                continue;
            player.Pay(type.Cost.ToDictionary(c => c.Key, c => c.Value * count));
            town.Available[type] -= count;
            town.Garrison.Add(type, count);
        }
    }

    /// <summary>
    /// Combat contre le monstre ou la garnison de ville qui coûte le moins de pertes, s'il en vaut la peine.
    /// Prend ce que le monstre gardait, ou la ville.
    /// </summary>
    private static void FightSafest(AdventureMap map, Hero hero)
    {
        static double Hp(IEnumerable<CreatureStack> stacks) => stacks.Sum(s => s.Count * s.Type.HitPoints);

        var targets = map.Objects.OfType<MonsterGuard>().Select(g => (Target: (MapObject)g, Army: g.Army))
            .Concat(map.Objects.OfType<Town>().Where(t => t.Owner != hero.Owner).Select(t => (Target: (MapObject)t, Army: t.Garrison)));
        var best = targets
            .Select(x => (x.Target, x.Army, Report: Battle.Resolve(hero.Army.Clone(), x.Army.Clone())))
            .Where(x => x.Report.AttackerWon)
            .Select(x => (x.Target, x.Army, Loss: Hp(x.Report.AttackerLosses) / Hp(hero.Army.Stacks)))
            .OrderBy(x => x.Loss)
            .FirstOrDefault();
        if (best.Target == null || best.Loss > MaxLossRatio)
            return;

        Battle.Resolve(hero.Army, best.Army);
        if (best.Target is Town town)
        {
            town.SetOwner(hero.Owner);
            return;
        }
        map.RemoveObject(best.Target);
        foreach (var obj in map.Objects.Where(o => o is Mine or TreasureChest && IsNear(o.Position, best.Target.Position)).ToList())
        {
            obj.Visit(hero);
            if (obj.RemovedOnVisit)
                map.RemoveObject(obj);
        }
    }

    private static bool IsNear(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= 1;
}
