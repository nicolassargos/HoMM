using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// La Tour de magie jouable (étape 2, décidée avec l'utilisateur, 2026-11-09) : ses habitations, la bibliothèque des
/// arcanes (un sort de plus par niveau de la guilde), la classe Magicien et son talent Érudit.
/// </summary>
public class VilleTourTests
{
    private const string Tower = "tour";

    private static CreatureType Creature(string id) => TestData.Creatures.Get(id);

    private static GameState TowerGame(int seed = 5) => new(seed, TestData.Data, MapSize.Small, 0, faction: Tower);

    [Fact]
    public void Un_joueur_de_la_tour_commence_avec_un_magicien()
    {
        var state = TowerGame();
        var hero = state.Players[0].Heroes.Single();
        var castle = state.Players[0].Towns.Single();

        Assert.Equal(Tower, castle.Faction);
        Assert.True(castle.Has("atelier-gnomes"));
        Assert.Equal("magicien", hero.Class?.Id);
        Assert.Equal((0, 1, 3, 3, 8), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Equal(new[] { "fleche-magique", "benediction" }, hero.Spells.Select(s => s.Id));
        Assert.Equal(1, hero.SkillRank(Skills.Mysticism));
        Assert.Equal(new[] { ("gnome", 15), ("gargouille", 6), ("golem-pierre", 2) }, hero.Army.Stacks.Select(s => (s.Type.Id, s.Count)));
        Assert.Equal(Tower, TestData.Data.Heroes.All.Single(h => h.Name == hero.Name).Faction);
    }

    [Fact]
    public void La_ville_de_la_tour_a_ses_sept_habitations()
    {
        var castle = TowerGame().Players[0].Towns.Single();
        Assert.Equal(new[] { "atelier-gnomes", "perchoir-gargouilles", "carriere-golems", "bassin-nagas", "lampe-djinns", "academie-mages", "sommet-geants" },
            Enumerable.Range(1, 7).Select(t => castle.DwellingOfTier(t)!.Id));
        var b = TestData.Data.Buildings;
        Assert.Equal(2, b.Get("lampe-djinns").Cost[ResourceType.Mercury]);
        Assert.Equal(2, b.Get("academie-mages").Cost[ResourceType.Mercury]);
        Assert.Equal(5, b.Get("sommet-geants").Cost[ResourceType.Gems]);
        Assert.Contains(b.Get("grand-hotel-ville").Requires, r => r.Split('|').Contains("bassin-nagas"));
    }

    [Fact]
    public void La_bibliotheque_des_arcanes_offre_un_sort_de_plus_par_niveau()
    {
        var state = TowerGame();
        var tower = state.Players[0].Towns.Single();
        Assert.Equal(new[] { 4, 4, 3, 3 }, Enumerable.Range(1, 4).Select(tower.GuildSpellsAt));
        var other = new GameState(5, TestData.Data, MapSize.Small, 0).Players[0].Towns.Single();
        Assert.Equal(new[] { 3, 3, 2, 2 }, Enumerable.Range(1, 4).Select(other.GuildSpellsAt));

        // tirés pour de bon : 4 sorts du niveau 1 (sur 5), 3 du niveau 4 (sur 3 : tous)
        tower.DrawGuildSpells(1, TestData.Data.Spells, n => 0);
        tower.DrawGuildSpells(4, TestData.Data.Spells, n => 0);
        Assert.Equal(4, tower.GuildSpells.Count(s => s.Level == 1));
        Assert.Equal(TestData.Data.Spells.OfLevel(4).Count(), tower.GuildSpells.Count(s => s.Level == 4));
    }

    [Fact]
    public void L_erudit_rend_ses_sorts_plus_forts_selon_son_niveau()
    {
        var hero = TowerGame().Players[0].Heroes.Single();
        var spell = TestData.Data.Spells.Get("fleche-magique");
        hero.Level = 10;
        Assert.Equal(20, hero.SpellBonus(spell));

        var paladin = new GameState(5, TestData.Data, MapSize.Small, 0).Players[0].Heroes.Single();
        paladin.Level = 10;
        Assert.Equal(0, paladin.SpellBonus(spell));
    }

    [Fact]
    public void Un_adversaire_de_la_tour_batit_ses_habitations()
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, opponents: 1, opponentFactions: new[] { Tower });
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
        Assert.Equal("magicien", rival.Heroes[0].Class?.Id);
        var castle = rival.Towns.First(t => t.Faction == Tower);
        Assert.True(castle.Has("perchoir-gargouilles"));
    }
}
