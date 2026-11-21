using System;
using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;
using static HoMM.Core.Tests.CarteObjetsTests;

namespace HoMM.Core.Tests;

/// <summary>
/// Les portails (2026-11-21) : une poche de terre où aucun chemin ne mène s'atteint par un portail, gardé par une forte
/// armée ; elle cache une grande récompense.
/// </summary>
public class PortailsTests
{
    /// <summary>Une partie où le héros est à côté d'un portail qui mène à <paramref name="far"/> (et retour).</summary>
    private static (GameState State, Hero Hero, Portal Portal) GameNextToPortal(Cell far)
    {
        var (state, portal) = GameNextTo(c => new Portal(c, far));
        foreach (var c in TallSite.FootprintAt(far))
        {
            if (state.Map.ObjectAt(c) is { } there)
                state.Map.RemoveObject(there);
            state.Map.SetTerrain(c, TerrainType.Grass);
        }
        state.Map.AddObject(new Portal(far, portal.Position));
        return (state, state.CurrentHero!, portal);
    }

    [Fact]
    public void Le_portail_mene_a_l_autre()
    {
        var far = new Cell(30, 30);
        var (state, hero, portal) = GameNextToPortal(far);

        Visit(state, portal);

        Assert.Equal(far, hero.Position);
        Assert.Null(state.Path);
        Assert.Contains(state.TakeEvents(), e => e is PortalCrossed { Exit: { } exit } && exit == far);
        Assert.NotNull(state.Map.ObjectAt(portal.Position)); // il reste
    }

    [Fact]
    public void Un_heros_a_la_sortie_empeche_de_passer()
    {
        var far = new Cell(30, 30);
        var (state, hero, portal) = GameNextToPortal(far);
        var other = new Hero("Autre", hero.Owner, far);
        hero.Owner.Heroes.Add(other);

        Visit(state, portal);

        Assert.Equal(portal.Position, hero.Position);
        Assert.Contains(state.TakeEvents(), e => e is PortalCrossed { Exit: null });
    }

    [Fact]
    public void Les_portails_se_sauvegardent()
    {
        var (state, _, portal) = GameNextToPortal(new Cell(30, 30));

        var loaded = GameState.Load(state.Save(), TestData.Data).Map;

        Assert.Equal(new Cell(30, 30), Assert.IsType<Portal>(loaded.ObjectAt(portal.Position)).Exit);
        Assert.Equal(portal.Position, Assert.IsType<Portal>(loaded.ObjectAt(new Cell(30, 30))).Exit);
    }

    [Fact]
    public void Le_chemin_passe_par_le_portail()
    {
        // deux moitiés séparées par une rangée de forêt, un portail de chaque côté
        var map = new AdventureMap(20, 6);
        for (int y = 0; y < 6; y++)
            map.SetTerrain(new Cell(10, y), TerrainType.Forest);
        map.AddObject(new Portal(new Cell(3, 3), new Cell(15, 3)));
        map.AddObject(new Portal(new Cell(15, 3), new Cell(3, 3)));

        var path = Pathfinder.FindPath(map, new Cell(1, 3), new Cell(18, 3));

        Assert.NotNull(path);
        int entrance = path!.IndexOf(new Cell(3, 3));
        Assert.Equal(new Cell(15, 3), path[entrance + 1]);
        Assert.Equal(0, Pathfinder.StepCost(map, new Cell(3, 3), new Cell(15, 3)));
        // un héros à la sortie : plus de chemin
        Assert.Null(Pathfinder.FindPath(map, new Cell(1, 3), new Cell(18, 3), new HashSet<Cell> { new Cell(15, 3) }));
    }

    [Fact]
    public void Le_heros_continue_sa_marche_de_l_autre_cote()
    {
        var far = new Cell(30, 30);
        var (state, hero, portal) = GameNextToPortal(far);
        var target = far.Offset(2, 1);
        if (state.Map.ObjectAt(target) is { } there)
            state.Map.RemoveObject(there);
        foreach (var guard in state.Map.Objects.OfType<MonsterGuard>().Where(g => Math.Max(Math.Abs(g.Position.X - far.X), Math.Abs(g.Position.Y - far.Y)) <= 4).ToList())
            state.Map.RemoveObject(guard);
        foreach (var c in new[] { far.Offset(1, 0), far.Offset(1, 1), target })
            state.Map.SetTerrain(c, TerrainType.Grass);

        Assert.Equal(1, state.DaysTo(target));
        state.SetDestination(target);
        Assert.Contains(portal.Position, state.Path!);
        while (state.StepAlongPath())
        {
        }

        Assert.Equal(target, hero.Position);
        Assert.Null(state.Path);
    }

