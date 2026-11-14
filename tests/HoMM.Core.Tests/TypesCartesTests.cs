using System.Collections.Generic;
using System.Linq;
using System.Text;
using HoMM.Core;
using Xunit;
using Xunit.Abstractions;

namespace HoMM.Core.Tests;

/// <summary>
/// Les types de carte (la mer, étape 2, 2026-11-14) : continent, quelques mers, îles, archipel. Chacun se génère complet,
/// avec de quoi prendre la mer sur l'île de chaque joueur.
/// </summary>
public class TypesCartesTests(ITestOutputHelper output)
{
    /// <summary>
    /// Les cases atteintes depuis <paramref name="from"/>, comme le <see cref="Pathfinder"/> (en diagonale sans couper un coin
    /// bloqué, en s'arrêtant sur un objet) ; <paramref name="sail"/> : l'eau aussi (en bateau) ; <paramref name="throughGuards"/> :
    /// à travers les monstres (on les bat).
    /// </summary>
    private static HashSet<Cell> Reach(AdventureMap map, Cell from, bool sail, bool throughGuards)
    {
        bool Open(Cell c) => map.IsPassable(c) || sail && map.IsWater(c);
        var seen = new HashSet<Cell> { from };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c != from && map.ObjectAt(c) is { } obj && !(throughGuards && obj is MonsterGuard))
                continue;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var next = c.Offset(dx, dy);
                if (!Open(next) || (dx != 0 && dy != 0 && !(Open(c.Offset(dx, 0)) && Open(c.Offset(0, dy)))))
                    continue;
                if (seen.Add(next))
                    queue.Enqueue(next);
            }
        }
        return seen;
    }

    private static IEnumerable<Cell> Around(Cell c) =>
        from dy in new[] { -1, 0, 1 } from dx in new[] { -1, 0, 1 } where dx != 0 || dy != 0 select c.Offset(dx, dy);

    private static double WaterShare(AdventureMap map)
    {
        int water = 0;
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
            if (map.IsWater(new Cell(x, y)))
                water++;
        return (double)water / (map.Width * map.Height);
    }

    [Theory]
    [InlineData(MapType.Continent)]
    [InlineData(MapType.Seas)]
    [InlineData(MapType.Islands)]
    [InlineData(MapType.Archipelago)]
    public void Chaque_type_donne_une_carte_complete(MapType type)
    {
        foreach (int seed in new[] { 1, 42 })
        {
            var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1, mapType: type);
            var map = state.Map;
            output.WriteLine($"{type}, graine {seed} : {WaterShare(map):P0} d'eau, {map.Objects.OfType<Town>().Count()} villes, "
                + $"{map.Objects.OfType<Boat>().Count()} bateaux, {map.Objects.OfType<Shipyard>().Count()} chantiers, "
                + $"{map.Objects.OfType<Town>().Count(t => t.BoatSpot != null)} ports");
            Assert.Contains(map.Objects, o => o is Obelisk);
            Assert.NotNull(map.Treasure);
            Assert.True(map.Objects.OfType<Town>().Count() >= 4);
            // tout s'atteint, en bateau s'il le faut
            var reachable = Reach(map, map.Start, sail: true, throughGuards: false);
            foreach (var obj in map.Objects.Where(o => o.Position != map.Start))
                Assert.True(reachable.Contains(obj.Position), $"{type}, graine {seed} : {obj.GetType().Name} hors d'atteinte en {obj.Position}");
            Assert.Contains(map.Treasure!.Value, reachable);
        }
    }

    [Theory]
    [InlineData(MapType.Seas)]
    [InlineData(MapType.Islands)]
    [InlineData(MapType.Archipelago)]
    public void Il_y_a_de_la_mer_et_de_quoi_naviguer(MapType type)
    {
        foreach (int seed in new[] { 1, 7, 42 })
        {
            var map = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1, mapType: type).Map;
            double continent = WaterShare(new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1).Map);
            Assert.True(WaterShare(map) > continent + 0.1, $"{type}, graine {seed} : {WaterShare(map):P0} d'eau (continent {continent:P0})");
            // chaque joueur trouve un bateau et un chantier naval à pied (en battant les monstres) depuis son départ
            foreach (var start in map.Starts)
            {
                var onFoot = Reach(map, start, sail: false, throughGuards: true);
                Assert.True(map.Objects.Any(o => o is Boat && Around(o.Position).Any(onFoot.Contains)), $"{type}, graine {seed} : pas de bateau pour {start}");
                Assert.True(map.Objects.Any(o => o is Shipyard && onFoot.Contains(o.Position)), $"{type}, graine {seed} : pas de chantier pour {start}");
            }
        }
    }

    [Fact]
    public void Avec_quelques_mers_tout_s_atteint_a_pied()
    {
        foreach (int seed in new[] { 1, 7, 42 })
        {
            var map = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1, mapType: MapType.Seas).Map;
            var onFoot = Reach(map, map.Start, sail: false, throughGuards: true);
            foreach (var obj in map.Objects.Where(o => o is not Boat && o.Position != map.Start))
                Assert.True(onFoot.Contains(obj.Position), $"graine {seed} : {obj.GetType().Name} en {obj.Position} seulement en bateau");
        }
    }

    [Theory]
    [InlineData(MapType.Islands)]
    [InlineData(MapType.Archipelago)]
    public void Sur_les_iles_chaque_joueur_a_la_sienne(MapType type)
    {
        foreach (int seed in new[] { 1, 7, 42 })
        {
            var map = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 3, mapType: type).Map;
            var towns = map.Objects.OfType<Town>().ToList();
            foreach (var start in map.Starts)
            {
                var onFoot = Reach(map, start, sail: false, throughGuards: true);
                Assert.All(map.Starts.Where(s => s != start), other => Assert.DoesNotContain(other, onFoot));
                // et toutes les villes ne sont pas sur son île
                Assert.Contains(towns, t => !onFoot.Contains(t.Position));
            }
        }
    }

    /// <summary>Les tailles extrêmes se génèrent aussi (la toute petite : une ou deux zones de chaque sorte).</summary>
    [Theory]
    [InlineData(MapSize.Tiny, MapType.Archipelago)]
    [InlineData(MapSize.Small, MapType.Islands)]
    [InlineData(MapSize.Huge, MapType.Archipelago)]
    public void Les_autres_tailles_se_generent(MapSize size, MapType type)
    {
        var state = new GameState(3, TestData.Data, size, opponents: 1, mapType: type);
        var reachable = Reach(state.Map, state.Map.Start, sail: true, throughGuards: false);
        Assert.All(state.Map.Objects.Where(o => o.Position != state.Map.Start), o => Assert.Contains(o.Position, reachable));
    }

    /// <summary>
    /// Sur beaucoup de cartes à quatre joueurs (les plus serrées) : tout s'atteint, chaque joueur va à pied jusqu'à un bateau
    /// et un chantier naval, et avec quelques mers tout s'atteint à pied. (Vérifié aussi sur 30 graines et les grandes
    /// cartes, 2026-11-14.)
    /// </summary>
    [Theory]
    [InlineData(MapType.Seas)]
    [InlineData(MapType.Islands)]
    [InlineData(MapType.Archipelago)]
    public void Les_cartes_a_quatre_joueurs_sont_completes(MapType type)
    {
        var problems = new List<string>();
        for (int seed = 1; seed <= 8; seed++)
        {
            var map = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 3, mapType: type).Map;
            var reachable = Reach(map, map.Start, sail: true, throughGuards: false);
            problems.AddRange(map.Objects.Where(o => o.Position != map.Start && !reachable.Contains(o.Position))
                .Select(o => $"graine {seed} : {o.GetType().Name} hors d'atteinte en {o.Position}"));
            if (!map.Objects.OfType<Obelisk>().Any())
                problems.Add($"graine {seed} : pas d'obélisque");
            foreach (var start in map.Starts)
            {
                var onFoot = Reach(map, start, sail: false, throughGuards: true);
                if (!map.Objects.Any(o => o is Boat && Around(o.Position).Any(onFoot.Contains)))
                    problems.Add($"graine {seed} : pas de bateau pour {start}");
                if (!map.Objects.Any(o => o is Shipyard && onFoot.Contains(o.Position)))
                    problems.Add($"graine {seed} : pas de chantier pour {start}");
                if (type == MapType.Seas)
                    problems.AddRange(map.Objects.Where(o => o is not Boat && o.Position != start && !onFoot.Contains(o.Position))
                        .Select(o => $"graine {seed} : {o.GetType().Name} en {o.Position} seulement en bateau"));
            }
        }
        Assert.Empty(problems);
    }

    /// <summary>La carte en lettres, pour la regarder (`dotnet test --filter Dessin --logger "console;verbosity=detailed"`).</summary>
    [Theory(Skip = "pour regarder une carte")]
    [InlineData(MapType.Seas, 1)]
    [InlineData(MapType.Islands, 1)]
    [InlineData(MapType.Archipelago, 1)]
    [InlineData(MapType.Islands, 3)]
    public void Dessin(MapType type, int seed)
    {
        var map = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 3, mapType: type).Map;
        var text = new StringBuilder();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var c = new Cell(x, y);
                text.Append(map.ObjectAt(c) switch
                {
                    Boat => 'B',
                    Shipyard => 'Y',
                    Town => 'T',
                    MonsterGuard => 'g',
                    null => map.GetTerrain(c) switch
                    {
                        TerrainType.Water => '~',
                        TerrainType.Mountain => '^',
                        TerrainType.Forest => 'f',
                        _ => map.HasRoad(c) ? '=' : '.',
                    },
                    _ => 'o',
                });
            }
            text.AppendLine();
        }
        output.WriteLine(text.ToString());
    }
}
