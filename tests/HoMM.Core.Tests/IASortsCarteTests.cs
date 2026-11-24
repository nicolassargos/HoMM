using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>L'adversaire lance les sorts de la carte : le vent porteur, l'invocation de bateau, la porte dimensionnelle, le retour en ville.</summary>
public class IASortsCarteTests
{
    /// <summary>
    /// L'adversaire à son tour, en Facile (le joueur humain est laissé tranquille), sans aucun objet sur la carte, toute
    /// en herbe : son héros connaît ce sort et a ses points.
    /// </summary>
    private static (GameState State, Hero Rival) RivalTurn(string spell)
    {
        var state = new GameState(4, TestData.Data, 54, 40, opponents: 1, startingTowns: false);
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents, Difficulty: Difficulty.Easy));
        state.EndTurn();
        Assert.True(state.CurrentPlayer.IsComputer);
        var rival = state.CurrentPlayer.Heroes[0];
        var map = state.Map;
        foreach (var obj in map.Objects.ToList())
            map.RemoveObject(obj);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            map.SetTerrain(new Cell(x, y), TerrainType.Grass);
            map.SetRoad(new Cell(x, y), false);
        }
        rival.Learn(TestData.Spells.Get(spell));
        rival.SpellPoints = 100;
        return (state, rival);
    }

    /// <summary>
    /// Une mer sur toute la hauteur, de 3 à 6 cases du héros (vers le milieu de la carte), toute la carte connue de son
    /// joueur, et un tas d'or sur l'autre rive : le héros ne peut pas l'atteindre à pied.
    /// </summary>
    private static ResourcePile SeaBetween(GameState state, Hero rival)
    {
        var map = state.Map;
        int side = rival.Position.X < map.Width / 2 ? 1 : -1;
        for (int y = 0; y < map.Height; y++)
            for (int k = 3; k <= 6; k++)
                map.SetTerrain(new Cell(rival.Position.X + side * k, y), TerrainType.Water);
        rival.Owner.Fog.Reveal(rival.Position, map.Width + map.Height);
        var pile = new ResourcePile(new Cell(rival.Position.X + side * 8, rival.Position.Y), ResourceType.Gold, 700);
        map.AddObject(pile);
        return pile;
    }

    [Fact]
    public void Le_vent_porteur_au_debut_de_la_marche()
    {
        var (state, rival) = RivalTurn("vent-porteur");
        var cell = new Cell(rival.Position.X + (rival.Position.X < state.Map.Width / 2 ? 3 : -3), rival.Position.Y);
        state.Map.AddObject(new ResourcePile(cell, ResourceType.Gold, 700));

        ComputerPlayer.PlayTurn(state);

        Assert.True(rival.DoubledMovementToday);
        Assert.Equal(100 - 15, rival.SpellPoints);
    }

    [Fact]
    public void Sans_but_pas_de_vent_porteur()
    {
        var (state, rival) = RivalTurn("vent-porteur");
        rival.Owner.Fog.Reveal(rival.Position, state.Map.Width + state.Map.Height); // rien à explorer non plus

        ComputerPlayer.PlayTurn(state);

        Assert.False(rival.DoubledMovementToday);
    }

    [Fact]
    public void La_porte_dimensionnelle_franchit_la_mer()
    {
        var (state, rival) = RivalTurn("porte-dimensionnelle");
        var pile = SeaBetween(state, rival);
        int gold = rival.Owner.Resources[ResourceType.Gold];

        ComputerPlayer.PlayTurn(state);

        Assert.Equal(1, rival.DimensionDoorsToday);
        Assert.DoesNotContain(pile, state.Map.Objects);
        Assert.Equal(gold + 700, rival.Owner.Resources[ResourceType.Gold]);
    }

    [Fact]
    public void L_invocation_de_bateau_au_bord_de_l_eau()
    {
        var (state, rival) = RivalTurn("invocation-bateau");
        SeaBetween(state, rival);
        rival.Owner.Resources[ResourceType.Gold] = 0; // pas de quoi acheter un bateau

        ComputerPlayer.PlayTurn(state);

        Assert.Equal(100 - 12, rival.SpellPoints);
        Assert.True(rival.AtSea(state.Map) || state.Map.Objects.OfType<Boat>().Any());
    }

    [Fact]
    public void Le_retour_en_ville_quand_il_n_a_plus_rien_a_faire()
    {
        var (state, rival) = RivalTurn("retour-en-ville");
        rival.Owner.Fog.Reveal(rival.Position, state.Map.Width + state.Map.Height);
        var p = rival.Position;
        var town = new Town(new Cell(p.X + (p.X < state.Map.Width / 2 ? 16 : -16), Math.Clamp(p.Y, 3, state.Map.Height - 4)),
            "Loinville", "chateau", TestData.Data);
        state.Map.AddObject(town);
        town.SetOwner(rival.Owner);

        ComputerPlayer.PlayTurn(state);

        Assert.Equal(town.Position, rival.Position);
    }

    [Fact]
    public void Pas_de_retour_en_ville_tout_pres_d_elle()
    {
        var (state, rival) = RivalTurn("retour-en-ville");
        rival.Owner.Fog.Reveal(rival.Position, state.Map.Width + state.Map.Height);
        var p = rival.Position;
        var town = new Town(new Cell(p.X + (p.X < state.Map.Width / 2 ? 5 : -5), Math.Clamp(p.Y, 3, state.Map.Height - 4)),
            "Prèsville", "chateau", TestData.Data);
        state.Map.AddObject(town);
        town.SetOwner(rival.Owner);

        ComputerPlayer.PlayTurn(state);

        Assert.Equal(100, rival.SpellPoints);
    }
}
