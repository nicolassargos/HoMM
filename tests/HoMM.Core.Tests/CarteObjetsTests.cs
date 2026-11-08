using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les objets et lieux de la carte : forêts, coffres au choix, et les lieux à visiter.</summary>
public class CarteObjetsTests
{
    /// <summary>Une partie où le héros est juste à côté d'un objet (posé à sa droite), sans monstre autour.</summary>
    internal static (GameState State, T Object) GameNextTo<T>(Func<Cell, T> make) where T : MapObject
    {
        var state = new GameState(42, TestData.Data, MapSize.Normal, startingTowns: false);
        state.TakeEvents();
        var hero = state.CurrentHero!;
        var cell = hero.Position.Offset(1, 0);
        foreach (var obj in state.Map.Objects.Where(o => o.Footprint.Contains(cell) || o is MonsterGuard g
                     && Math.Max(Math.Abs(g.Position.X - cell.X), Math.Abs(g.Position.Y - cell.Y)) <= 2).ToList())
            state.Map.RemoveObject(obj);
        state.Map.SetTerrain(cell, TerrainType.Grass);
        state.Map.SetTerrain(hero.Position.Offset(-1, 0), TerrainType.Grass);
        var placed = make(cell);
        state.Map.AddObject(placed);
        return (state, placed);
    }

    /// <summary>Le héros entre sur la case de l'objet.</summary>
    internal static void Visit(GameState state, MapObject obj)
    {
        state.SetDestination(obj.Position);
        state.StepAlongPath();
    }

    [Fact]
    public void La_foret_bloque_le_passage()
    {
        Assert.False(TerrainInfo.IsPassable(TerrainType.Forest));
        var map = new AdventureMap(3, 1);
        map.SetTerrain(new Cell(1, 0), TerrainType.Forest);
        Assert.Null(Pathfinder.FindPath(map, new Cell(0, 0), new Cell(2, 0)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Un_coffre_donne_l_or_ou_l_experience_au_choix(bool gold)
    {
        var (state, chest) = GameNextTo(c => new TreasureChest(c, 1500));
        var hero = state.CurrentHero!;
        int goldBefore = hero.Owner.Resources[ResourceType.Gold];

        Visit(state, chest);

        var opened = Assert.Single(state.TakeEvents().OfType<ChestOpened>());
        Assert.DoesNotContain(state.Map.Objects, o => o == chest);
        Assert.Equal(goldBefore, hero.Owner.Resources[ResourceType.Gold]); // rien tant qu'on n'a pas choisi
        Assert.Single(state.PendingChests);

        Assert.True(state.ChooseChest(opened, gold));

        Assert.Empty(state.PendingChests);
        Assert.False(state.ChooseChest(opened, gold)); // une seule fois
        Assert.Equal(goldBefore + (gold ? 1500 : 0), hero.Owner.Resources[ResourceType.Gold]);
        Assert.Equal(gold ? 0 : 1000, hero.Experience);
        Assert.Contains(state.TakeEvents(), e => e is ChestChosen c && c.Gold == gold);
    }

    [Fact]
    public void Un_coffre_qui_attend_son_choix_est_sauvegarde()
    {
        var (state, chest) = GameNextTo(c => new TreasureChest(c, 2000));
        Visit(state, chest);

        var loaded = GameState.Load(state.Save(), TestData.Data);

        var pending = Assert.Single(loaded.PendingChests);
        Assert.Equal(2000, pending.Chest.Gold);
        Assert.True(loaded.ChooseChest(pending, gold: false));
        Assert.Equal(1500, loaded.CurrentHero!.Experience);
    }
}
