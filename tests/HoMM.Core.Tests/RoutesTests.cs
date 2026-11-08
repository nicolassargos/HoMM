using System.Linq;
using System.Text.Json.Nodes;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La route est une couche posée sur le terrain : son coût, le générateur, la sauvegarde.</summary>
public class RoutesTests
{
    [Fact]
    public void Une_route_coute_75_quel_que_soit_le_terrain()
    {
        var map = new AdventureMap(3, 1);
        var sand = new Cell(0, 0);
        map.SetTerrain(sand, TerrainType.Sand);
        Assert.Equal(TerrainInfo.MoveCost(TerrainType.Sand), map.MoveCost(sand));

        map.SetRoad(sand, true);

        Assert.Equal(TerrainType.Sand, map.GetTerrain(sand)); // le terrain reste
        Assert.Equal(TerrainInfo.RoadCost, map.MoveCost(sand));
        Assert.Equal(TerrainInfo.RoadCost, Pathfinder.StepCost(map, sand, new Cell(1, 0)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(99)]
    public void Le_generateur_pose_des_routes_praticables_et_libres(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();
        var roads = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new Cell(x, y)))
            .Where(map.HasRoad).ToList();

        Assert.NotEmpty(roads);
        foreach (var cell in roads)
        {
            // on peut marcher sur la route, et les arbres ne la cachent pas
            Assert.True(TerrainInfo.IsPassable(map.GetTerrain(cell)), $"route infranchissable en {cell}");
            Assert.NotEqual(TerrainType.Forest, map.GetTerrain(cell));
            // les tas et les coffres ne sont pas posés sur la route
            Assert.False(map.ObjectAt(cell) is ResourcePile or TreasureChest, $"objet sur la route en {cell}");
        }
    }

    /// <summary>
    /// Les routes mènent à la porte des villes : la case d'approche (sous l'entrée) est sur la route, aucune route ne passe
    /// sous les murs, et aucune ne s'arrête contre le flanc d'une ville.
    /// </summary>
    [Theory]
    [InlineData(1, 1, 36, 27)]
    [InlineData(2, 2, 54, 27)]
    [InlineData(3, 3, 54, 40)]
    [InlineData(4, 4, 72, 53)]
    [InlineData(5, 1, 54, 40)]
    [InlineData(6, 2, 54, 40)]
    [InlineData(7, 3, 90, 66)]
    [InlineData(8, 4, 90, 66)]
    public void Les_routes_menent_a_la_porte_des_villes(int seed, int players, int width, int height)
    {
        var map = new MapGenerator(seed, TestData.Data) { Players = players }.Generate(width, height);
        bool Road(Cell c) => map.InBounds(c) && map.HasRoad(c);
        Cell[] Around(Cell c) => new[] { c.Offset(1, 0), c.Offset(-1, 0), c.Offset(0, 1), c.Offset(0, -1) };

        foreach (var town in map.Objects.OfType<Town>())
        {
            var walls = town.Footprint.ToHashSet();
            Assert.True(Road(town.Approach), $"pas de route devant la porte de {town.Name} ({town.Position})");
            foreach (var wall in walls.Where(w => w != town.Position))
                Assert.False(Road(wall), $"route sous les murs de {town.Name} en {wall}");
            // une route qui finit (une seule voisine) contre la ville ne peut finir que devant la porte
            foreach (var end in walls.SelectMany(Around).Where(c => !walls.Contains(c) && c != town.Approach).Distinct())
                Assert.False(Road(end) && Around(end).Count(Road) <= 1, $"route en cul-de-sac contre {town.Name} en {end}");
        }
    }

    [Fact]
    public void Les_routes_sont_sauvegardees()
    {
        var state = new GameState(42, TestData.Data);
        var cell = Enumerable.Range(0, state.Map.Width).Select(x => new Cell(x, 0)).First(c => !state.Map.HasRoad(c));
        state.Map.SetRoad(cell, true);

        var loaded = GameState.Load(state.Save(), TestData.Data);

        for (int y = 0; y < state.Map.Height; y++)
        for (int x = 0; x < state.Map.Width; x++)
            Assert.Equal(state.Map.HasRoad(new Cell(x, y)), loaded.Map.HasRoad(new Cell(x, y)));
        Assert.True(loaded.Map.HasRoad(cell));
    }

    [Fact]
    public void Une_sauvegarde_de_la_version_1_est_convertie()
    {
        // Une sauvegarde de la version 1 : la route y était le terrain numéro 1, sans liste de routes
        var state = new GameState(42, TestData.Data);
        var json = JsonNode.Parse(state.Save())!;
        json["version"] = 1;
        var map = json["map"]!;
        var terrain = map["terrain"]!.AsArray();
        var roads = map["roads"]!.AsArray();
        for (int y = 0; y < terrain.Count; y++)
        {
            var row = terrain[y]!.GetValue<string>().ToCharArray();
            var road = roads[y]!.GetValue<string>();
            for (int x = 0; x < row.Length; x++)
                if (road[x] == '1')
                    row[x] = '1';
            terrain[y] = new string(row);
        }
        map.AsObject().Remove("roads");

        var loaded = GameState.Load(json.ToJsonString(), TestData.Data);

        for (int y = 0; y < state.Map.Height; y++)
        for (int x = 0; x < state.Map.Width; x++)
        {
            var cell = new Cell(x, y);
            Assert.Equal(state.Map.HasRoad(cell), loaded.Map.HasRoad(cell));
            // l'ancienne route devient de l'herbe avec une route
            Assert.Equal(state.Map.HasRoad(cell) ? TerrainType.Grass : state.Map.GetTerrain(cell), loaded.Map.GetTerrain(cell));
        }
    }

    [Fact]
    public void Une_route_illisible_est_refusee()
    {
        var json = JsonNode.Parse(new GameState(42, TestData.Data).Save())!;
        var roads = json["map"]!["roads"]!.AsArray();
        roads[0] = "x" + roads[0]!.GetValue<string>()[1..];

        Assert.Throws<System.FormatException>(() => GameState.Load(json.ToJsonString(), TestData.Data));
    }
}