    [Fact]
    public void Un_adversaire_prend_le_portail_pour_voir_l_autre_cote()
    {
        var state = new GameState(3, TestData.Data, 54, 40, opponents: 1, startingTowns: false);
        state.EndTurn();
        var hero = state.CurrentPlayer.Heroes[0];
        var map = state.Map;
        // rien d'autre à faire autour de lui
        foreach (var obj in map.Objects.Where(o => o is not Town && Math.Max(Math.Abs(o.Position.X - hero.Position.X),
                     Math.Abs(o.Position.Y - hero.Position.Y)) <= 12).ToList())
            map.RemoveObject(obj);
        var cell = hero.Position.Offset(hero.Position.X < 27 ? 2 : -2, 0);
        var far = new Cell(hero.Position.X < 27 ? 50 : 3, hero.Position.Y < 20 ? 35 : 4);
        foreach (var c in TallSite.FootprintAt(cell).Concat(TallSite.FootprintAt(far)).Append(hero.Position.Offset(Math.Sign(cell.X - hero.Position.X), 0)))
        {
            if (map.ObjectAt(c) is { } there)
                map.RemoveObject(there);
            map.SetTerrain(c, TerrainType.Grass);
        }
        map.AddObject(new Portal(cell, far));
        map.AddObject(new Portal(far, cell));
        Assert.False(hero.Owner.Fog.IsExplored(far));

        ComputerPlayer.PlayTurn(state);

        Assert.True(hero.Owner.Fog.IsExplored(far));
    }

    /// <summary>
    /// Sur la carte générée : les portails vont par paires ; celui du dehors (atteint à pied depuis le départ) a un gardien
    /// à côté, plus fort que ceux de sa zone ; celui du dedans mène à au moins deux récompenses.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(10)]
    public void Une_poche_fermee_a_un_portail_garde_et_une_recompense(int seed)
    {
        var map = new GameState(seed, TestData.Data, MapSize.Normal).Map;
        var portals = map.Objects.OfType<Portal>().ToList();
        Assert.NotEmpty(portals);

        // à pied depuis le départ, sans les portails, en battant les monstres
        var outside = Flood(map, map.Start, o => o is MonsterGuard);
        foreach (var portal in portals)
        {
            var other = Assert.IsType<Portal>(map.ObjectAt(portal.Exit));
            Assert.Equal(portal.Position, other.Exit);
            if (!outside.Contains(portal.Position))
                continue;
            Assert.DoesNotContain(portal.Exit, outside);
            Assert.Contains(Around(portal.Position), c => map.ObjectAt(c) is MonsterGuard);
            // la poche : depuis le portail du dedans, les récompenses qu'on y touche
            var inside = Flood(map, portal.Exit, _ => false);
            var rewards = inside.SelectMany(Around).Distinct()
                .Count(c => map.ObjectAt(c) is TreasureChest or ResourcePile && map.ObjectAt(c)!.Position == c);
            Assert.True(rewards >= 2, $"{rewards} récompense(s) derrière le portail en {portal.Position}");
        }
    }

    private static IEnumerable<Cell> Around(Cell c)
    {
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if (dx != 0 || dy != 0)
                yield return c.Offset(dx, dy);
    }

    /// <summary>Les cases atteintes à pied depuis <paramref name="from"/> (sans couper un coin bloqué) ; un objet arrête, sauf ceux qu'on traverse.</summary>
    private static HashSet<Cell> Flood(AdventureMap map, Cell from, Func<MapObject, bool> crossed)
    {
        var seen = new HashSet<Cell> { from };
        var queue = new Queue<Cell>(seen);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c != from && map.ObjectAt(c) is { } obj && !crossed(obj))
                continue;
            foreach (var n in Around(c))
                if (map.InBounds(n) && map.IsPassable(n) && map.IsPassable(new Cell(n.X, c.Y)) && map.IsPassable(new Cell(c.X, n.Y)) && seen.Add(n))
                    queue.Enqueue(n);
        }
        return seen;
    }
}
