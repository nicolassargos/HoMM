using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les chiffres de la partie (fin de partie, étape 7) : combats, créatures, villes ; gardés par la sauvegarde.</summary>
public class StatistiquesTests
{
    [Fact]
    public void Prendre_une_ville_compte_le_combat_les_creatures_et_la_ville()
    {
        var state = new GameState(7, TestData.Data);
        var town = new Town(state.CurrentHero!.Position.Offset(2, 0), "Testville", "chateau", TestData.Data);
        town.AddBuilding(TestData.Data.Buildings.Get("salle-village"));
        town.Garrison.Add(TestData.Creatures.Get("paysan"), 3);
        state.Map.AddObject(town);

        state.SetDestination(town.Position);
        while (state.StepAlongPath()) { }

        var stats = state.Players[0].Statistics;
        Assert.Equal(1, stats.BattlesWon);
        Assert.Equal(3, stats.CreaturesKilled);
        Assert.Equal(1, stats.TownsCaptured);

        var loaded = GameState.Load(state.Save(), TestData.Data).Players[0].Statistics;
        Assert.Equal((stats.BattlesWon, stats.CreaturesKilled, stats.CreaturesLost, stats.TownsCaptured),
            (loaded.BattlesWon, loaded.CreaturesKilled, loaded.CreaturesLost, loaded.TownsCaptured));
    }
}
