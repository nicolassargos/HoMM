using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les Nains jouables (étape 2, 2026-11-12) : leurs habitations, la classe Thane et son talent Obstination, leurs six
/// héros.
/// </summary>
public class VilleNainsTests
{
    private const string Dwarves = "nains";

    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static GameState DwarfGame(int seed = 5) => new(seed, TestData.Data, MapSize.Small, 0, faction: Dwarves);

    [Fact]
    public void Un_joueur_nain_commence_avec_un_thane()
    {
        var state = DwarfGame();
        var hero = state.Players[0].Heroes.Single();
        var castle = state.Players[0].Towns.Single();

        Assert.Equal(Dwarves, castle.Faction);
        Assert.True(castle.Has("salle-boucliers"));
        Assert.Equal("thane", hero.Class?.Id);
        Assert.Equal((1, 3, 1, 2, 11), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Equal(new[] { "peau-de-pierre" }, hero.Spells.Select(s => s.Id));
        Assert.Equal(1, hero.SkillRank("ecole-terre"));
        Assert.Equal(new[] { ("porte-bouclier", 14), ("arbaletrier", 5), ("chevaucheur-chevre", 2) }, hero.Army.Stacks.Select(s => (s.Type.Id, s.Count)));
        Assert.Equal(Dwarves, TestData.Data.Heroes.All.Single(h => h.Name == hero.Name).Faction);
    }

    [Fact]
    public void Les_nains_ont_six_heros()
    {
        Assert.Equal(new[] { "brokk-marteau-noir", "hulda-pierre-lune", "gunnar-fend-la-roche", "ingrid-vapeur-d-airain", "bardur-le-tetu", "kalla-oeil-de-braise" },
            TestData.Data.Heroes.All.Where(h => h.Faction == Dwarves).Select(h => h.Id));
    }

    [Fact]
    public void La_ville_naine_a_ses_sept_habitations()
    {
        var castle = DwarfGame().Players[0].Towns.Single();
        Assert.Equal(new[] { "salle-boucliers", "atelier-arbaletriers", "enclos-chevres", "salle-ancetres", "forge-flammes", "hangar-gyrocopteres", "grande-fonderie" },
            Enumerable.Range(1, 7).Select(t => castle.DwellingOfTier(t)!.Id));
        var b = TestData.Data.Buildings;
        Assert.Equal(2, b.Get("forge-flammes").Cost[ResourceType.Sulfur]);
        Assert.Equal(2, b.Get("hangar-gyrocopteres").Cost[ResourceType.Sulfur]);
        Assert.Equal(5, b.Get("grande-fonderie").Cost[ResourceType.Crystal]);
        Assert.Contains(b.Get("grand-hotel-ville").Requires, r => r.Split('|').Contains("salle-ancetres"));
    }

    [Fact]
    public void L_obstination_du_thane_grandit_avec_le_niveau_jusqu_a_la_moitie_des_degats()
    {
        var hero = DwarfGame().Players[0].Heroes.Single();
        hero.Level = 10;
        Assert.Equal(0.3, Talents.StubbornShare(hero, TacticalBattle.StubbornShare), 6);
        hero.Level = 60;
        Assert.Equal(Talents.StubbornMax, Talents.StubbornShare(hero, TacticalBattle.StubbornShare), 6);

        var paladin = new GameState(5, TestData.Data, MapSize.Small, 0).Players[0].Heroes.Single();
        paladin.Level = 10;
        Assert.Equal(TacticalBattle.StubbornShare, Talents.StubbornShare(paladin, TacticalBattle.StubbornShare), 6);
        Assert.Equal(TacticalBattle.StubbornShare, Talents.StubbornShare(null, TacticalBattle.StubbornShare), 6);
    }

    [Fact]
    public void Un_adversaire_nain_batit_ses_habitations()
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, opponents: 1, opponentFactions: new[] { Dwarves });
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
        state.Players[0].Heroes[0].Army.Add(Creature("ange"), 50);
        for (int day = 0; day < 14; day++)
        {
            state.EndTurn();
            while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
            {
                ComputerPlayer.PlayTurn(state);
                state.EndTurn();
            }
        }
        var rival = state.Players[1];
        Assert.Equal("thane", rival.Heroes[0].Class?.Id);
        var castle = rival.Towns.First(t => t.Faction == Dwarves);
        Assert.True(castle.Has("atelier-arbaletriers"));
    }
}
