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
        // l'adversaire est un Paladin : une autre classe (le Magicien) gagnerait par ses sorts ce que ces tests ne mesurent pas
        var state = new GameState(4, TestData.Data, MapSize.Tiny, opponents: 1, startingTowns: false, opponentFactions: new[] { "chateau" });
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
    public void Les_troupes_trop_nombreuses_du_heros_attaque_peuvent_se_revolter()
    {
        // ses Fantômes dépassent de loin son charisme (95 % de chances de révolte) : ils combattent avec le joueur
        var (state, me, him) = FaceToFace(mine: 40, rival: 10);
        var ghost = TestData.Creatures.Get("fantome");
        him.Army.Add(ghost, 20 * Command.Limit(him.Charisma) / ghost.CommandCost);

        state.SetDestination(him.Position);
        state.StepAlongPath();

        var fought = state.TakeEvents().OfType<BattleFought>().Single();
        Assert.Equal(ghost, Assert.Single(fought.Report.DefenderRebels).Type);
        Assert.True(fought.Report.AttackerWon);
        Assert.DoesNotContain(me.Army.Stacks, s => s.Type == ghost); // ils ne rejoignent pas le vainqueur
    }

    [Fact]
    public void Les_revoltes_du_defenseur_ne_gagnent_pas_a_la_place_de_l_attaquant()
    {
        var attacker = new Army();
        attacker.Add(TestData.Creatures.Get("paysan"), 1);
        var defender = new Army();
        defender.Add(TestData.Creatures.Get("ange"), 1);
        var rebels = new Army();
        rebels.Add(TestData.Creatures.Get("ange"), 5);
        var battle = new TacticalBattle(attacker, defender, new GameRandom(1), defenderRebels: rebels);

        BattleAI.PlayOut(battle);

        // le paysan tombe vite : le combat s'arrête là, même si les anges révoltés auraient battu l'ange
        Assert.Equal(BattleOutcome.DefenderWon, battle.Outcome);
        Assert.True(battle.Report().DefenderRebelSurvivors.Count > 0);
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

    [Fact]
    public void Le_joueur_attaque_dirige_sa_defense_puis_l_adversaire_reprend_son_tour()
    {
        var (state, me, him) = FaceToFace(mine: 40, rival: 60);
        state.HumansDefend = true;
        state.EndTurn();

        ComputerPlayer.PlayTurn(state);

        // le combat attend le joueur : son héros est encore là, l'adversaire ne peut pas finir son tour
        Assert.NotNull(state.CurrentBattle);
        Assert.Contains(me, state.Players[0].Heroes);
        Assert.Contains(state.TakeEvents(), e => e is BattleStarted { Defender: HeroDefender d } && d.Hero == me);
        state.EndTurn();
        Assert.True(state.CurrentPlayer.IsComputer);

        // le joueur se défend (ici, l'IA du combat le fait pour lui), puis l'adversaire reprend et finit son tour
        BattleAI.PlayOut(state.CurrentBattle!);
        Assert.True(state.FinishBattle());
        ComputerPlayer.PlayTurn(state);
        Assert.Null(state.CurrentBattle);
        state.EndTurn();
        // c'est de nouveau au joueur, ou la partie est finie (s'il a perdu son seul héros)
        Assert.True(state.Outcome != GameOutcome.Playing || !state.CurrentPlayer.IsComputer);
    }
}
