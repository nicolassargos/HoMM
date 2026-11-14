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

    // sur la petite carte (36 × 36, rapide à simuler) : la victoire est de prendre toutes les villes depuis le 2026-11-14 ;
    // elle n'en a que deux (la sienne et une neutre), prises vers le jour 15 (la normale, 72 × 72 : vers le jour 210)
    [Fact]
    public void La_victoire_est_possible_sur_la_petite_carte()
    {
        foreach (int seed in Seeds)
        {
            var result = BalanceSimulation.Play(seed, MapSize.Small);
            Assert.True(result.TownTaken, $"graine {seed} : aucune ville prise");
            Assert.True(result.VictoryDay is >= 5 and <= 60, $"graine {seed} : victoire au jour {result.VictoryDay}");
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
    public sealed record Result(bool TownTaken, int VictoryDay, List<int> GoldByWeek, int Level = 1, int SpellsCast = 0);

    /// <summary>
    /// Le héros de la simulation : <see cref="None"/>, comme depuis l'étape 2 (ses caractéristiques ne comptent pas, il ne
    /// monte pas de niveau) ; <see cref="Might"/>, il gagne de l'expérience et monte en attaque et en défense ;
    /// <see cref="Magic"/>, il monte en puissance et en savoir, bâtit la guilde des mages et lance des sorts.
    /// </summary>
    public enum Style { None, Might, Magic }

    /// <summary>Ordre de construction : les habitations d'abord, puis l'économie.</summary>
    private static readonly string[] BuildOrder =
    {
        "tour-archers", "taverne", "hotel-ville", "tour-griffons", "caserne", "monastere",
        "terrain-entrainement", "grand-hotel-ville", "portail-gloire",
    };

    /// <summary>
    /// Les listes nomment les bâtiments du Château ; dans une ville d'une autre faction, une habitation du Château devient
    /// celle du même niveau de cette faction (les bâtiments communs ne changent pas).
    /// </summary>
    private static string? ForTown(Town town, string id)
    {
        var building = TestData.Data.Buildings.Get(id);
        if (building.Faction == town.Faction || building.Faction == BuildingType.AnyFaction)
            return id;
        int tier = building.Dwelling is { } creature ? TestData.Creatures.Get(creature).Tier : 0;
        return town.DwellingOfTier(tier)?.Id;
    }

    private const int FightEveryDays = 2;      // un combat tous les deux jours (le temps d'y aller)
    private const double MaxLossRatio = 0.3;    // on n'attaque pas si on perd plus de 30 % de son armée
    private const int MaxDays = 400;

    public static Army StartingArmy()
    {
        var army = new Army();
        army.Add(TestData.Creatures.Get("paysan"), 30);
        army.Add(TestData.Creatures.Get("archer"), 15);
        army.Add(TestData.Creatures.Get("griffon"), 4);
        return army;
    }

    /// <summary>
    /// Pour le héros de magie, la guilde des mages tôt, comme le ferait un joueur qui mise sur la magie : le niveau 1
    /// d'abord (le bois et le minerai du départ ne suffisent pas à tout : la simulation ne ramasse pas les tas), chaque
    /// niveau suivant dès que ce qu'il demande est là.
    /// </summary>
    private static readonly string[] MagicBuildOrder =
    {
        "guilde-mages-1", "tour-archers", "guilde-mages-2", "taverne", "hotel-ville", "guilde-mages-3", "tour-griffons",
        "caserne", "monastere", "terrain-entrainement", "grand-hotel-ville", "guilde-mages-4", "portail-gloire",
    };

    public static Result Play(int seed, MapSize size = MapSize.Normal, Style style = Style.None)
    {
        var rng = new Random(seed);
        int spellsCast = 0;
        var (width, height) = MapSizes.Dimensions(size);
        var map = new MapGenerator(seed, TestData.Data).Generate(width, height);
        var player = new Player("Rouge", map);
        var hero = new Hero("Test", player, map.Start);
        foreach (var stack in StartingArmy().Stacks)
            hero.Army.Add(stack.Type, stack.Count);
        // Le premier jour, il prend les mines sans gardien près de son départ (dont celle de la ressource principale de sa
        // faction, le cristal du Château), comme le ferait un joueur
        foreach (var mine in map.Objects.OfType<Mine>()
                     .Where(m => Math.Max(Math.Abs(m.Position.X - map.Start.X), Math.Abs(m.Position.Y - map.Start.Y)) <= 9)
                     .Where(m => !map.Objects.OfType<MonsterGuard>().Any(g => IsNear(g.Position, m.Position)))
                     .ToList())
            mine.Visit(hero);
        // Le héros doit d'abord se frayer un chemin jusqu'à une ville : avant, il ne peut pas recruter
        var goldByWeek = new List<int>();
        for (int day = 1; day <= MaxDays; day++)
        {
            NewDay(map, player, hero, day);
            if (day % 7 == 0)
                goldByWeek.Add(player.Resources[ResourceType.Gold]);
            foreach (var town in player.Towns)
            {
                BuildAndRecruit(town, player, style, rng);
                if (day % 7 == 1) // une fois par semaine, le héros passe prendre la garnison (et à la guilde)
                {
                    TakeBestTroops(hero, town);
                    if (town.MageGuildLevel > 0)
                    {
                        foreach (var spell in town.GuildSpells)
                            hero.Learn(spell);
                        hero.SpellPoints = Math.Max(hero.SpellPoints, hero.MaxSpellPoints);
                    }
                }
            }

            if (day % FightEveryDays != 0)
                continue;
            // la victoire : toutes les villes (« vaincre tous les monstres » jusqu'au 2026-11-14)
            if (map.Objects.OfType<Town>().All(t => t.Owner == player))
                return new Result(player.Towns.Count > 0, day, goldByWeek, hero.Level, spellsCast);
            spellsCast += FightSafest(map, hero, style);
        }
        return new Result(player.Towns.Count > 0, -1, goldByWeek, hero.Level, spellsCast);
    }

    /// <summary>
    /// Le héros prend la garnison : il garde les 7 troupes qui valent le plus (en or), le reste attend en ville. Avec des
    /// villes de plusieurs factions, il y a plus de sortes de créatures que de places dans l'armée.
    /// </summary>
    private static void TakeBestTroops(Hero hero, Town town)
    {
        var all = hero.Army.Stacks.Concat(town.Garrison.Stacks).Where(st => st.Count > 0)
            .GroupBy(st => st.Type).Select(g => (Type: g.Key, Count: g.Sum(st => st.Count)))
            .OrderByDescending(t => t.Count * t.Type.Cost.GetValueOrDefault(ResourceType.Gold)).ToList();
        foreach (var stack in hero.Army.Stacks.Concat(town.Garrison.Stacks))
            stack.Count = 0;
        hero.Army.RemoveEmpty();
        town.Garrison.RemoveEmpty();
        foreach (var (type, count) in all)
            if (!hero.Army.Add(type, count))
                town.Garrison.Add(type, count);
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

    private static void BuildAndRecruit(Town town, Player player, Style style, Random rng)
    {
        foreach (var id in (style == Style.Magic ? MagicBuildOrder : BuildOrder).Select(id => ForTown(town, id)))
        {
            if (id == null)
                continue;
            var building = TestData.Data.Buildings.Get(id);
            if (town.CanBuild(building, player) == BuildResult.Ok)
            {
                town.Build(building, player);
                if (building.MageGuild > 0)
                    town.DrawGuildSpells(building.MageGuild, TestData.Data.Spells, rng.Next);
                break;
            }
        }
        // le héros de magie met de côté l'or du prochain niveau de la guilde (sinon les recrues prennent tout, chaque jour)
        int reserve = style == Style.Magic
            ? town.Buildings.Where(b => b.MageGuild > 0 && !town.Has(b.Id) && b.Requires.All(town.Has))
                .Select(b => b.Cost.GetValueOrDefault(ResourceType.Gold)).DefaultIfEmpty(0).First()
            : 0;
        foreach (var type in town.Available.Keys.OrderByDescending(t => t.Tier).ToList())
        {
            int gold = type.Cost.GetValueOrDefault(ResourceType.Gold);
            int count = Math.Min(town.MaxRecruitable(type, player),
                gold > 0 ? Math.Max(0, player.Resources[ResourceType.Gold] - reserve) / gold : int.MaxValue);
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
    /// <returns>Les points de sort dépensés (0 sans magie).</returns>
    private static int FightSafest(AdventureMap map, Hero hero, Style style)
    {
        // le héros compte (son attaque, sa défense, ses sorts) sauf dans la simulation d'origine
        var leader = style == Style.None ? null : hero;
        static double Hp(IEnumerable<CreatureStack> stacks) => stacks.Sum(s => s.Count * s.Type.HitPoints);

        var targets = map.Objects.OfType<MonsterGuard>().Select(g => (Target: (MapObject)g, Army: g.Army))
            .Concat(map.Objects.OfType<Town>().Where(t => t.Owner != hero.Owner).Select(t => (Target: (MapObject)t, Army: t.Garrison)));
        var best = targets
            .Select(x => (x.Target, x.Army, Report: Battle.Resolve(hero.Army.Clone(), x.Army.Clone(), leader)))
            .Where(x => x.Report.AttackerWon)
            .Select(x => (x.Target, x.Army, Loss: Hp(x.Report.AttackerLosses) / Hp(hero.Army.Stacks)))
            .OrderBy(x => x.Loss)
            .FirstOrDefault();
        if (best.Target == null || best.Loss > MaxLossRatio)
            return 0;

        var report = Battle.Resolve(hero.Army, best.Army, leader);
        if (leader != null)
        {
            // l'expérience et les niveaux : attaque et défense, ou puissance et savoir, tour à tour
            hero.SpendSpellPoints(report.AttackerSpellPointsSpent);
            hero.AddExperience(report.Experience);
            while (hero.Experience >= Hero.ExperienceForLevel(hero.Level + 1))
                hero.LevelUp(style == Style.Magic
                    ? (hero.Level % 2 == 0 ? HeroStat.Knowledge : HeroStat.Power)
                    : (hero.Level % 2 == 0 ? HeroStat.Defense : HeroStat.Attack));
        }
        if (best.Target is Town town)
        {
            town.SetOwner(hero.Owner);
            return report.AttackerSpellPointsSpent;
        }
        map.RemoveObject(best.Target);
        foreach (var obj in map.Objects.Where(o => o is Mine or TreasureChest && IsNear(o.Position, best.Target.Position)).ToList())
        {
            if (obj is TreasureChest chest) // la simulation prend l'or du coffre
                hero.Owner.Resources[ResourceType.Gold] += chest.Gold;
            else
                obj.Visit(hero);
            if (obj.RemovedOnVisit)
                map.RemoveObject(obj);
        }
        return report.AttackerSpellPointsSpent;
    }

    private static bool IsNear(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= 1;
}
