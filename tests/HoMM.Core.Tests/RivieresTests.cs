using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les rivières entre les zones (on ne passe que sur leurs ponts) et les ruisseaux (un décor qu'on traverse).</summary>
public class RivieresTests
{
    private static IEnumerable<Cell> Cells(AdventureMap map) =>
        Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Cell(x, y)));

    private static readonly Cell[] Sides = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    [Fact]
    public void Une_riviere_bloque_un_pont_et_un_ruisseau_se_traversent()
    {
        var map = new AdventureMap(4, 1);
        map.SetRiver(new Cell(1, 0), RiverKind.River);
        map.SetRiver(new Cell(2, 0), RiverKind.Bridge);
        map.SetRiver(new Cell(3, 0), RiverKind.Stream);

        Assert.False(map.IsPassable(new Cell(1, 0)));
        Assert.True(map.IsPassable(new Cell(2, 0)));
        Assert.True(map.IsPassable(new Cell(3, 0)));
        Assert.Equal(TerrainInfo.MoveCost(TerrainType.Grass), map.MoveCost(new Cell(3, 0))); // le ruisseau ne ralentit pas
    }

    [Theory]
    [InlineData(42)]
    [InlineData(5)]
    [InlineData(7)]
    public void Les_rivieres_separent_des_zones_et_on_les_passe_sur_un_pont_garde(int seed)
    {
        var map = new GameState(seed, TestData.Data, MapSize.Normal, access: ZoneAccess.Normal).Map;
        var cells = Cells(map).ToList();
        var bridges = cells.Where(c => map.GetRiver(c) == RiverKind.Bridge).ToList();

        Assert.Contains(cells, c => map.GetRiver(c) == RiverKind.River);
        Assert.NotEmpty(bridges);
        foreach (var bridge in bridges)
        {
            // le pont : une case où l'on marche, rien n'y est posé, et son gardien se tient tout près
            Assert.True(map.IsPassable(bridge), $"pont infranchissable en {bridge}");
            Assert.Null(map.OccupantAt(bridge));
            Assert.True(Enumerable.Range(-2, 5).SelectMany(dy => Enumerable.Range(-2, 5).Select(dx => bridge.Offset(dx, dy)))
                .Any(c => map.InBounds(c) && map.ObjectAt(c) is MonsterGuard), $"pont sans gardien en {bridge}");
        }
    }

    [Theory]
    [InlineData(42)]
    [InlineData(5)]
    [InlineData(11)]
    public void Les_ruisseaux_se_traversent_et_rien_n_y_est_pose(int seed)
    {
        var map = new GameState(seed, TestData.Data, MapSize.Normal, access: ZoneAccess.Normal).Map;
        var streams = Cells(map).Where(c => map.GetRiver(c) == RiverKind.Stream).ToHashSet();

        Assert.NotEmpty(streams);
        foreach (var cell in streams)
        {
            Assert.True(map.IsPassable(cell), $"ruisseau infranchissable en {cell}");
            Assert.Null(map.OccupantAt(cell));
            // un ruisseau : une ligne de cases, chacune reliée à deux au plus (pas de flaque)
            Assert.True(Sides.Count(d => streams.Contains(cell.Offset(d.X, d.Y))) <= 2, $"ruisseau trop large en {cell}");
            // une route le croise tout droit
            if (map.HasRoad(cell))
                Assert.True(Sides.Count(d => map.InBounds(cell.Offset(d.X, d.Y)) && map.HasRoad(cell.Offset(d.X, d.Y))) <= 2, $"route le long du ruisseau en {cell}");
        }
    }

    [Fact]
    public void Les_rivieres_sont_sauvegardees()
    {
        var state = new GameState(42, TestData.Data, MapSize.Small, access: ZoneAccess.Normal);
        var loaded = GameState.Load(state.Save(), TestData.Data);

        foreach (var cell in Cells(state.Map))
            Assert.Equal(state.Map.GetRiver(cell), loaded.Map.GetRiver(cell));
    }
}
