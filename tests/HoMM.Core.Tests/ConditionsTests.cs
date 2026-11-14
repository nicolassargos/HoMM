using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Conditions de victoire et de défaite, choisies au lancement de la partie.</summary>
public class ConditionsTests
{
    [Fact]
    public void Les_conditions_se_choisissent_au_premier_jour_seulement()
    {
        var state = new GameState(42, TestData.Data);

        Assert.True(state.ChooseRules(new GameRules(VictoryCondition.CaptureTowns)));
        Assert.Equal(VictoryCondition.CaptureTowns, state.Rules.Victory);

        state.EndTurn();
        Assert.False(state.ChooseRules(GameRules.Default));
        Assert.Equal(VictoryCondition.CaptureTowns, state.Rules.Victory);
    }

    [Fact]
    public void Une_limite_de_jours_fait_perdre_la_partie()
    {
        var state = new GameState(42, TestData.Data);
        state.ChooseRules(new GameRules(VictoryCondition.CaptureTowns, DayLimit: 2));

        state.EndTurn(); // jour 1
        state.EndTurn(); // jour 2 : encore possible
        Assert.Equal(GameOutcome.Playing, state.Outcome);

        state.EndTurn(); // jour 3 : trop tard
        Assert.Equal(GameOutcome.Defeat, state.Outcome);
        Assert.Contains(state.TakeEvents(), e => e is GameEnded { Reason: GameEndReason.TimeOut });
    }

    [Fact]
    public void Prendre_toutes_les_villes_donne_la_victoire()
    {
        var state = new GameState(42, TestData.Data);
        state.ChooseRules(new GameRules(VictoryCondition.CaptureTowns));

        // Les monstres et la garnison ne sont pas le sujet : on les retire pour aller droit à la ville
        foreach (var guard in state.Map.Objects.OfType<MonsterGuard>().ToList())
            state.Map.RemoveObject(guard);
        var towns = state.Towns.ToList();
        Assert.True(towns.Count >= 2);
        foreach (var town in towns)
            for (int i = town.Garrison.Stacks.Count - 1; i >= 0; i--)
                town.Garrison.MoveStackTo(i, new Army());

        var hero = state.CurrentHero!;
        hero.BaseMovement = 100_000;
        foreach (var town in towns)
        {
            Assert.Equal(GameOutcome.Playing, state.Outcome);
            hero.NewDay();
            state.SetDestination(town.Position);
            for (int i = 0; i < 500 && state.CanStartWalking; i++)
                state.StepAlongPath();
            Assert.Same(state.CurrentPlayer, town.Owner);
        }

        Assert.Equal(GameOutcome.Victory, state.Outcome);
        Assert.Contains(state.TakeEvents(), e => e is GameEnded { Reason: GameEndReason.TownsCaptured });
    }
}
