using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Quand l'affichage joue les combats (QuickBattles à false), la partie s'arrête au début de chaque combat, puis
/// reprend le pas une fois le combat fini (FinishBattle).
/// </summary>
public class PauseCombatTests
{
    /// <summary>Une partie où un monstre attend à deux cases du héros, et un coffre juste à côté du monstre.</summary>
    private static (GameState State, MonsterGuard Guard, Cell Next) GameWithGuard(int count = 10, string creature = "gobelin")
    {
        var state = new GameState(7, TestData.Data, 54, 40, startingTowns: false) { QuickBattles = false };
        var start = state.CurrentHero!.Position;
        var guard = new MonsterGuard(start.Offset(2, 0), TestData.Creatures.Get(creature), count);
        state.Map.AddObject(guard);
        state.TakeEvents();
        return (state, guard, start.Offset(1, 0));
    }

    [Fact]
    public void La_partie_attend_la_fin_du_combat()
    {
        var (state, guard, next) = GameWithGuard();
        state.SetDestination(next);

        state.StepAlongPath();

        var battle = Assert.IsType<TacticalBattle>(state.CurrentBattle);
        Assert.False(battle.IsOver);
        Assert.Contains(state.TakeEvents(), e => e is BattleStarted started && started.Defender == guard);
        Assert.Contains(state.Map.Objects, o => o == guard); // pas encore de bilan
        Assert.False(state.FinishBattle()); // le combat n'est pas fini
        Assert.Throws<System.InvalidOperationException>(() => state.Save());
        int day = state.Day;
        state.EndTurn();
        Assert.Equal(day, state.Day); // pas de fin de tour pendant un combat
    }

    [Fact]
    public void Apres_le_combat_le_bilan_est_fait_et_le_pas_reprend()
    {
        var (state, guard, next) = GameWithGuard();
        state.SetDestination(next);
        state.StepAlongPath();

        BattleAI.PlayOut(state.CurrentBattle!);
        Assert.True(state.FinishBattle());

        Assert.Null(state.CurrentBattle);
        var fought = Assert.Single(state.TakeEvents().OfType<BattleFought>());
        Assert.True(fought.Report.AttackerWon);
        Assert.DoesNotContain(state.Map.Objects, o => o == guard);
        Assert.Equal(next, state.CurrentHero!.Position);
        state.Save(); // on peut de nouveau sauvegarder
    }

    [Fact]
    public void Fuir_ramene_le_heros_sur_sa_case_sans_mouvement()
    {
        var (state, guard, next) = GameWithGuard(10, "dragon");
        var hero = state.CurrentHero!;
        var start = hero.Position;
        state.SetDestination(next);
        state.StepAlongPath();
        var battle = state.CurrentBattle!;

        // on joue jusqu'au tour d'une troupe du héros, puis on fuit
        while (battle.Active!.Side != BattleSide.Attacker)
            BattleAI.PlayTurn(battle);
        Assert.True(battle.Flee());
        Assert.True(state.FinishBattle());

        Assert.Equal(start, hero.Position);
        Assert.Equal(0, hero.Movement);
        Assert.Contains(state.Map.Objects, o => o == guard); // le monstre est toujours là
        Assert.Contains(hero, state.Players[0].Heroes);
        Assert.True(state.TakeEvents().OfType<BattleFought>().Single().Report.Fled);
    }
}
