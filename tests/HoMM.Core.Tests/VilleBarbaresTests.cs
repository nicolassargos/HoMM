using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Jouer les Barbares (étape 9) : leur ville, la classe Barbare, leurs héros et l'adversaire barbare.</summary>
public class VilleBarbaresTests
{
    private const string Barbarians = "barbares";

    private static GameState BarbarianGame(int seed = 5, int opponents = 0) =>
        new(seed, TestData.Data, MapSize.Small, opponents, faction: Barbarians);

    [Fact]
    public void Un_joueur_barbare_commence_dans_sa_ville_avec_un_barbare_et_sa_horde()
    {
        var state = BarbarianGame();
        var player = state.Players[0];
        var hero = player.Heroes.Single();
        var castle = player.Towns.Single();

        Assert.Equal(Barbarians, castle.Faction);
        Assert.True(castle.Has("hutte-gobelins"));
        Assert.Equal("barbare", hero.Class?.Id);
        Assert.Equal((3, 2, 0, 1, 10), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Empty(hero.Spells);
        Assert.Equal(new[] { ("gobelin", 30), ("loup", 10), ("orc", 4) }, hero.Army.Stacks.Select(s => (s.Type.Id, s.Count)));
        Assert.Equal(Barbarians, TestData.Data.Heroes.All.Single(h => h.Name == hero.Name).Faction);
    }

    [Fact]
    public void La_ville_barbare_a_les_batiments_communs_et_ses_sept_habitations()
    {
        var castle = BarbarianGame().Players[0].Towns.Single();
        var ids = castle.Buildings.Select(b => b.Id).ToHashSet();

        Assert.Subset(ids, new[] { "salle-village", "hotel-ville", "taverne", "guilde-mages-4", "marche" }.ToHashSet());
        Assert.Equal(new[] { "hutte-gobelins", "taniere-loups", "camp-orcs", "fort-ogres", "campement-nomade", "nid-tonnerre", "repaire-behemoth" },
            Enumerable.Range(1, 7).Select(t => castle.DwellingOfTier(t)!.Id));
        Assert.DoesNotContain("corps-de-garde", ids);
        Assert.DoesNotContain("ossuaire", ids);
    }

    [Fact]
    public void Les_habitations_barbares_demandent_du_cristal_puis_du_soufre()
    {
        var b = TestData.Data.Buildings;
        Assert.Equal(2, b.Get("campement-nomade").Cost[ResourceType.Crystal]);
        Assert.Equal(2, b.Get("nid-tonnerre").Cost[ResourceType.Crystal]);
        Assert.Equal(5, b.Get("repaire-behemoth").Cost[ResourceType.Sulfur]);
        var barbarian = TestData.Data.Classes.ForFaction(Barbarians)!;
        Assert.Equal((ResourceType.Sulfur, ResourceType.Crystal), (barbarian.MainResource, barbarian.SecondaryResource));
    }

    [Fact]
    public void Le_grand_hotel_de_ville_barbare_demande_le_fort_des_ogres()
    {
        var state = BarbarianGame();
        var player = state.Players[0];
        foreach (var resource in player.Resources.Keys.ToList())
            player.Resources[resource] = 100_000;
        var castle = player.Towns.Single();
        foreach (var id in new[] { "taverne", "hotel-ville", "taniere-loups" })
        {
            castle.BuiltToday = false;
            Assert.Equal(BuildResult.Ok, state.Build(castle, id));
        }
        castle.BuiltToday = false;
        Assert.Equal(BuildResult.MissingRequirement, state.Build(castle, "grand-hotel-ville"));

        castle.BuiltToday = false;
        state.Build(castle, "fort-ogres");
        castle.BuiltToday = false;
        Assert.Equal(BuildResult.Ok, state.Build(castle, "grand-hotel-ville"));
    }

    [Fact]
    public void Les_barbares_sont_une_faction_jouable_et_ont_des_villes_neutres()
    {
        Assert.Contains(Barbarians, TestData.Data.Classes.PlayableFactions);
        var all = Enumerable.Range(1, 6)
            .SelectMany(seed => new GameState(seed, TestData.Data, 72, 53, opponents: 3).Map.Objects.OfType<Town>())
            .Select(t => t.Faction).ToHashSet();
        Assert.Contains(Barbarians, all);
    }

    [Fact]
    public void Un_adversaire_barbare_batit_ses_habitations()
    {
        var state = new GameState(7, TestData.Data, 54, 40, opponents: 1, opponentFactions: new[] { Barbarians });
        state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
        state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50);
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
        Assert.Equal("barbare", rival.Heroes[0].Class?.Id);
        var castle = rival.Towns.First(t => t.Faction == Barbarians);
        Assert.True(castle.Has("taniere-loups"));
        Assert.True(castle.Has("taverne"));
    }
}
