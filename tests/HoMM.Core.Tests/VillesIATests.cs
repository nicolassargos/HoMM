using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>L'adversaire construit, recrute et engage des héros dans ses villes (étape 8, point 4).</summary>
public class VillesIATests
{
    /// <summary>Une partie à un adversaire qui prend, à son premier tour, une ville sans garnison posée près de lui.</summary>
    private static (GameState State, Player Rival, Town Town) RivalWithTown()
    {
        var state = new GameState(4, TestData.Data, MapSize.Tiny, opponents: 1);
        // les monstres seront retirés : la partie ne doit pas se gagner pour autant
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
        state.EndTurn();
        var rival = state.CurrentPlayer;
        var hero = rival.Heroes[0];
        foreach (var obj in state.Map.Objects.Where(o => o is ResourcePile or TreasureChest or Mine or Obelisk or MonsterGuard or Town).ToList())
            state.Map.RemoveObject(obj);
        var town = Enumerable.Range(2, 4).SelectMany(d => new[] { hero.Position.Offset(0, d), hero.Position.Offset(d, 0), hero.Position.Offset(-d, 0) })
            .Select(c => new Town(c, "Testville", "chateau", TestData.Data))
            .First(t => t.Footprint.All(f => state.Map.InBounds(f) && state.Map.IsPassable(f) && state.Map.ObjectAt(f) == null && f != hero.Position)
                && rival.Fog.IsExplored(t.Position) && Pathfinder.FindPath(state.Map, hero.Position, t.Approach) != null);
        town.AddBuilding(TestData.Data.Buildings.Get("salle-village"));
        town.AddBuilding(TestData.Data.Buildings.Get("corps-de-garde"));
        state.Map.AddObject(town);
        ComputerPlayer.PlayTurn(state);
        Assert.Equal(rival, town.Owner);
        return (state, rival, town);
    }

    /// <summary>Des jours entiers : le joueur passe, l'adversaire joue.</summary>
    private static void PlayDays(GameState state, int days)
    {
        for (int i = 0; i < days && state.Outcome == GameOutcome.Playing; i++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
    }

    private static int ArmyValue(Player player) => player.Heroes.Sum(h => Battle.ExperienceFor(h.Army.Stacks))
        + player.Towns.Sum(t => Battle.ExperienceFor(t.Garrison.Stacks));

    [Fact]
    public void L_adversaire_construit_et_recrute()
    {
        var (state, rival, town) = RivalWithTown();
        int buildings = town.BuiltBuildings.Count();
        int value = ArmyValue(rival);

        PlayDays(state, 7);

        Assert.True(town.BuiltBuildings.Count() > buildings);
        Assert.True(ArmyValue(rival) > value);
    }

    [Fact]
    public void Riche_avec_une_taverne_l_adversaire_engage_un_heros()
    {
        var (state, rival, town) = RivalWithTown();
        town.AddBuilding(TestData.Data.Buildings.Get("taverne"));
        rival.Resources[ResourceType.Gold] = 20000;
        PlayDays(state, 3);

        Assert.True(rival.Heroes.Count >= 2);
    }
}
