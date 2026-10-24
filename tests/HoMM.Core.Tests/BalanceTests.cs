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
            Assert.True(result.TownTaken, $"graine {seed} : ville non prise");
            Assert.True(result.VictoryDay is >= 20 and <= 150, $"graine {seed} : victoire au jour {result.VictoryDay}");
        }
    }

    [Fact]
    public void L_armee_de_depart_bat_les_monstres_les_plus_proches()
    {
        foreach (int seed in Seeds)
        {
            var map = new MapGenerator(seed, TestData.Data).Generate();
            var nearest = map.Objects.OfType<MonsterGuard>()
                .OrderBy(g => Math.Abs(g.Position.X - map.Start.X) + Math.Abs(g.Position.Y - map.Start.Y))
                .Take(2);
            foreach (var guard in nearest)
                Assert.True(Battle.Resolve(BalanceSimulation.StartingArmy(), guard.Army.Clone()).AttackerWon,
                    $"graine {seed} : {guard.Count} {guard.Type.Id} trop forts au départ");
        }
    }
}

/// <summary>Une partie jouée par une stratégie simple, sans déplacements (voir <see cref="BalanceTests"/>).</summary>
internal static class BalanceSimulation
{
    public sealed record Result(bool TownTaken, int VictoryDay);

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
        army.Add(TestData.Creatures.Get("piquier"), 30);
        army.Add(TestData.Creatures.Get("archer"), 12);
        army.Add(TestData.Creatures.Get("griffon"), 4);
        return army;
    }

    public static Result Play(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();
        var player = new Player("Rouge", map);
        var hero = new Hero("Test", player, map.Start);
        foreach (var stack in StartingArmy().Stacks)
            hero.Army.Add(stack.Type, stack.Count);
        var town = map.Objects.OfType<Town>().Single();

        // On commence par prendre la ville
        if (!Battle.Resolve(hero.Army, town.Garrison).AttackerWon)
            return new Result(false, -1);
        town.SetOwner(player);

        for (int day = 1; day <= MaxDays; day++)
        {
            NewDay(map, player, hero, day);
            BuildAndRecruit(town, player);
            if (day % 7 == 1) // une fois par semaine, le héros passe prendre la garnison
                while (town.Garrison.Stacks.Count > 0 && town.Garrison.MoveStackTo(0, hero.Army)) { }

            if (day % FightEveryDays != 0)
                continue;
            if (!map.Objects.OfType<MonsterGuard>().Any())
                return new Result(true, day);
            FightSafestGuard(map, hero);
        }
        return new Result(true, -1);
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

    /// <summary>Combat contre le monstre qui coûte le moins de pertes, s'il en vaut la peine. Prend ce qu'il gardait.</summary>
    private static void FightSafestGuard(AdventureMap map, Hero hero)
    {
        static double Hp(IEnumerable<CreatureStack> stacks) => stacks.Sum(s => s.Count * s.Type.HitPoints);

        var best = map.Objects.OfType<MonsterGuard>()
            .Select(g => (Guard: g, Report: Battle.Resolve(hero.Army.Clone(), g.Army.Clone())))
            .Where(x => x.Report.AttackerWon)
            .Select(x => (x.Guard, Loss: Hp(x.Report.AttackerLosses) / Hp(hero.Army.Stacks)))
            .OrderBy(x => x.Loss)
            .FirstOrDefault();
        if (best.Guard == null || best.Loss > MaxLossRatio)
            return;

        Battle.Resolve(hero.Army, best.Guard.Army);
        map.RemoveObject(best.Guard);
        foreach (var obj in map.Objects.Where(o => o is Mine or TreasureChest && IsNear(o.Position, best.Guard.Position)).ToList())
        {
            obj.Visit(hero);
            if (obj.RemovedOnVisit)
                map.RemoveObject(obj);
        }
    }

    private static bool IsNear(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= 1;
}
