using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les montagnes en massifs, comme dans Heroes 3 : les cases de montagne sont celles que bloquent les massifs posés.</summary>
public class MontagnesTests
{
    private static IEnumerable<Cell> Cells(AdventureMap map) =>
        Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Cell(x, y)));

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(42)]
    public void Les_massifs_couvrent_exactement_les_montagnes(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();
        var shapes = TestData.Data.Massifs;

        var blocked = map.Massifs.SelectMany(m => shapes.Find(m.Id)!.CellsAt(m.At)).ToHashSet();
        var mountains = Cells(map).Where(c => map.GetTerrain(c) == TerrainType.Mountain).ToHashSet();
        Assert.NotEmpty(mountains);
        Assert.True(mountains.SetEquals(blocked), $"montagnes sans massif : {string.Join(" ", mountains.Except(blocked))} ; "
            + $"massif hors des montagnes : {string.Join(" ", blocked.Except(mountains))}");
        // surtout de vrais massifs, pas seulement des sommets seuls
        int real = map.Massifs.Count(m => shapes.Find(m.Id)!.Cells.Count() > 1);
        Assert.True(real * 2 > map.Massifs.Count, $"{real} massifs sur {map.Massifs.Count}");
    }

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(42)]
    public void Aucun_massif_ne_couvre_l_eau(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();
        var shapes = TestData.Data.Massifs;

        // (les sommets seuls restent dans leur case)
        var drawn = map.Massifs.Select(m => (Shape: shapes.Find(m.Id)!, m.At)).Where(x => x.Shape.Cells.Count() > 1)
            .SelectMany(x => x.Shape.DrawnCellsAt(x.At));
        Assert.All(drawn.Where(map.InBounds), c =>
            Assert.True(map.GetTerrain(c) != TerrainType.Water && map.GetRiver(c) == RiverKind.None, $"{c} : de l'eau sous un massif"));
    }

    [Fact]
    public void Les_mines_creusees_s_adossent_souvent_a_une_montagne()
    {
        // comme dans Heroes 3 : une mine (sauf la scierie) au flanc d'une montagne, deux des trois cases juste derrière elle
        // sont de la montagne
        int total = 0, backed = 0;
        foreach (int seed in new[] { 5, 12, 42 })
        {
            var map = new MapGenerator(seed, TestData.Data).Generate();
            foreach (var mine in map.Objects.OfType<Mine>().Where(m => m.Resource != ResourceType.Wood))
            {
                total++;
                var e = mine.Position;
                if (new[] { -1, 0, 1 }.Count(dx => map.InBounds(e.Offset(dx, -2)) && map.GetTerrain(e.Offset(dx, -2)) == TerrainType.Mountain) >= 2)
                    backed++;
            }
        }
        Assert.True(backed * 10 >= total * 4, $"{backed} mines adossées sur {total}");
    }

    [Fact]
    public void Un_massif_bloque_les_cases_de_sa_forme()
    {
        var shape = new MassifShape { Id = "essai", Rows = new List<string> { ".##", "##." } };

        Assert.Equal(new[] { new Cell(6, 3), new Cell(7, 3), new Cell(5, 4), new Cell(6, 4) }, shape.CellsAt(new Cell(5, 3)));
        Assert.Equal((3, 2), (shape.Width, shape.Height));
    }

    [Fact]
    public void Les_massifs_sont_sauvegardes()
    {
        var state = new GameState(42, TestData.Data, 54, 40);

        var loaded = GameState.Load(state.Save(), TestData.Data);

        Assert.NotEmpty(loaded.Map.Massifs);
        Assert.Equal(state.Map.Massifs, loaded.Map.Massifs);
    }
}
