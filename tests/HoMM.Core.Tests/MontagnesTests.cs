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
        Assert.True(mountains.SetEquals(blocked));
        // surtout de vrais massifs, pas seulement des sommets seuls
        Assert.True(map.Massifs.Count(m => shapes.Find(m.Id)!.Cells.Count() > 1) * 2 > map.Massifs.Count);
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
