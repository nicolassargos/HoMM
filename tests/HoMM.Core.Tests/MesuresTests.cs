using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;
using Xunit.Abstractions;

namespace HoMM.Core.Tests;

/// <summary>
/// Une mesure : un test qui ne vérifie rien, mais écrit des chiffres (l'équilibre, la force des adversaires…), pour
/// comparer avant et après un changement. Ignorée par un « dotnet test » ordinaire (elle est longue) ; pour la lancer :
/// <c>HOMM_MESURES=1 dotnet test --filter Category=Mesures --logger "console;verbosity=detailed"</c>.
/// </summary>
public sealed class MesureAttribute : FactAttribute
{
    public MesureAttribute()
    {
        if (Environment.GetEnvironmentVariable("HOMM_MESURES") != "1")
            Skip = "Mesure : lancer avec HOMM_MESURES=1 (voir MesuresTests)";
    }
}

/// <summary>
/// Les mesures de l'équilibre (voir <see cref="MesureAttribute"/>). Les mêmes graines à chaque fois : deux mesures se
/// comparent ligne à ligne. Noter les chiffres retenus dans PLAN.md (Repères) ou HISTORIQUE.md.
/// </summary>
[Trait("Category", "Mesures")]
public class MesuresTests(ITestOutputHelper output)
{
    /// <summary>Les cartes mesurées (toujours les mêmes, pour comparer).</summary>
    private static readonly int[] Seeds = { 3, 7, 11, 19, 42, 77 };

    private static string Range(IReadOnlyCollection<int> values) =>
        values.Count == 0 ? "-" : $"moyenne {values.Average():0}, de {values.Min()} à {values.Max()}";

    /// <summary>La simulation seule (<see cref="BalanceSimulation"/>) : le jour de la victoire, sur chaque taille de carte.</summary>
    [Mesure]
    public void Simulation_seule()
    {
        foreach (var size in new[] { MapSize.Small, MapSize.Normal, MapSize.Large })
        {
            var days = Seeds.Select(seed => BalanceSimulation.Play(seed, size).VictoryDay).ToList();
            var won = days.Where(d => d > 0).ToList();
            output.WriteLine($"{size} : victoire {won.Count} fois sur {days.Count}, jour {Range(won)} ({string.Join(" ", days)})");
        }
    }

    /// <summary>
    /// La simulation seule avec un héros qui monte de niveau : en force (attaque, défense) ou en magie (puissance,
    /// savoir, guilde des mages, sorts), sur une carte normale : la magie doit être un choix aussi bon que la force.
    /// </summary>
    [Mesure]
    public void Simulation_force_ou_magie()
    {
        foreach (var style in new[] { BalanceSimulation.Style.Might, BalanceSimulation.Style.Magic })
        {
            var results = Seeds.Select(seed => BalanceSimulation.Play(seed, MapSize.Normal, style)).ToList();
            var won = results.Where(r => r.VictoryDay > 0).Select(r => r.VictoryDay).ToList();
            output.WriteLine($"{style} : victoire {won.Count} fois sur {results.Count}, jour {Range(won)} "
                             + $"({string.Join(" ", results.Select(r => r.VictoryDay))}), niveau {Range(results.Select(r => r.Level).ToList())}, "
                             + $"points de sort dépensés {Range(results.Select(r => r.SpellsCast).ToList())}");
        }
    }

