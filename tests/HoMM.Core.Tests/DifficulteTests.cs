using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les niveaux de difficulté (étape 8, point 6) : des profils de réglages de l'adversaire.</summary>
public class DifficulteTests
{
    /// <summary>Une partie à un adversaire, avec cette difficulté, son héros fort à côté du héros faible du joueur.</summary>
    private static (GameState State, Hero Mine, Hero Rival) FaceToFace(Difficulty difficulty)
    {
        var state = new GameState(4, TestData.Data, MapSize.Tiny, opponents: 1);
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents, Difficulty: difficulty));
        var me = state.Players[0].Heroes[0];
        var him = state.Players[1].Heroes[0];
        him.Position = new[] { me.Position.Offset(1, 0), me.Position.Offset(0, 1), me.Position.Offset(1, 1) }
            .First(c => state.Map.IsPassable(c) && state.Map.ObjectAt(c) == null && !state.Map.IsGuarded(c));
        him.Owner.Fog.Reveal(him.Position, 4);
        while (me.Army.Stacks.Count > 1)
            me.Army.MoveStackTo(1, new Army());
        return (state, me, him);
    }

    [Fact]
    public void En_facile_l_adversaire_epargne_le_joueur_au_debut()
    {
        var (state, me, _) = FaceToFace(Difficulty.Easy);
        state.EndTurn();
        ComputerPlayer.PlayTurn(state);
        Assert.Contains(me, state.Players[0].Heroes);

        var (normal, mine, _) = FaceToFace(Difficulty.Normal);
        normal.EndTurn();
        ComputerPlayer.PlayTurn(normal);
        Assert.DoesNotContain(mine, normal.Players[0].Heroes);
    }

    [Fact]
    public void En_difficile_l_adversaire_gagne_plus_d_or()
    {
        int GoldAfterADay(Difficulty difficulty)
        {
            var state = new GameState(4, TestData.Data, MapSize.Tiny, opponents: 1);
            state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents, Difficulty: difficulty));
            var rival = state.Players[1];
            var mine = new Mine(rival.Heroes[0].Position.Offset(0, 0), ResourceType.Gold);
            mine.Visit(rival.Heroes[0]); // la mine d'or est à lui
            int before = rival.Resources[ResourceType.Gold];
            state.EndTurn(); // au tour de l'adversaire
            state.EndTurn(); // nouveau jour : les revenus
            return rival.Resources[ResourceType.Gold] - before;
        }
        int easy = GoldAfterADay(Difficulty.Easy), normal = GoldAfterADay(Difficulty.Normal), hard = GoldAfterADay(Difficulty.Hard);
        Assert.True(easy < normal && normal < hard, $"facile {easy}, normal {normal}, difficile {hard}");
    }

    [Fact]
    public void La_difficulte_est_sauvegardee()
    {
        var state = new GameState(4, TestData.Data, MapSize.Tiny, opponents: 1);
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents, Difficulty: Difficulty.Hard));
        Assert.Equal(Difficulty.Hard, GameState.Load(state.Save(), TestData.Data).Rules.Difficulty);
    }
}
