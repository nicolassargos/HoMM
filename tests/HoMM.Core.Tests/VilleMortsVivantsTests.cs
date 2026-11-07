using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Jouer les morts-vivants (étape 9) : leur ville, le Nécromancien et la nécromancie, les factions de la carte.</summary>
public class VilleMortsVivantsTests
{
    private const string Undead = "morts-vivants";

    private static GameState UndeadGame(int seed = 5, int opponents = 0) =>
        new(seed, TestData.Data, MapSize.Small, opponents, faction: Undead);

    [Fact]
    public void Un_joueur_mort_vivant_commence_dans_sa_necropole_avec_un_necromancien()
    {
        var state = UndeadGame();
        var player = state.Players[0];
        var hero = player.Heroes.Single();
        var castle = player.Towns.Single();

        Assert.Equal(Undead, castle.Faction);
        Assert.True(castle.Has("ossuaire"));
        Assert.Equal("necromancien", hero.Class?.Id);
        Assert.Equal((1, 1, 2, 2, 8), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Contains(hero.Spells, s => s.Id == "malediction");
        Assert.Equal(new[] { ("squelette", 20), ("zombie", 10), ("fantome", 2) }, hero.Army.Stacks.Select(s => (s.Type.Id, s.Count)));
    }

    [Fact]
    public void La_ville_des_morts_vivants_a_les_batiments_communs_et_ses_sept_habitations()
    {
        var castle = UndeadGame().Players[0].Towns.Single();
        var ids = castle.Buildings.Select(b => b.Id).ToHashSet();

        Assert.Subset(ids, new[] { "salle-village", "hotel-ville", "taverne", "guilde-mages-1" }.ToHashSet());
        Assert.Equal(new[] { "ossuaire", "cimetiere", "etang-noyees", "antre-araignees", "crypte", "tour-liche", "charnier" },
            Enumerable.Range(1, 7).Select(t => castle.DwellingOfTier(t)!.Id));
        Assert.DoesNotContain("corps-de-garde", ids); // pas les habitations du Château
    }

    [Fact]
    public void Le_grand_hotel_de_ville_demande_une_habitation_du_niveau_4_de_sa_faction()
    {
        var state = UndeadGame();
        var player = state.Players[0];
        foreach (var resource in player.Resources.Keys.ToList())
            player.Resources[resource] = 100_000;
        var castle = player.Towns.Single();
        foreach (var id in new[] { "taverne", "hotel-ville", "cimetiere" })
        {
            castle.BuiltToday = false;
            Assert.Equal(BuildResult.Ok, state.Build(castle, id));
        }
        castle.BuiltToday = false;
        Assert.Equal(BuildResult.MissingRequirement, state.Build(castle, "grand-hotel-ville"));

        castle.BuiltToday = false;
        state.Build(castle, "antre-araignees");
        castle.BuiltToday = false;
        Assert.Equal(BuildResult.Ok, state.Build(castle, "grand-hotel-ville"));
    }

    [Fact]
    public void Apres_une_victoire_la_necromancie_releve_des_squelettes_sans_depasser_le_charisme()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, startingTowns: false, faction: Undead);
        var hero = state.CurrentHero!;
        var start = hero.Position;
        state.Map.AddObject(new MonsterGuard(start.Offset(2, 0), TestData.Creatures.Get("gobelin"), 40));
        int before = hero.Army.Stacks.Single(s => s.Type.Id == "squelette").Count;
        state.SetDestination(start.Offset(1, 0));

        state.StepAlongPath();

        var fought = Assert.Single(state.TakeEvents().OfType<BattleFought>());
        Assert.True(fought.Report.AttackerWon);
        int killed = fought.Report.DefenderLosses.Sum(s => s.Count);
        int expected = (int)(killed * (Talents.NecromancyBase + Talents.NecromancyPerLevel * hero.Level));
        int lost = fought.Report.AttackerLosses.Where(s => s.Type.Id == "squelette").Sum(s => s.Count);
        int limit = Command.Limit(hero.Charisma) / TestData.Creatures.Get("squelette").CommandCost;
        Assert.Equal(System.Math.Min(before - lost + expected, limit), hero.Army.Stacks.Single(s => s.Type.Id == "squelette").Count);
    }

    [Fact]
    public void Un_paladin_ne_releve_personne()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, startingTowns: false);
        var hero = state.CurrentHero!;
        state.Map.AddObject(new MonsterGuard(hero.Position.Offset(2, 0), TestData.Creatures.Get("gobelin"), 20));
        state.SetDestination(hero.Position.Offset(1, 0));
        state.StepAlongPath();
        var events = state.TakeEvents();
        Assert.Contains(events, e => e is BattleFought);
        Assert.Empty(events.OfType<DeadRaised>());
        Assert.DoesNotContain(hero.Army.Stacks, s => s.Type.Id == "squelette");
    }

    [Fact]
    public void Les_adversaires_et_les_villes_neutres_ont_des_factions_au_hasard_mais_fixees_par_la_graine()
    {
        string Factions(int seed)
        {
            var state = new GameState(seed, TestData.Data, MapSize.Large, opponents: 3);
            return string.Join(",", state.Map.Objects.OfType<Town>().OrderBy(t => t.Id).Select(t => t.Faction));
        }
        Assert.Equal(Factions(4), Factions(4));
        var all = Enumerable.Range(1, 6).SelectMany(seed => Factions(seed).Split(',')).ToHashSet();
        Assert.Contains("chateau", all);
        Assert.Contains(Undead, all);
        // une ville neutre a une garnison de sa faction
        var map = new MapGenerator(3, TestData.Data).Generate();
        Assert.All(map.Objects.OfType<Town>().Where(t => t.Position != map.Start),
            t => Assert.All(t.Garrison.Stacks, s => Assert.Equal(t.Faction, s.Type.Faction)));
    }

    [Fact]
    public void Un_adversaire_mort_vivant_batit_ses_habitations()
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, opponents: 1, opponentFactions: new[] { Undead });
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
        Assert.Equal("necromancien", rival.Heroes[0].Class?.Id);
        var castle = rival.Towns.First(t => t.Faction == Undead);
        Assert.True(castle.Has("cimetiere"));
        Assert.True(castle.Has("taverne"));
    }
}