    /// <summary>
    /// Un adversaire seul face au joueur (qui ne joue pas et ne se fait pas prendre), six semaines, à chaque niveau :
    /// ses villes, ses héros, ses combats gagnés, la valeur de son armée (or), ses sorts.
    /// </summary>
    [Mesure]
    public void Adversaires_en_six_semaines()
    {
        foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Normal, Difficulty.Hard })
        {
            var values = new List<int>();
            int towns = 0, heroes = 0, battles = 0, defeated = 0, mages = 0, guilds = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1);
                state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents, Difficulty: difficulty));
                state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50);
                PlayDays(state, 42);
                var rival = state.Players[1];
                values.Add(rival.Heroes.Sum(h => Battle.ExperienceFor(h.Army.Stacks))
                           + rival.Towns.Sum(t => Battle.ExperienceFor(t.Garrison.Stacks)));
                towns += rival.Towns.Count;
                heroes += rival.Heroes.Count;
                battles += rival.Statistics.BattlesWon;
                defeated += rival.IsDefeated ? 1 : 0;
                mages += rival.Heroes.Count(h => h.Spells.Count > 0);
                guilds += rival.Towns.Count(t => t.MageGuildLevel > 0);
            }
            int n = Seeds.Length;
            output.WriteLine($"{difficulty} : armée {Range(values)} ; par partie : {towns / (double)n:0.0} villes, "
                             + $"{heroes / (double)n:0.0} héros ({mages / (double)n:0.0} avec des sorts), "
                             + $"{guilds / (double)n:0.0} guildes, {battles / (double)n:0.0} combats gagnés ; éliminé {defeated} fois");
        }
    }

    /// <summary>
    /// Les rivières de chaque type de carte (carte normale) : leur part de la terre (Heroes 3 : 2,3 % en médiane, de 0 à
    /// 7 %), les cases de rivière entre les zones, les ponts, les ruisseaux et leurs cases, les routes qui croisent un
    /// ruisseau.
    /// </summary>
    [Mesure]
    public void Rivieres_par_type_de_carte()
    {
        foreach (var type in Enum.GetValues<MapType>())
        {
            var shares = new List<int>();
            int rivers = 0, bridges = 0, streams = 0, streamCells = 0, fords = 0, joins = 0, ponds = 0, fromHills = 0, fromSwamps = 0, springs = 0;
            foreach (int seed in Seeds)
            {
                var map = new GameState(seed, TestData.Data, MapSize.Normal, access: ZoneAccess.Normal, mapType: type).Map;
                var cells = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Cell(x, y))).ToList();
                int land = cells.Count(c => !map.IsWater(c));
                var kinds = cells.Select(map.GetRiver).ToList();
                shares.Add((int)Math.Round(1000.0 * kinds.Count(k => k != RiverKind.None) / land));
                rivers += kinds.Count(k => k == RiverKind.River);
                bridges += kinds.Count(k => k == RiverKind.Bridge) / 2;
                var stream = cells.Where(c => map.GetRiver(c) == RiverKind.Stream).ToHashSet();
                streamCells += stream.Count;
                fords += stream.Count(map.HasRoad);
                // un ruisseau : un tracé, de sa source à son bout
                streams += map.Streams.Count;
                joins += map.Streams.Count(s => s.End == StreamEnd.Join);
                ponds += map.Streams.Count(s => s.End == StreamEnd.Pond);
                fromHills += map.Streams.Count(s => s.Source == StreamSource.Mountain);
                fromSwamps += map.Streams.Count(s => s.Source == StreamSource.Swamp);
                springs += map.Streams.Count(s => s.Source == StreamSource.Spring);
            }
            int n = Seeds.Length;
            output.WriteLine($"{type} : rivières et ruisseaux {Range(shares)} ‰ de la terre ; par carte : {rivers / (double)n:0} cases de rivière, "
                             + $"{bridges / (double)n:0.0} ponts, {streams / (double)n:0.0} ruisseaux ({streamCells / (double)n:0} cases ; "
                             + $"{joins / (double)n:0.0} affluents, {ponds / (double)n:0.0} finis dans une mare ; nés au pied d'une montagne {fromHills / (double)n:0.0}, "
                             + $"d'un marais {fromSwamps / (double)n:0.0}, d'une source {springs / (double)n:0.0}), "
                             + $"{fords / (double)n:0.0} routes qui croisent un ruisseau");
        }
    }

    /// <summary>
    /// Les zones telles que le joueur les voit (2026-11-20, à comparer avec Heroes 3 : tools/cartes/zones_h3.py) : les
    /// régions où l'on marche, séparées par ce qui bloque et par les monstres (leur case et ses 8 voisines comptent comme un
    /// mur ; un objet aussi, comme dans Heroes 3 où on le visite d'à côté) ; les régions d'au moins 20 cases.
    /// </summary>
    [Mesure]
    public void Zones_vues_du_joueur()
    {
        foreach (var size in new[] { MapSize.Small, MapSize.Normal, MapSize.Large, MapSize.Huge })
        {
            var townCounts = new List<int>();
            var mineCounts = new List<int>();
            var blocked = new List<int>();
            var weighted = new List<int>();
            var counts = new List<int>();
            var all = new List<int>();
            var big = new List<int>();
            foreach (int seed in Seeds)
            {
                var map = new GameState(seed, TestData.Data, size).Map;
                townCounts.Add(map.Objects.OfType<Town>().Count());
                mineCounts.Add(map.Objects.OfType<Mine>().Count());
                var land = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Cell(x, y)))
                    .Where(c => !map.IsWater(c)).ToList();
                blocked.Add(100 * land.Count(c => map.GetTerrain(c) is TerrainType.Mountain or TerrainType.Forest) / land.Count);
                var wall = new bool[map.Width, map.Height];
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var c = new Cell(x, y);
                    wall[x, y] = !map.IsPassable(c) || map.ObjectAt(c) is { } o && o is not Town;
                }
                foreach (var guard in map.Objects.OfType<MonsterGuard>())
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (map.InBounds(guard.Position.Offset(dx, dy)))
                            wall[guard.Position.X + dx, guard.Position.Y + dy] = true;
                var seen = new bool[map.Width, map.Height];
                var sizes = new List<int>();
                for (int y0 = 0; y0 < map.Height; y0++)
                for (int x0 = 0; x0 < map.Width; x0++)
                {
                    if (wall[x0, y0] || seen[x0, y0])
                        continue;
                    seen[x0, y0] = true;
                    var stack = new Stack<Cell>(new[] { new Cell(x0, y0) });
                    int n = 0;
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        n++;
                        for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var next = c.Offset(dx, dy);
                            if (map.InBounds(next) && !wall[next.X, next.Y] && !seen[next.X, next.Y])
                            {
                                seen[next.X, next.Y] = true;
                                stack.Push(next);
                            }
                        }
                    }
                    if (n >= 20)
                        sizes.Add(n);
                }
                int free = sizes.Sum(), acc = 0;
                foreach (var v in sizes.OrderBy(v => v))
                    if ((acc += v) * 2 >= free)
                    {
                        weighted.Add(v);
                        break;
                    }
                counts.Add(sizes.Count);
                all.AddRange(sizes);
                big.AddRange(sizes.Where(v => v >= 150));
            }
            string Q(List<int> v) => v.Count == 0 ? "-" : $"min {v.Min()} médiane {v.OrderBy(x => x).ElementAt(v.Count / 2)} max {v.Max()} (n={v.Count})";
            output.WriteLine($"{size} : régions par carte {Q(counts)} ; taille {Q(all)} ; villes {Q(townCounts)} ; mines {Q(mineCounts)} ; montagne et forêt {Q(blocked)} % de la terre");
            output.WriteLine($"  la région d'une case typique {Q(weighted)} ; zones d'au moins 150 cases {Q(big)}");
        }
    }

    /// <summary>
    /// La force des gardiens des portails (2026-11-21, l'utilisateur : « much harder than most armies ») : en or à vaincre
    /// (<see cref="CreatureStrength.GoldToBeat"/>), comparée aux autres monstres de la carte (la part qui est plus faible)
    /// et au plus fort de ceux qui sont à 15 cases au plus.
    /// </summary>
    [Mesure]
    public void Gardiens_des_portails()
    {
        var creatures = TestData.Data.Creatures;
        foreach (var size in new[] { MapSize.Normal, MapSize.Large })
        {
            var weaker = new List<double>();
            var versusNear = new List<double>();
            var shown = new List<string>();
            foreach (int seed in Seeds)
            {
                var (width, height) = MapSizes.Dimensions(size);
                var map = new MapGenerator(seed, TestData.Data) { Players = 2, Access = ZoneAccess.Normal }.Generate(width, height);
                double Strength(MonsterGuard g) => CreatureStrength.GoldToBeat(g.Type, creatures) * g.Count;
                var guards = map.Objects.OfType<MonsterGuard>().Where(g => !g.Type.Sea).ToList();
                foreach (var portal in map.Objects.OfType<Portal>())
                {
                    if (guards.FirstOrDefault(g => Math.Abs(g.Position.X - portal.Position.X) <= 1 && g.Position.Y == portal.Position.Y + 1) is not { } keeper)
                        continue; // celui du dedans
                    double s = Strength(keeper);
                    var others = guards.Where(g => g != keeper).ToList();
                    weaker.Add(100.0 * others.Count(g => Strength(g) < s) / others.Count);
                    var near = others.Where(g => Math.Max(Math.Abs(g.Position.X - keeper.Position.X), Math.Abs(g.Position.Y - keeper.Position.Y)) <= 15).ToList();
                    if (near.Count > 0)
                        versusNear.Add(s / near.Max(Strength));
                    if (shown.Count < 6)
                        shown.Add($"{keeper.Count} {keeper.Type.Id} ({s:0})");
                }
            }
            output.WriteLine($"{size} : {weaker.Count} gardiens ; plus forts que {Median(weaker):0} % des monstres de la carte (min {Min(weaker):0}) ; "
                + $"{Median(versusNear):0.0} fois le plus fort à 15 cases (min {Min(versusNear):0.0}) ; ex. {string.Join(", ", shown)}");
        }
        static double Median(List<double> v) => v.Count == 0 ? 0 : v.OrderBy(x => x).ElementAt(v.Count / 2);
        static double Min(List<double> v) => v.Count == 0 ? 0 : v.Min();
    }

    /// <summary>
    /// Les poches de terre où aucun héros n'entre (2026-11-21, remarque de l'utilisateur : des régions vides sur certaines
    /// cartes) : les cases libres et praticables que l'on n'atteint pas depuis le premier départ, à pied (en battant les
    /// monstres, sans traverser les objets, par les portails) ou en bateau ; leur nombre et leur taille, et les plus grandes ;
    /// ce qu'en font les trouées dans la forêt, les arbres et les portails.
    /// </summary>
    [Mesure]
    public void Poches_inaccessibles()
    {
        foreach (var type in new[] { MapType.Continent, MapType.Seas, MapType.Islands, MapType.Archipelago })
        foreach (var size in new[] { MapSize.Small, MapSize.Normal, MapSize.Large })
        {
            var share = new List<double>();
            var pockets = new List<int>();
            var sizes = new List<int>();
            var biggest = new List<string>();
            var opened = new List<double>();
            var filled = new List<double>();
            var portals = new List<int>();
            var rewards = new List<int>();
            foreach (int seed in Seeds)
            {
                var generator = new MapGenerator(seed, TestData.Data) { Players = 2, Access = ZoneAccess.Normal, Type = type };
                var (width, height) = MapSizes.Dimensions(size);
                var map = generator.Generate(width, height);
                bool Open(Cell c) => map.InBounds(c) && (map.IsPassable(c) || type != MapType.Continent && map.IsWater(c));
                var seen = new bool[map.Width, map.Height];
                var queue = new Queue<Cell>(new[] { map.Start });
                var exits = new HashSet<Cell>();
                seen[map.Start.X, map.Start.Y] = true;
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    if (c != map.Start && !exits.Contains(c) && map.ObjectAt(c) is { } o && o is not MonsterGuard and not Boat)
                    {
                        // un portail mène à l'autre
                        if (o is Portal portal && !seen[portal.Exit.X, portal.Exit.Y])
                        {
                            seen[portal.Exit.X, portal.Exit.Y] = true;
                            exits.Add(portal.Exit);
                            queue.Enqueue(portal.Exit);
                        }
                        continue;
                    }
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = c.Offset(dx, dy);
                        if (!Open(n) || seen[n.X, n.Y] || dx != 0 && dy != 0 && !(Open(c.Offset(dx, 0)) && Open(c.Offset(0, dy))))
                            continue;
                        seen[n.X, n.Y] = true;
                        queue.Enqueue(n);
                    }
                }
                // (un ruisseau qui traverse une forêt n'est pas de la terre perdue)
                bool Lost(Cell c) => map.InBounds(c) && map.IsPassable(c) && !seen[c.X, c.Y] && map.ObjectAt(c) == null && map.GetRiver(c) == RiverKind.None;
                int land = 0, lost = 0;
                var done = new bool[map.Width, map.Height];
                var found = new List<(int Size, Cell At)>();
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var c0 = new Cell(x, y);
                    if (!map.IsWater(c0))
                        land++;
                    if (!Lost(c0) || done[x, y])
                        continue;
                    done[x, y] = true;
                    var stack = new Stack<Cell>(new[] { c0 });
                    int n = 0;
                    while (stack.Count > 0)
                    {
                        var c = stack.Pop();
                        n++;
                        for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            var next = c.Offset(dx, dy);
                            if (Lost(next) && !done[next.X, next.Y])
                            {
                                done[next.X, next.Y] = true;
                                stack.Push(next);
                            }
                        }
                    }
                    lost += n;
                    found.Add((n, c0));
                }
                share.Add(100.0 * lost / land);
                opened.Add(100.0 * generator.PocketCellsOpened / land);
                filled.Add(100.0 * generator.PocketCellsFilled / land);
                portals.Add(generator.PortalCount);
                rewards.Add(generator.PortalRewards);
                pockets.Add(found.Count(f => f.Size >= 4));
                sizes.AddRange(found.Select(f => f.Size));
                biggest.AddRange(found.OrderByDescending(f => f.Size).Take(2).Select(f => $"{f.Size} en {f.At} (graine {seed})"));
            }
            string Q(List<int> v) => v.Count == 0 ? "-" : $"min {v.Min()} médiane {v.OrderBy(x => x).ElementAt(v.Count / 2)} max {v.Max()} (n={v.Count})";
            output.WriteLine($"{type} {size} : terre perdue {string.Join(" ", share.Select(v => $"{v:0.0}"))} % ; poches de 4 cases et plus par carte {Q(pockets)} ; taille {Q(sizes)}");
            output.WriteLine($"  ouvertes {string.Join(" ", opened.Select(v => $"{v:0.0}"))} % ; remplies d'arbres {string.Join(" ", filled.Select(v => $"{v:0.0}"))} % ; portails {string.Join(" ", portals)} ; récompenses {string.Join(" ", rewards)}");
            output.WriteLine($"  les plus grandes : {string.Join(", ", biggest.OrderByDescending(b => int.Parse(b.Split(' ')[0])).Take(4))}");
        }
    }

    /// <summary>
    /// Les détours des routes (2026-11-20, à comparer avec Heroes 3 : tools/cartes/sinuosite_h3.py) : entre deux cases de
    /// route distantes de 10 à 20 cases, le chemin le plus court par la route (√2 en diagonale) divisé par la distance à vol
    /// d'oiseau ; 1 : tout droit.
    /// </summary>
    [Mesure]
    public void Detours_des_routes()
    {
        foreach (var (near, far) in new[] { (5, 8), (10, 20) })
        {
        var all = new List<double>();
        var perMap = new List<double>();
        foreach (int seed in Seeds)
        {
            var map = new GameState(seed, TestData.Data, MapSize.Normal).Map;
            var road = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Cell(x, y)))
                .Where(map.HasRoad).ToList();
            var set = road.ToHashSet();
            var rng = new System.Random(7);
            var ratios = new List<double>();
            for (int tries = 0; ratios.Count < 150 && tries < 4500; tries++)
            {
                var a = road[rng.Next(road.Count)];
                var b = road[rng.Next(road.Count)];
                double d = System.Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                if (d < near || d > far)
                    continue;
                var best = new Dictionary<Cell, double> { [a] = 0 };
                var queue = new PriorityQueue<Cell, double>();
                queue.Enqueue(a, 0);
                double? found = null;
                while (queue.TryDequeue(out var c, out double g))
                {
                    if (c == b)
                    {
                        found = g;
                        break;
                    }
                    if (g > best[c] || g > 3 * d)
                        continue;
                    for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        var n = c.Offset(dx, dy);
                        if ((dx == 0 && dy == 0) || !set.Contains(n))
                            continue;
                        double ng = g + (dx != 0 && dy != 0 ? 1.4142 : 1);
                        if (ng < best.GetValueOrDefault(n, double.MaxValue))
                        {
                            best[n] = ng;
                            queue.Enqueue(n, ng);
                        }
                    }
                }
                if (found is { } f)
                    ratios.Add(f / d);
            }
            ratios.Sort();
            perMap.Add(ratios[ratios.Count / 2]);
            all.AddRange(ratios);
        }
        all.Sort();
        perMap.Sort();
        output.WriteLine($"de {near} à {far} cases : détour médian par carte : min {perMap[0]:F3} médiane {perMap[perMap.Count / 2]:F3} max {perMap[^1]:F3}");
        output.WriteLine($"toutes les paires : quart {all[all.Count / 4]:F3} médiane {all[all.Count / 2]:F3} trois quarts {all[3 * all.Count / 4]:F3} 90 % {all[9 * all.Count / 10]:F3}");
        }
    }

    /// <summary>
    /// Les objets de la carte (2026-11-20) : les mines prévues et posées, par ressource ; les coffres et les tas prévus et
    /// posés ; et, zone par zone (zones de terre hors départs), combien de mines, de trésors (coffres et tas) et de lieux.
    /// </summary>
    [Mesure]
    public void Objets_par_zone()
    {
        foreach (var size in new[] { MapSize.Normal, MapSize.Large })
        {
            var (w, h) = MapSizes.Dimensions(size);
            var missingMines = new List<int>();
            var missingChests = new List<int>();
            var missingPiles = new List<int>();
            var perResource = new Dictionary<ResourceType, List<int>>();
            var zoneMines = new List<int>();
            var zoneMineKinds = new List<int>();
            var zoneTreasures = new List<int>();
            var zoneSites = new List<int>();
            var zoneTreasureDensity = new List<int>();
            foreach (int seed in Seeds)
            {
                var generator = new MapGenerator(seed, TestData.Data);
                var map = generator.Generate(w, h);
                var mines = map.Objects.OfType<Mine>().ToList();
                missingMines.Add(generator.MinesNotPlaced);
                missingChests.Add(generator.PlannedChests - map.Objects.OfType<TreasureChest>().Count());
                missingPiles.Add(generator.PlannedPiles - map.Objects.OfType<ResourcePile>().Count());
                foreach (var resource in ResourceInfo.All)
                    (perResource.TryGetValue(resource, out var l) ? l : perResource[resource] = new List<int>()).Add(mines.Count(m => m.Resource == resource));
                var cells = Enumerable.Range(0, h).SelectMany(y => Enumerable.Range(0, w).Select(x => new Cell(x, y))).ToList();
                for (int zone = 0; zone < generator.ZoneCount; zone++)
                {
                    if (generator.IsSeaZone(zone) || generator.IsStartZone(zone))
                        continue;
                    var objects = map.Objects.Where(o => generator.ZoneIndexOf(o.Position) == zone).ToList();
                    int land = cells.Count(c => generator.ZoneIndexOf(c) == zone && !map.IsWater(c));
                    zoneMines.Add(objects.Count(o => o is Mine));
                    zoneMineKinds.Add(objects.OfType<Mine>().Select(m => m.Resource).Distinct().Count());
                    int treasures = objects.Count(o => o is TreasureChest or ResourcePile);
                    zoneTreasures.Add(treasures);
                    zoneTreasureDensity.Add(land == 0 ? 0 : 1000 * treasures / land);
                    zoneSites.Add(objects.Count(o => o is Shrine or SageHut or WeeklyProducer or BlessingSite or ObservationTower or Obelisk));
                }
            }
            string Q(List<int> v) => v.Count == 0 ? "-" : $"min {v.Min()} médiane {v.OrderBy(x => x).ElementAt(v.Count / 2)} max {v.Max()}";
            output.WriteLine($"{size} : mines manquantes {Q(missingMines)} ; coffres manquants {Q(missingChests)} ; tas manquants {Q(missingPiles)}");
            output.WriteLine("  mines par ressource : " + string.Join(", ", perResource.Select(r => $"{r.Key} {Q(r.Value)}")));
            output.WriteLine($"  par zone : sortes de mines {Q(zoneMineKinds)} ; mines {Q(zoneMines)} (sans mine : {zoneMines.Count(m => m == 0)} sur {zoneMines.Count}) ; "
                + $"trésors {Q(zoneTreasures)} ; trésors pour 1000 cases {Q(zoneTreasureDensity)} ; lieux {Q(zoneSites)} (sans lieu : {zoneSites.Count(s => s == 0)})");
        }
    }

    /// <summary>
    /// Les objets comme les mesure tools/cartes/objets_h3.py dans Heroes 3 (2026-11-28) : combien de chaque sorte pour 1000
    /// cases de terre, la part gardée (un monstre à 2 cases au plus de l'entrée), les trésors en groupes (un autre à 3 cases
    /// au plus), les monstres loin de tout objet, la distance d'un trésor à la ville la plus proche. Quartiles bas / médiane
    /// / haut, une valeur par carte.
    /// </summary>
    [Mesure]
    public void Objets_comme_Heroes3()
    {
        foreach (var size in new[] { MapSize.Normal, MapSize.Large })
        {
            var per1000 = new Dictionary<string, List<double>>();
            var guarded = new Dictionary<string, List<double>>();
            var grouped = new List<double>();
            var freeMonsters = new List<double>();
            var townDistance = new List<double>();
            foreach (int seed in Seeds)
            {
                var map = new GameState(seed, TestData.Data, size).Map;
                int land = 0;
                for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                    if (!map.IsWater(new Cell(x, y)))
                        land++;
                string? Kind(MapObject o) => o switch
                {
                    MonsterGuard => "monstres",
                    ArtifactPile => "artefacts",
                    ResourcePile => "ressources",
                    TreasureChest or Campfire or SpellScroll or Remains => "petits trésors",
                    Mine => "mines",
                    Town => "villes",
                    Shrine or SageHut or WeeklyProducer or BlessingSite or ObservationTower or Obelisk or Portal => "lieux",
                    _ => null,
                };
                var items = map.Objects.Where(o => !map.IsWater(o.Position)).Select(o => (Kind: Kind(o), o.Position)).Where(i => i.Kind != null).ToList();
                foreach (var kind in new[] { "artefacts", "ressources", "petits trésors", "mines", "villes", "monstres", "lieux" })
                    (per1000.TryGetValue(kind, out var l) ? l : per1000[kind] = new List<double>()).Add(1000.0 * items.Count(i => i.Kind == kind) / land);
                var monsters = items.Where(i => i.Kind == "monstres").Select(i => i.Position).ToList();
                int Distance(Cell a, Cell b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
                bool NearMonster(Cell c) => monsters.Any(m => Distance(m, c) <= 2);
                foreach (var kind in new[] { "artefacts", "ressources", "petits trésors", "mines", "lieux" })
                {
                    var cells = items.Where(i => i.Kind == kind).Select(i => i.Position).ToList();
                    if (cells.Count > 0)
                        (guarded.TryGetValue(kind, out var l) ? l : guarded[kind] = new List<double>()).Add(100.0 * cells.Count(NearMonster) / cells.Count);
                }
                var treasures = items.Where(i => i.Kind is "artefacts" or "ressources" or "petits trésors").Select(i => i.Position).ToList();
                if (treasures.Count > 1)
                    grouped.Add(100.0 * treasures.Count(p => treasures.Any(t => t != p && Distance(t, p) <= 3)) / treasures.Count);
                var others = items.Where(i => i.Kind is not ("monstres" or "villes")).Select(i => i.Position).ToList();
                if (monsters.Count > 0)
                    freeMonsters.Add(100.0 * monsters.Count(m => !others.Any(o => Distance(o, m) <= 2)) / monsters.Count);
                var towns = items.Where(i => i.Kind == "villes").Select(i => i.Position).ToList();
                if (towns.Count > 0)
                    townDistance.AddRange(treasures.Select(p => (double)towns.Min(t => Distance(t, p))));
            }
            string Q(List<double> v, string f = "F1")
            {
                var s = v.OrderBy(x => x).ToList();
                return s.Count == 0 ? "-" : $"{s[s.Count / 4].ToString(f)} / {s[s.Count / 2].ToString(f)} / {s[3 * s.Count / 4].ToString(f)}";
            }
            output.WriteLine($"{size} : pour 1000 cases de terre : " + string.Join(", ", per1000.Select(k => $"{k.Key} {Q(k.Value)}")));
            output.WriteLine("  part gardée % : " + string.Join(", ", guarded.Select(k => $"{k.Key} {Q(k.Value, "F0")}")));
            output.WriteLine($"  trésors en groupes {Q(grouped, "F0")} % ; monstres loin de tout objet {Q(freeMonsters, "F0")} % ; "
                + $"distance d'un trésor à la ville {Q(townDistance, "F0")} (moins de 10 : {100 * townDistance.Count(d => d < 10) / Math.Max(1, townDistance.Count)} %)");
        }
    }

    /// <summary>
    /// Les villes neutres de chaque sol, par faction (2026-11-20 : les seconds sols des factions, qui comptent moitié moins
    /// que le sol principal) : sur des cartes grandes, toutes les graines.
    /// </summary>
    [Mesure]
    public void Villes_neutres_par_sol()
    {
        var towns = new Dictionary<TerrainType, Dictionary<string, int>>();
        foreach (int seed in Seeds.Concat(Seeds.Select(s => s + 100)))
        {
            var map = new GameState(seed, TestData.Data, MapSize.Large).Map;
            foreach (var town in map.Objects.OfType<Town>().Where(t => !map.Starts.Contains(t.Position)))
            {
                var ground = map.GetTerrain(town.Position);
                var list = towns.TryGetValue(ground, out var l) ? l : towns[ground] = new Dictionary<string, int>();
                list[town.Faction] = list.GetValueOrDefault(town.Faction) + 1;
            }
        }
        foreach (var (ground, list) in towns.OrderBy(t => t.Key))
            output.WriteLine($"{ground} : {string.Join(", ", list.OrderByDescending(f => f.Value).Select(f => $"{f.Key} {f.Value}"))}");
    }

    /// <summary>
    /// Les sols des cartes (2026-11-17, à comparer avec Heroes 2 et 3 : tools/cartes/terrains.py) : la part de la terre de
    /// chaque sol (la forêt compte comme l'herbe, la montagne comme le sol qui l'entoure), et ses taches (cases du même sol
    /// qui se touchent par un côté) : combien de 4 cases et plus, leur taille, la part de la plus grande, la part en taches
    /// de moins de 10 cases.
    /// </summary>
    [Mesure]
    public void Sols_des_cartes()
    {
        foreach (var size in new[] { MapSize.Small, MapSize.Normal, MapSize.Large })
        {
            var share = new Dictionary<TerrainType, List<int>>();
            var count = new Dictionary<TerrainType, List<int>>();
            var sizes = new Dictionary<TerrainType, List<int>>();
            var biggest = new Dictionary<TerrainType, List<int>>();
            var small = new Dictionary<TerrainType, List<int>>();
            foreach (int seed in Seeds)
            {
                var map = new GameState(seed, TestData.Data, size).Map;
                var under = MountainGrounds.Of(map);
                var forests = ForestGrounds.Of(map);
                TerrainType? Ground(Cell c) => map.GetTerrain(c) switch
                {
                    TerrainType.Water => null,
                    TerrainType.Forest => forests.GetValueOrDefault(c, TerrainType.Grass),
                    TerrainType.Mountain => under.GetValueOrDefault(c, TerrainType.Grass),
                    var t => t,
                };
                var cells = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Cell(x, y)))
                    .Where(c => !map.IsWater(c) && Ground(c) != null).ToList();
                var seen = new HashSet<Cell>();
                var patches = new Dictionary<TerrainType, List<int>>();
                foreach (var start in cells)
                {
                    if (!seen.Add(start))
                        continue;
                    var ground = Ground(start)!.Value;
                    int n = 0;
                    var queue = new Queue<Cell>(new[] { start });
                    while (queue.Count > 0)
                    {
                        var c = queue.Dequeue();
                        n++;
                        foreach (var next in new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) })
                            if (map.InBounds(next) && !map.IsWater(next) && Ground(next) == ground && seen.Add(next))
                                queue.Enqueue(next);
                    }
                    (patches.TryGetValue(ground, out var list) ? list : patches[ground] = new List<int>()).Add(n);
                }
                foreach (var (ground, list) in patches)
                {
                    int total = list.Sum();
                    if (total * 100 < cells.Count)
                        continue;
                    List<int> Of(Dictionary<TerrainType, List<int>> d) => d.TryGetValue(ground, out var l) ? l : d[ground] = new List<int>();
                    Of(share).Add((int)Math.Round(100.0 * total / cells.Count));
                    Of(count).Add(list.Count(s => s >= 4));
                    Of(sizes).AddRange(list.Where(s => s >= 4));
                    Of(biggest).Add((int)Math.Round(100.0 * list.Max() / total));
                    Of(small).Add((int)Math.Round(100.0 * list.Where(s => s < 10).Sum() / total));
                }
            }
            static string Median(List<int> v) => v.Count == 0 ? "-" : $"médiane {v.OrderBy(x => x).ElementAt(v.Count / 2)} (de {v.Min()} à {v.Max()})";
            foreach (var ground in share.Keys.OrderBy(g => g))
                output.WriteLine($"{size} {ground} : part de la terre {Median(share[ground])} % ; taches de 4 cases et plus par carte "
                                 + $"{Median(count[ground])}, taille {Median(sizes[ground])} ; la plus grande {Median(biggest[ground])} % ; "
                                 + $"en taches de moins de 10 cases {Median(small[ground])} %");
        }
    }

    /// <summary>
    /// Un adversaire (Normal) sur chaque type de carte, six semaines : s'il prend la mer (bateaux pris, débarquements),
    /// ses villes, son armée. Sur les îles, il doit quitter la sienne.
    /// </summary>
    [Mesure]
    public void Adversaires_par_type_de_carte()
    {
        foreach (var type in Enum.GetValues<MapType>())
        {
            var values = new List<int>();
            int towns = 0, boardings = 0, landings = 0, bought = 0, defeated = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1, mapType: type);
                state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
                state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50);
                var rival = state.Players[1];
                for (int day = 0; day < 42 && state.Outcome == GameOutcome.Playing; day++)
                {
                    state.EndTurn();
                    while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
                    {
                        ComputerPlayer.PlayTurn(state);
                        foreach (var e in state.TakeEvents())
                        {
                            boardings += e is BoatBoarded b && b.Hero.Owner == rival ? 1 : 0;
                            landings += e is BoatLeft l && l.Hero.Owner == rival ? 1 : 0;
                            bought += e is BoatBuilt built && built.Player == rival ? 1 : 0;
                        }
                        state.EndTurn();
                    }
                }
                values.Add(rival.Heroes.Sum(h => Battle.ExperienceFor(h.Army.Stacks)) + rival.Towns.Sum(t => Battle.ExperienceFor(t.Garrison.Stacks)));
                towns += rival.Towns.Count;
                defeated += rival.IsDefeated ? 1 : 0;
            }
            int n = Seeds.Length;
            output.WriteLine($"{type} : armée {Range(values)} ; par partie : {towns / (double)n:0.0} villes, "
                             + $"{boardings / (double)n:0.0} embarquements, {landings / (double)n:0.0} débarquements, "
                             + $"{bought / (double)n:0.0} bateaux achetés ; éliminé {defeated} fois");
        }
    }

    /// <summary>
    /// Un adversaire de chaque faction (Normal), six semaines, sur les mêmes cartes : les factions doivent se valoir.
    /// <c>HOMM_FACTION=morts-vivants</c> : cette faction seulement.
    /// </summary>
    [Mesure]
    public void Adversaires_par_faction()
    {
        var only = Environment.GetEnvironmentVariable("HOMM_FACTION");
        foreach (var faction in TestData.Data.Classes.PlayableFactions.Where(f => only == null || f == only))
        {
            var values = new List<int>();
            int towns = 0, battles = 0, defeated = 0;
            foreach (int seed in Seeds)
            {
                var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1, opponentFactions: new[] { faction });
                state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
                state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50);
                PlayDays(state, 42);
                var rival = state.Players[1];
                values.Add(rival.Heroes.Sum(h => Battle.ExperienceFor(h.Army.Stacks)) + rival.Towns.Sum(t => Battle.ExperienceFor(t.Garrison.Stacks)));
                towns += rival.Towns.Count;
                battles += rival.Statistics.BattlesWon;
                defeated += rival.IsDefeated ? 1 : 0;
            }
            int n = Seeds.Length;
            output.WriteLine($"{faction} : armée {Range(values)} ({string.Join(" ", values)}) ; par partie : {towns / (double)n:0.0} villes, "
                             + $"{battles / (double)n:0.0} combats gagnés ; éliminé {defeated} fois");
        }
    }

    /// <summary>
    /// La courbe de chaque faction : combien de paysans (sans héros) pour vaincre une unité de chaque créature. Deux
    /// créatures du même niveau devraient en demander à peu près autant.
    /// </summary>
    /// <summary>
    /// La difficulté de chaque gardien possible de la carte pour la simulation : un gardien moyen de son niveau (taille de
    /// <see cref="MapGenerator"/>, force ramenée à celle du gardien de référence), et la plus petite armée du Château
    /// (Paysans, Archers, Griffons, Cavaliers, dans les proportions de leur croissance) qui le bat en perdant au plus 30 % de ses points de vie, la règle de
    /// la simulation. Écrit l'or de cette armée : dans un niveau, des gardiens justes demandent à peu près autant.
    /// </summary>
    [Mesure]
    public void Gardiens_par_creature()
    {
        var growth = new[] { ("paysan", 30), ("archer", 18), ("griffon", 14), ("chevalier", 8), ("moine", 6), ("cavalier", 4), ("ange", 2) };
        output.WriteLine("Forces de référence : " + string.Join(", ", Enumerable.Range(1, 7).Select(t => $"{t} : {MapGenerator.ReferenceStrength(TestData.Creatures, t):0.###}")));
        foreach (var group in TestData.Creatures.All.Where(c => MapGenerator.MonsterFactions.Contains(c.Faction)).GroupBy(c => c.Tier).OrderBy(g => g.Key))
        {
            int tier = group.Key;
            var lines = new List<(double Gold, string Text)>();
            foreach (var type in group)
            {
                double danger = (tier - 0.5) / 6;
                var (guardType, count) = MapGenerator.SizeGuard(TestData.Creatures, type, tier,
                    (int)Math.Round(MapGenerator.GuardBaseCount[tier] * 1.5 * (1 + danger)));
                // des troupes nombreuses des quatre premiers niveaux du Château : l'armée grandit par petits pas
                var mix = growth.Take(4).ToList();
                double Gold(double k) => mix.Sum(m => (int)Math.Round(m.Item2 * k) * TestData.Creatures.Get(m.Item1).Cost.GetValueOrDefault(ResourceType.Gold));
                bool Beats(double k)
                {
                    var army = new Army();
                    foreach (var (id, n) in mix.Where(m => (int)Math.Round(m.Item2 * k) > 0))
                        army.Add(TestData.Creatures.Get(id), (int)Math.Round(n * k));
                    if (army.Stacks.Count == 0)
                        return false;
                    double hp = army.Stacks.Sum(s => s.Count * s.Type.HitPoints);
                    var guard = new Army();
                    guard.Add(guardType, count);
                    var report = Battle.Resolve(army, guard);
                    return report.AttackerWon && report.AttackerLosses.Sum(s => s.Count * s.Type.HitPoints) <= 0.3 * hp;
                }
                double high = 0.02;
                while (!Beats(high) && high < 2000)
                    high *= 1.5;
                double low = high / 1.5;
                for (int i = 0; i < 12; i++)
                {
                    double middle = (low + high) / 2;
                    if (Beats(middle)) high = middle; else low = middle;
                }
                lines.Add((Gold(high), $"{count} {guardType.Plural} ({type.Faction}{(guardType != type ? $", à la place de {type.Plural}" : "")}) : {Gold(high):0} or"));
            }
            double median = lines.Select(l => l.Gold).OrderBy(g => g).ElementAt(lines.Count / 2);
            output.WriteLine($"Niveau {tier} (médiane {median:0} or) :");
            foreach (var (gold, text) in lines.OrderByDescending(l => l.Gold))
                output.WriteLine($"   {text}  ×{gold / median:0.00}");
        }
    }

    [Mesure]
    public void Courbe_du_chateau()
    {
        foreach (var faction in TestData.Creatures.All.Select(c => c.Faction).Distinct())
            output.WriteLine($"{faction} : " + string.Join(", ", Curve(faction)));
    }

    /// <summary>
    /// Les Nomades (frappe et repart) contre des armées du même prix en or, avec et sans leur capacité : la part de
    /// l'armée perdue par le vainqueur, sur 20 combats (les deux camps joués par l'IA). Pour voir si la capacité est trop
    /// forte contre les armées lentes.
    /// </summary>
    [Mesure]
    public void Nomades_frappe_et_repart()
    {
        var nomads = TestData.Creatures.Get("nomades");
        var without = nomads with { Abilities = nomads.Abilities.Where(a => a != Ability.HitAndRun).ToList() };
        const int count = 20;
        int gold = count * nomads.Cost[ResourceType.Gold];
        foreach (var id in new[] { "zombie", "ogre", "chevalier", "moine", "vampire", "cavalier", "troll" })
        {
            var enemy = TestData.Creatures.Get(id);
            string Line(CreatureType type)
            {
                int wins = 0;
                double lost = 0;
                for (int seed = 0; seed < 20; seed++)
                {
                    var ours = new Army();
                    ours.Add(type, count);
                    var theirs = new Army();
                    theirs.Add(enemy, Math.Max(1, (int)Math.Round((double)gold / enemy.Cost[ResourceType.Gold])));
                    var battle = new TacticalBattle(ours, theirs, new GameRandom(seed));
                    BattleAI.PlayOut(battle);
                    bool won = battle.Outcome == BattleOutcome.AttackerWon;
                    if (won)
                        wins++;
                    lost += won ? 1 - ours.TotalCount / (double)count : 0;
                }
                return $"{wins}/20 gagnés, pertes {(wins == 0 ? 0 : 100 * lost / wins):0} %";
            }
            output.WriteLine($"{enemy.Plural} : avec {Line(nomads)} ; sans {Line(without)}");
        }
    }

    /// <summary>
    /// Les créatures d'un même niveau, de factions différentes, l'une contre l'autre pour le même prix en or (environ
    /// 3000 or chacune, au moins 3 créatures, sans héros), sur 20 combats joués par l'IA, chacune à son tour attaquante : les victoires de la
    /// première, et ce qui reste au vainqueur. Plus fin que la courbe des paysans pour comparer un niveau.
    /// </summary>
    [Mesure]
    public void Duels_a_prix_egal()
    {
        const int gold = 3000;
        var factions = new[] { "chateau", "morts-vivants", "barbares", "foret", "tour", "donjon", "enfer", "nains", "neutre" };
        for (int tier = 1; tier <= 7; tier++)
        {
            var creatures = factions.Select(f => TestData.Creatures.All.FirstOrDefault(c => c.Faction == f && c.Tier == tier))
                .OfType<CreatureType>().ToList();
            for (int i = 0; i < creatures.Count; i++)
            for (int j = i + 1; j < creatures.Count; j++)
            {
                var (a, b) = (creatures[i], creatures[j]);
                int Count(CreatureType c) => Math.Max(1, (int)Math.Round((double)Math.Max(gold, 3 * c.Cost[ResourceType.Gold]) / c.Cost[ResourceType.Gold]));
                int winsA = 0;
                double leftA = 0, leftB = 0;
                for (int seed = 0; seed < 20; seed++)
                {
                    bool aAttacks = seed % 2 == 0;
                    var armyA = new Army();
                    armyA.Add(a, Count(a));
                    var armyB = new Army();
                    armyB.Add(b, Count(b));
                    var battle = aAttacks ? new TacticalBattle(armyA, armyB, new GameRandom(seed)) : new TacticalBattle(armyB, armyA, new GameRandom(seed));
                    BattleAI.PlayOut(battle);
                    if (armyA.TotalCount > 0 && armyB.TotalCount == 0)
                    {
                        winsA++;
                        leftA += armyA.TotalCount / (double)Count(a);
                    }
                    else if (armyB.TotalCount > 0)
                        leftB += armyB.TotalCount / (double)Count(b);
                }
                int winsB = 20 - winsA;
                output.WriteLine($"{tier}. {Count(a)} {a.Plural} contre {Count(b)} {b.Plural} : {winsA}/20 (reste {(winsA == 0 ? 0 : 100 * leftA / winsA):0} %)"
                                 + $" contre {winsB}/20 (reste {(winsB == 0 ? 0 : 100 * leftB / winsB):0} %)");
            }
        }
    }

    /// <summary>
    /// Les armées entières à prix égal (2026-11-25, la mesure qui compte pour l'utilisateur) : pour chaque faction, une
    /// semaine de croissance de ses sept niveaux, ramenée à <c>gold</c> or (sans héros), contre celle de chaque autre
    /// faction, sur 20 combats joués par l'IA, chacune à son tour attaquante. Écrit chaque rencontre, puis les victoires de
    /// chaque faction (une armée trop forte ou trop faible se voit ici, même si ses créatures sont justes une à une).
    /// <c>HOMM_FACTION=nains</c> : seulement les rencontres de cette faction (plus rapide, pour essayer un changement).
    /// </summary>
    [Mesure]
    public void Armees_a_prix_egal()
    {
        const int gold = 30000;
        var factions = new[] { "chateau", "morts-vivants", "barbares", "foret", "tour", "donjon", "enfer", "nains" };
        var only = Environment.GetEnvironmentVariable("HOMM_FACTION");
        Army Week(string faction)
        {
            var types = TestData.Creatures.All.Where(c => c.Faction == faction).OrderBy(c => c.Tier).ToList();
            double weekGold = types.Sum(c => c.Growth * c.Cost[ResourceType.Gold]);
            var army = new Army();
            foreach (var c in types)
                army.Add(c, Math.Max(1, (int)Math.Round(c.Growth * gold / weekGold)));
            return army;
        }
        double Strength(Army army) => army.Stacks.Sum(s => s.Count * s.Type.Cost[ResourceType.Gold]);
        var wins = factions.ToDictionary(f => f, _ => 0);
        for (int i = 0; i < factions.Length; i++)
        for (int j = i + 1; j < factions.Length; j++)
        {
            var (a, b) = (factions[i], factions[j]);
            if (only != null && a != only && b != only)
                continue;
            int winsA = 0;
            double leftA = 0, leftB = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                var (armyA, armyB) = (Week(a), Week(b));
                double startA = Strength(armyA), startB = Strength(armyB);
                var battle = seed % 2 == 0 ? new TacticalBattle(armyA, armyB, new GameRandom(seed)) : new TacticalBattle(armyB, armyA, new GameRandom(seed));
                BattleAI.PlayOut(battle);
                if (armyA.TotalCount > 0 && armyB.TotalCount == 0)
                {
                    winsA++;
                    leftA += Strength(armyA) / startA;
                }
                else if (armyB.TotalCount > 0)
                    leftB += Strength(armyB) / startB;
            }
            int winsB = 20 - winsA;
            wins[a] += winsA;
            wins[b] += winsB;
            output.WriteLine($"{a} contre {b} : {winsA}/20 (reste {(winsA == 0 ? 0 : 100 * leftA / winsA):0} % de l'or)"
                             + $" contre {winsB}/20 (reste {(winsB == 0 ? 0 : 100 * leftB / winsB):0} %)");
        }
        output.WriteLine("Victoires : " + string.Join(", ", wins.OrderByDescending(w => w.Value)
            .Select(w => $"{w.Key} {100.0 * w.Value / (20 * (factions.Length - 1)):0} %")));
    }

    /// <summary>
    /// Les premières semaines (2026-11-25) : pour chaque faction, l'armée de départ de sa classe et la croissance de ses
    /// niveaux 1 à 4 pendant 1, 2 puis 3 semaines (sans regarder l'or), menées par un héros de niveau 1 de sa classe
    /// (attaque, défense, moral et limite de commandement), contre celle de chaque autre faction, sur 20 combats joués par
    /// l'IA, chacune à son tour attaquante. Écrit les victoires et la marge moyenne de chaque faction, puis les troupes qui
    /// dépassent la limite de commandement (leur moral baisse). La horde barbare devrait y dominer.
    /// <c>HOMM_FACTION=barbares</c> : seulement les rencontres de cette faction.
    /// </summary>
    [Mesure]
    public void Premieres_semaines()
    {
        var factions = new[] { "chateau", "morts-vivants", "barbares", "foret", "tour", "donjon", "enfer", "nains" };
        var only = Environment.GetEnvironmentVariable("HOMM_FACTION");
        var map = new AdventureMap(4, 4);
        Hero Leader(string faction)
        {
            var hero = new Hero(faction, new Player(faction, map), new Cell(0, 0));
            hero.TakeClass(TestData.Data.Classes.ForFaction(faction)!, TestData.Data.Spells, TestData.Data.Skills);
            return hero;
        }
        Army Week(string faction, int weeks)
        {
            var army = new Army();
            foreach (var stack in TestData.Data.Classes.ForFaction(faction)!.Army)
                army.Add(TestData.Creatures.Get(stack.Creature), stack.Count);
            foreach (var c in TestData.Creatures.All.Where(c => c.Faction == faction && c.Tier <= 4).OrderBy(c => c.Tier))
                army.Add(c, c.Growth * weeks);
            return army;
        }
        double Gold(Army army) => army.Stacks.Sum(s => s.Count * s.Type.Cost[ResourceType.Gold]);
        for (int weeks = 1; weeks <= 3; weeks++)
        {
            var margin = factions.ToDictionary(f => f, _ => 0.0);
            var wins = factions.ToDictionary(f => f, _ => 0);
            for (int i = 0; i < factions.Length; i++)
            for (int j = i + 1; j < factions.Length; j++)
            {
                var (a, b) = (factions[i], factions[j]);
                if (only != null && a != only && b != only)
                    continue;
                int winsA = 0;
                double sum = 0;
                for (int seed = 0; seed < 20; seed++)
                {
                    var (armyA, armyB) = (Week(a, weeks), Week(b, weeks));
                    var (heroA, heroB) = (Leader(a), Leader(b));
                    double startA = Gold(armyA), startB = Gold(armyB);
                    var battle = seed % 2 == 0
                        ? new TacticalBattle(armyA, armyB, new GameRandom(seed), heroA, defenderHero: heroB)
                        : new TacticalBattle(armyB, armyA, new GameRandom(seed), heroB, defenderHero: heroA);
                    BattleAI.PlayOut(battle);
                    if (armyA.TotalCount > 0 && armyB.TotalCount == 0)
                    {
                        winsA++;
                        sum += Gold(armyA) / startA;
                    }
                    else if (armyB.TotalCount > 0)
                        sum -= Gold(armyB) / startB;
                }
                wins[a] += winsA;
                wins[b] += 20 - winsA;
                margin[a] += 100 * sum / 20;
                margin[b] -= 100 * sum / 20;
            }
            int pairs = only != null ? 1 : factions.Length - 1;
            output.WriteLine($"{weeks} semaine(s) : " + string.Join(", ", factions.Where(f => only == null || f == only)
                .OrderByDescending(f => margin[f])
                .Select(f => $"{f} {100.0 * wins[f] / (20 * (only == null ? pairs : factions.Length - 1)):0} % ({margin[f] / (only == null ? pairs : factions.Length - 1):+0;-0})")));
        }
        foreach (var faction in factions)
        {
            var hero = Leader(faction);
            var over = Week(faction, 3).Stacks.Where(s => Morale.Overload(s.Type, s.Count, hero.CommandLimit) < 0)
                .Select(s => $"{s.Count} {s.Type.Plural} ({Morale.Overload(s.Type, s.Count, hero.CommandLimit)})");
            output.WriteLine($"{faction}, limite {hero.CommandLimit}, après 3 semaines : {string.Join(", ", over)}");
        }
    }

    private static IEnumerable<string> Curve(string faction)
    {
        var line = new List<string>();
        foreach (var creature in TestData.Creatures.All.Where(c => c.Faction == faction && c.Id != "paysan").OrderBy(c => c.Tier))
        {
            int peasants = 1;
            while (peasants < 100_000)
            {
                var army = new Army();
                army.Add(TestData.Creatures.Get("paysan"), peasants);
                var enemy = new Army();
                enemy.Add(creature, 1);
                if (Battle.Resolve(army, enemy).AttackerWon)
                    break;
                peasants = Math.Max(peasants + 1, (int)(peasants * 1.05));
            }
            line.Add($"{creature.Tier}. {creature.Name} {peasants}");
        }
        return line;
    }

    /// <summary>Joue des jours : le joueur passe son tour, les adversaires jouent.</summary>
    private static void PlayDays(GameState state, int days)
    {
        for (int day = 0; day < days && state.Outcome == GameOutcome.Playing; day++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
    }
}
