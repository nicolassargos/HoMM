using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les héros s'affrontent (étape 8, point 5) : attaquer un héros adverse, se défendre, être éliminé.</summary>
public class HerosContreHerosTests
{
    /// <summary>Une partie à un adversaire, dont le héros est posé à côté du héros du joueur, avec ces armées.</summary>
    private static (GameState State, Hero Mine, Hero Rival) FaceToFace(int mine, int rival)
    {
        var state = new GameState(4, TestData.Data, MapSize.Tiny, opponents: 1);
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
        var me = state.Players[0].Heroes[0];
        var him = state.Players[1].Heroes[0];
        var next = new[] { me.Position.Offset(1, 0), me.Position.Offset(0, 1), me.Position.Offset(1, 1) }
            .First(c => state.Map.IsPassable(c) && state.Map.ObjectAt(c) == null && !state.Map.IsGuarded(c));
        him.Position = next;
        him.Owner.Fog.Reveal(next, 4); // il voit ce qui l'entoure à sa nouvelle place
        SetArmy(me, mine);
        SetArmy(him, rival);
        return (state, me, him);
    }

    private static void SetArmy(Hero hero, int peasants)
    {
        while (hero.Army.Stacks.Count > 0)
            hero.Army.MoveStackTo(0, new Army());
        hero.Army.Add(TestData.Creatures.Get("paysan"), peasants);
    }

    [Fact]
    public void Attaquer_un_heros_adverse_et_l_eliminer()
    {
        var (state, me, him) = FaceToFace(mine: 80, rival: 3);

        state.SetDestination(him.Position);
        Assert.NotNull(state.Path);
        state.StepAlongPath();

        Assert.DoesNotContain(him, state.Players[1].Heroes);
        Assert.True(state.Players[1].IsDefeated);
        var events = state.TakeEvents();
        Assert.Contains(events, e => e is BattleFought { Defender: HeroDefender, DefenderOwner: { } owner } && owner == state.Players[1]);
        Assert.Contains(events, e => e is PlayerDefeated d && d.Player == state.Players[1]);
        Assert.Equal(GameOutcome.Victory, state.Outcome);
        Assert.True(me.Experience > 0);
    }

    [Fact]
    public void Le_heros_qui_se_defend_et_gagne_gagne_de_l_experience()
    {
        var (state, me, him) = FaceToFace(mine: 80, rival: 3);
        state.EndTurn(); // au tour de l'adversaire, qui attaque (mal) le héros du joueur
        state.SelectHero(him);
        state.SetDestination(me.Position);
        state.StepAlongPath();

        Assert.DoesNotContain(him, state.Players[1].Heroes);
        Assert.Contains(me, state.Players[0].Heroes);
        Assert.True(me.Experience > 0);
        Assert.Equal(1, state.Players[0].Statistics.BattlesWon);
    }

    [Fact]
    public void L_adversaire_attaque_un_heros_faible_du_joueur()
    {
        var (state, me, him) = FaceToFace(mine: 2, rival: 60);
        state.EndTurn();

        ComputerPlayer.PlayTurn(state);

        Assert.DoesNotContain(me, state.Players[0].Heroes);
        Assert.Equal(GameOutcome.Defeat, state.Outcome); // le joueur n'a plus ni héros ni ville
    }

    [Fact]
    public void Le_bonus_du_heros_qui_se_defend_compte()
    {
        var attacker = new Army();
        attacker.Add(TestData.Creatures.Get("archer"), 20);
        var defender = new Army();
        defender.Add(TestData.Creatures.Get("archer"), 20);
        var map = new AdventureMap(5, 5);
        var player = new Player("Bleu", map);
        var strong = new Hero("Fort", player, new Cell(1, 1)) { Attack = 10, Defense = 10 };

        var plain = Battle.Resolve(attacker.Clone(), defender.Clone());
        var helped = Battle.Resolve(attacker.Clone(), defender.Clone(), defenderHero: strong);

        Assert.True(helped.AttackerLosses.Sum(s => s.Count) > plain.AttackerLosses.Sum(s => s.Count));
    }
}
