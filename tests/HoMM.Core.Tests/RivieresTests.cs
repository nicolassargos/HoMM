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
            // rien n'y est posé, sauf la roue d'une roue à eau, qui tourne dedans
            if (map.OccupantAt(cell) is not WeeklyProducer { IsWaterWheel: true })
            {
                Assert.True(map.IsPassable(cell), $"ruisseau infranchissable en {cell}");
                Assert.Null(map.OccupantAt(cell));
            }
            // un ruisseau : une ligne de cases, chacune reliée à deux au plus, trois là où un affluent le rejoint (pas de flaque)
            Assert.True(Sides.Count(d => streams.Contains(cell.Offset(d.X, d.Y))) <= 3, $"ruisseau trop large en {cell}");
            // une route le croise tout droit
            if (map.HasRoad(cell))
                Assert.True(Sides.Count(d => map.InBounds(cell.Offset(d.X, d.Y)) && map.HasRoad(cell.Offset(d.X, d.Y))) <= 2, $"route le long du ruisseau en {cell}");
        }
    }

    [Theory]
    [InlineData(42)]
    [InlineData(5)]
    [InlineData(11)]
    public void Le_trace_d_un_ruisseau_passe_par_ses_cases_et_finit_dans_l_eau_une_mare_ou_un_autre_ruisseau(int seed)
    {
        var map = new GameState(seed, TestData.Data, MapSize.Normal, access: ZoneAccess.Normal).Map;
        var streams = Cells(map).Where(c => map.GetRiver(c) == RiverKind.Stream).ToHashSet();
        static Cell CellOf(CoursePoint p) => new((int)System.Math.Floor(p.X), (int)System.Math.Floor(p.Y));

        Assert.NotEmpty(map.Streams);
        var touched = new HashSet<Cell>();
        foreach (var course in map.Streams)
        {
            // de la source au bout, sans le dernier point quand il est dans l'eau ou dans un autre ruisseau
            var land = course.End == StreamEnd.Pond ? course.Points : course.Points.Take(course.Points.Count - 1).ToList();
            foreach (var point in land)
                Assert.Contains(CellOf(point), streams);
            touched.UnionWith(land.Select(CellOf));
            // d'un point au suivant : la même case ou une voisine par un côté (le tracé ne saute pas de case)
            for (int i = 0; i + 1 < land.Count; i++)
            {
                var (a, b) = (CellOf(land[i]), CellOf(land[i + 1]));
                Assert.True(System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y) <= 1, $"le tracé saute de {a} à {b}");
            }
            var last = CellOf(course.Points[^1]);
            if (course.End == StreamEnd.Mouth)
                Assert.True(map.IsWater(last) || map.GetRiver(last) == RiverKind.River, $"embouchure hors de l'eau en {last}");
            else if (course.End == StreamEnd.Join)
                Assert.Contains(last, streams);
        }
        // toutes les cases de ruisseau viennent d'un tracé
        Assert.Equal(streams.OrderBy(c => c.Y).ThenBy(c => c.X), touched.OrderBy(c => c.Y).ThenBy(c => c.X));
    }

    [Fact]
    public void Les_rivieres_sont_sauvegardees()
    {
        var state = new GameState(42, TestData.Data, MapSize.Small, access: ZoneAccess.Normal);
        var loaded = GameState.Load(state.Save(), TestData.Data);

        foreach (var cell in Cells(state.Map))
            Assert.Equal(state.Map.GetRiver(cell), loaded.Map.GetRiver(cell));
        // et les tracés des ruisseaux, au millième de case
        Assert.Equal(state.Map.Streams.Count, loaded.Map.Streams.Count);
        foreach (var (saved, back) in state.Map.Streams.Zip(loaded.Map.Streams))
        {
            Assert.Equal(saved.End, back.End);
            Assert.Equal(saved.Points.Count, back.Points.Count);
            foreach (var (p, q) in saved.Points.Zip(back.Points))
            {
                Assert.Equal(p.X, q.X, 3);
                Assert.Equal(p.Y, q.Y, 3);
            }
        }
    }
}
