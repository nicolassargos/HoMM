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
