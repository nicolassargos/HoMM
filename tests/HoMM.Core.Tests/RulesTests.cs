using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

public class MapGeneratorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Tous_les_objets_sont_accessibles_depuis_le_depart(int seed)
    {
        var map = new MapGenerator(seed).Generate();
        foreach (var obj in map.Objects)
            Assert.NotNull(Pathfinder.FindPath(map, map.Start, obj.Position));
    }

    [Fact]
    public void La_meme_graine_donne_la_meme_carte()
    {
        var a = new MapGenerator(123).Generate();
        var b = new MapGenerator(123).Generate();
        for (int y = 0; y < a.Height; y++)
        for (int x = 0; x < a.Width; x++)
            Assert.Equal(a.GetTerrain(new Cell(x, y)), b.GetTerrain(new Cell(x, y)));
        Assert.Equal(a.Objects.Select(o => o.Position).OrderBy(p => p.Y).ThenBy(p => p.X),
                     b.Objects.Select(o => o.Position).OrderBy(p => p.Y).ThenBy(p => p.X));
    }

    [Fact]
    public void Une_petite_carte_pose_ce_qui_rentre_sans_planter()
    {
        var map = new MapGenerator(5).Generate(6, 6);
        Assert.NotEmpty(map.Objects);
        Assert.True(map.Objects.Count < ResourceInfo.All.Length + 6 + 28);
    }
}

public class PathfinderTests
{
    private static AdventureMap FlatMap()
    {
        var map = new AdventureMap(10, 10);
        for (int y = 0; y < 10; y++)
        for (int x = 0; x < 10; x++)
            map.SetTerrain(new Cell(x, y), TerrainType.Grass);
        return map;
    }

    [Fact]
    public void Une_diagonale_coute_plus_cher_qu_un_pas_droit()
    {
        var map = FlatMap();
        Assert.Equal(100, Pathfinder.StepCost(map, new Cell(1, 1), new Cell(2, 1)));
        Assert.Equal(141, Pathfinder.StepCost(map, new Cell(1, 1), new Cell(2, 2)));
    }

    [Fact]
    public void Le_chemin_contourne_l_eau()
    {
        var map = FlatMap();
        for (int y = 0; y < 9; y++)
            map.SetTerrain(new Cell(5, y), TerrainType.Water);
        var path = Pathfinder.FindPath(map, new Cell(2, 2), new Cell(8, 2));
        Assert.NotNull(path);
        Assert.DoesNotContain(path!, c => map.GetTerrain(c) == TerrainType.Water);
        Assert.Contains(new Cell(5, 9), path!);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Le_chemin_trouve_est_le_moins_cher(int seed)
    {
        var map = new MapGenerator(seed).Generate();
        var best = CheapestCosts(map, map.Start);
        foreach (var obj in map.Objects)
        {
            var path = Pathfinder.FindPath(map, map.Start, obj.Position)!;
            int cost = 0;
            var previous = map.Start;
            foreach (var cell in path)
            {
                cost += Pathfinder.StepCost(map, previous, cell);
                previous = cell;
            }
            Assert.Equal(best[obj.Position], cost);
        }
    }

    /// <summary>Dijkstra simple (sans estimation), avec les mêmes règles que le Pathfinder.</summary>
    private static Dictionary<Cell, int> CheapestCosts(AdventureMap map, Cell start)
    {
        var best = new Dictionary<Cell, int> { [start] = 0 };
        var open = new PriorityQueue<Cell, int>();
        open.Enqueue(start, 0);
        while (open.TryDequeue(out var cell, out int cost))
        {
            if (cost > best[cell] || (cell != start && map.ObjectAt(cell) != null))
                continue;
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var next = cell.Offset(dx, dy);
                if ((dx == 0 && dy == 0) || !map.IsPassable(next))
                    continue;
                if (dx != 0 && dy != 0 && !(map.IsPassable(cell.Offset(dx, 0)) && map.IsPassable(cell.Offset(0, dy))))
                    continue;
                int newCost = cost + Pathfinder.StepCost(map, cell, next);
                if (newCost < best.GetValueOrDefault(next, int.MaxValue))
                {
                    best[next] = newCost;
                    open.Enqueue(next, newCost);
                }
            }
        }
        return best;
    }

    [Fact]
    public void Pas_de_chemin_vers_une_montagne()
    {
        var map = FlatMap();
        map.SetTerrain(new Cell(4, 4), TerrainType.Mountain);
        Assert.Null(Pathfinder.FindPath(map, new Cell(0, 0), new Cell(4, 4)));
    }
}

public class GameStateTests
{
    [Fact]
    public void Une_mine_capturee_rapporte_a_chaque_fin_de_tour()
    {
        var state = new GameState(7);
        var mine = state.Map.Objects.OfType<Mine>().First(m => m.Resource == ResourceType.Gold);
        mine.Visit(state.Player);
        int gold = state.Player.Resources[ResourceType.Gold];

        state.EndTurn();

        Assert.Equal(gold + 1000, state.Player.Resources[ResourceType.Gold]);
    }

    [Fact]
    public void La_fin_du_tour_rend_les_points_de_mouvement()
    {
        var state = new GameState(7);
        var target = state.Map.Objects.First().Position;
        state.SetDestination(target);
        while (state.StepAlongPath()) { }
        Assert.True(state.Hero.Movement < state.Hero.MaxMovement);

        state.EndTurn();

        Assert.Equal(state.Hero.MaxMovement, state.Hero.Movement);
        Assert.Equal(1, state.Day);
    }

    [Fact]
    public void Ramasser_un_tas_ajoute_la_ressource_et_le_retire_de_la_carte()
    {
        var state = new GameState(7);
        var pile = state.Map.Objects.OfType<ResourcePile>()
            .OrderBy(p => Pathfinder.FindPath(state.Map, state.Hero.Position, p.Position)!.Count).First();
        int before = state.Player.Resources[pile.Resource];

        state.SetDestination(pile.Position);
        for (int day = 0; day < 10 && state.Hero.Position != pile.Position; day++)
        {
            while (state.StepAlongPath()) { }
            state.EndTurn();
        }

        Assert.Equal(before + pile.Amount, state.Player.Resources[pile.Resource]);
        Assert.Null(state.Map.ObjectAt(pile.Position));
    }
}
