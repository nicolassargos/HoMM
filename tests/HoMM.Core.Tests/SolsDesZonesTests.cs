using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Un sol par zone, comme dans Heroes 2 et 3 : de grandes régions de sable et de marais, le départ au sol de sa faction.</summary>
public class SolsDesZonesTests
{
    private static AdventureMap Map(int seed, params string[] factions) =>
        new MapGenerator(seed, TestData.Data) { Players = factions.Length, PlayerFactions = factions }.Generate();

    [Theory]
    [InlineData("barbares", TerrainType.Sand, null)]
    [InlineData("enfer", TerrainType.Burnt, TerrainType.Sand)]
    [InlineData("morts-vivants", TerrainType.Swamp, TerrainType.Rock)]
    [InlineData("donjon", TerrainType.Swamp, null)]
    [InlineData("chateau", TerrainType.Grass, null)]
    [InlineData("nains", TerrainType.Rock, TerrainType.Snow)]
    [InlineData("tour", TerrainType.Snow, TerrainType.Sand)]
    public void Le_depart_prend_un_sol_de_sa_faction(string faction, TerrainType ground, TerrainType? second)
    {
        var seen = new System.Collections.Generic.HashSet<TerrainType>();
        foreach (int seed in Enumerable.Range(1, 12))
        {
            var map = Map(seed, faction);
            var start = map.Starts[0];
            var own = map.GetTerrain(start);
            seen.Add(own);

            Assert.True(own == ground || own == second, $"graine {seed} : {own}");
            Assert.All(Town.FootprintAt(start).Append(start.Offset(0, 1)), c => Assert.Equal(own, map.GetTerrain(c)));
        }
        // le principal le plus souvent, le second aussi parfois
        Assert.Contains(ground, seen);
        if (second is { } other)
            Assert.Contains(other, seen);
    }

    [Fact]
    public void Chaque_joueur_a_le_sol_de_sa_faction()
    {
        var map = Map(42, "barbares", "donjon");

        Assert.Equal(TerrainType.Sand, map.GetTerrain(map.Starts[0]));
        Assert.Equal(TerrainType.Swamp, map.GetTerrain(map.Starts[1]));
    }

    [Fact]
    public void Un_heros_marche_sans_ralentir_sur_le_sol_de_sa_faction()
    {
        var map = new AdventureMap(5, 5);
        var sand = new Cell(1, 1);
        var swamp = new Cell(3, 3);
        var road = new Cell(1, 3);
        map.SetTerrain(sand, TerrainType.Sand);
        map.SetTerrain(swamp, TerrainType.Swamp);
        map.SetTerrain(road, TerrainType.Sand);
        map.SetRoad(road, true);
        var barbarian = new GameState(42, TestData.Data, MapSize.Small, faction: "barbares").CurrentHero!;
        var knight = new GameState(42, TestData.Data, MapSize.Small, faction: "chateau").CurrentHero!;

        Assert.Equal(new[] { TerrainType.Sand }, barbarian.OwnGrounds);
        Assert.Equal(100, barbarian.StepCost(map, sand, sand.Offset(1, 0)));
        Assert.Equal(141, barbarian.StepCost(map, sand, sand.Offset(1, 1)));
        Assert.Equal(175, barbarian.StepCost(map, swamp, swamp.Offset(1, 0))); // l'autre sol reste lent
        Assert.Equal(TerrainInfo.RoadCost, barbarian.StepCost(map, road, road.Offset(1, 0))); // la route reste plus rapide
        Assert.Equal(150, knight.StepCost(map, sand, sand.Offset(1, 0)));
    }

    [Fact]
    public void Un_heros_marche_aussi_sans_ralentir_sur_le_second_sol_de_sa_faction()
    {
        var map = new AdventureMap(5, 5);
        var rock = new Cell(1, 1);
        var snow = new Cell(3, 3);
        map.SetTerrain(rock, TerrainType.Rock);
        map.SetTerrain(snow, TerrainType.Snow);
        var dwarf = new GameState(42, TestData.Data, MapSize.Small, faction: "nains").CurrentHero!;
        var knight = new GameState(42, TestData.Data, MapSize.Small, faction: "chateau").CurrentHero!;

        Assert.Equal(100, dwarf.StepCost(map, rock, rock.Offset(1, 0)));
        Assert.Equal(100, dwarf.StepCost(map, snow, snow.Offset(1, 0)));
        Assert.True(knight.StepCost(map, snow, snow.Offset(1, 0)) > 100);
    }

    [Fact]
    public void Une_ville_neutre_est_d_une_faction_de_son_sol()
    {
        int checkedTowns = 0;
        foreach (int seed in new[] { 5, 12, 42 })
        {
            var map = Map(seed, "chateau");
            foreach (var town in map.Objects.OfType<Town>().Where(t => !map.Starts.Contains(t.Position)))
            {
                var ground = map.GetTerrain(town.Position);
                Assert.Contains(ground, TestData.Data.Classes.ForFaction(town.Faction)!.Grounds);
                checkedTowns++;
            }
        }
        Assert.True(checkedTowns > 0);
    }
}
