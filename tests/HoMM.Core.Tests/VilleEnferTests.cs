using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// L'Enfer jouable (étape 2, 2026-11-11) : ses habitations, la classe Démoniste et son talent Pacte, ses quatre héros.
/// </summary>
public class VilleEnferTests
{
    private const string Inferno = "enfer";

    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static GameState InfernoGame(int seed = 5) => new(seed, TestData.Data, MapSize.Small, 0, faction: Inferno);

    [Fact]
    public void Un_joueur_de_l_enfer_commence_avec_un_demoniste()
    {
        var state = InfernoGame();
        var hero = state.Players[0].Heroes.Single();
        var castle = state.Players[0].Towns.Single();

        Assert.Equal(Inferno, castle.Faction);
        Assert.True(castle.Has("puits-diablotins"));
        Assert.Equal("demoniste", hero.Class?.Id);
        Assert.Equal((1, 1, 3, 2, 9), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Equal(new[] { "soif-de-sang", "malediction" }, hero.Spells.Select(s => s.Id));
        Assert.Equal(1, hero.SkillRank("ecole-feu"));
        Assert.Equal(new[] { ("diablotin", 15), ("cultiste", 7), ("limon-magma", 3) }, hero.Army.Stacks.Select(s => (s.Type.Id, s.Count)));
        Assert.Equal(Inferno, TestData.Data.Heroes.All.Single(h => h.Name == hero.Name).Faction);
    }

    [Fact]
    public void L_enfer_a_quatre_heros_et_il_ne_reste_plus_de_heros_sans_faction()
    {
        Assert.Equal(new[] { "mephis-le-cornu", "dame-cendre", "zarkhan", "sorcha-la-damnee" },
            TestData.Data.Heroes.All.Where(h => h.Faction == Inferno).Select(h => h.Id));
        Assert.DoesNotContain(TestData.Data.Heroes.All, h => h.Faction == "sombre");
    }

    [Fact]
    public void La_ville_de_l_enfer_a_ses_sept_habitations()
    {
        var castle = InfernoGame().Players[0].Towns.Single();
        Assert.Equal(new[] { "puits-diablotins", "temple-culte", "coulee-magma", "palais-succubes", "salle-tourments", "fournaise-moloch", "portail-demons" },
            Enumerable.Range(1, 7).Select(t => castle.DwellingOfTier(t)!.Id));
        var b = TestData.Data.Buildings;
        Assert.Equal(2, b.Get("salle-tourments").Cost[ResourceType.Sulfur]);
        Assert.Equal(2, b.Get("fournaise-moloch").Cost[ResourceType.Sulfur]);
        Assert.Equal(5, b.Get("portail-demons").Cost[ResourceType.Mercury]);
        Assert.Contains(b.Get("grand-hotel-ville").Requires, r => r.Split('|').Contains("palais-succubes"));
    }

    [Fact]
    public void Le_pacte_releve_le_prix_du_sang_avec_le_niveau()
    {
        var hero = InfernoGame().Players[0].Heroes.Single();
        hero.Level = 1;
        Assert.Equal(2 * hero.Knowledge + 1, Talents.BloodPriceCap(hero));
        hero.Level = 10;
        Assert.Equal(2 * hero.Knowledge + 10, Talents.BloodPriceCap(hero));

        var paladin = new GameState(5, TestData.Data, MapSize.Small, 0).Players[0].Heroes.Single();
        paladin.Level = 10;
        Assert.Equal(2 * paladin.Knowledge, Talents.BloodPriceCap(paladin));
    }

    [Fact]
    public void Un_adversaire_de_l_enfer_batit_ses_habitations()
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, opponents: 1, opponentFactions: new[] { Inferno });
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
        Assert.Equal("demoniste", rival.Heroes[0].Class?.Id);
        var castle = rival.Towns.First(t => t.Faction == Inferno);
        Assert.True(castle.Has("temple-culte"));
    }
}
