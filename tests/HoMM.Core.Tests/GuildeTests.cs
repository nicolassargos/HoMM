using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La guilde des mages (étape 9) : ses niveaux, ses sorts tirés au sort, ce qu'y apprend un héros.</summary>
public class GuildeTests
{
    /// <summary>Une partie où le joueur a de quoi tout construire ; son héros est dans son château.</summary>
    private static (GameState State, Town Castle, Hero Hero) Game(int seed = 3)
    {
        var state = new GameState(seed, TestData.Data, MapSize.Small);
        var player = state.Players[0];
        foreach (var resource in player.Resources.Keys.ToList())
            player.Resources[resource] = 100_000;
        var castle = player.Towns.Single();
        var hero = player.Heroes.Single();
        state.TakeEvents();
        return (state, castle, hero);
    }

    /// <summary>Construit, un jour après l'autre (un bâtiment par jour).</summary>
    private static void Build(GameState state, Town town, params string[] ids)
    {
        foreach (var id in ids)
        {
            town.BuiltToday = false;
            Assert.Equal(BuildResult.Ok, state.Build(town, id));
        }
    }

    [Fact]
    public void Construire_la_guilde_tire_ses_sorts_et_les_enseigne_au_heros_present()
    {
        var (state, castle, hero) = Game();
        hero.SpellPoints = 2;

        Build(state, castle, "guilde-mages-1");

        Assert.Equal(1, castle.MageGuildLevel);
        Assert.Equal(3, castle.GuildSpells.Count);
        Assert.All(castle.GuildSpells, s => Assert.Equal(1, s.Level));
        Assert.Equal(3, castle.GuildSpells.Distinct().Count());
        Assert.All(castle.GuildSpells, s => Assert.True(hero.Knows(s)));
        Assert.Equal(hero.MaxSpellPoints, hero.SpellPoints);
        var learned = Assert.Single(state.TakeEvents().OfType<SpellsLearned>());
        Assert.Equal(3, learned.Spells.Count);
    }

    [Fact]
    public void Chaque_niveau_ajoute_ses_sorts_et_demande_ses_batiments()
    {
        var (state, castle, _) = Game();
        Build(state, castle, "guilde-mages-1", "guilde-mages-2");
        castle.BuiltToday = false;
        Assert.Equal(BuildResult.MissingRequirement, state.Build(castle, "guilde-mages-3")); // il faut l'hôtel de ville

        Build(state, castle, "taverne", "hotel-ville", "guilde-mages-3", "tour-archers", "caserne", "grand-hotel-ville", "guilde-mages-4");

        Assert.Equal(4, castle.MageGuildLevel);
        Assert.Equal(new[] { 3, 3, 2, 2 }, Enumerable.Range(1, 4).Select(l => castle.GuildSpells.Count(s => s.Level == l)));
        Assert.Equal(10, castle.GuildSpells.Distinct().Count());
    }

    [Fact]
    public void Les_sorts_tires_changent_d_une_ville_a_l_autre()
    {
        var draws = Enumerable.Range(1, 12).Select(seed =>
        {
            var (state, castle, _) = Game(seed);
            Build(state, castle, "guilde-mages-1");
            return string.Join(",", castle.GuildSpells.Select(s => s.Id));
        }).Distinct().Count();
        Assert.True(draws > 1);
    }

    [Fact]
    public void Un_heros_qui_entre_dans_la_ville_apprend_les_sorts_et_retrouve_ses_points()
    {
        var (state, castle, hero) = Game();
        hero.Position = castle.Approach; // dehors : il n'apprend rien quand la guilde est construite
        Build(state, castle, "guilde-mages-1");
        Assert.All(castle.GuildSpells, s => Assert.False(hero.Knows(s)));
        hero.SpellPoints = 0;

        state.SetDestination(castle.Position);
        state.StepAlongPath();

        Assert.Equal(castle.Position, hero.Position);
        Assert.All(castle.GuildSpells, s => Assert.True(hero.Knows(s)));
        Assert.Equal(hero.MaxSpellPoints, hero.SpellPoints);
    }

    [Fact]
    public void Une_journee_commencee_dans_la_ville_recharge_tous_les_points()
    {
        var (state, castle, hero) = Game();
        Build(state, castle, "guilde-mages-1");
        hero.Knowledge = 3;
        hero.SpellPoints = 5;

        int day = state.Day;
        state.EndTurn();

        Assert.Equal(day + 1, state.Day);
        Assert.Equal(30, hero.SpellPoints);
    }

    [Fact]
    public void Sans_guilde_un_jour_ne_rend_qu_un_point()
    {
        var (state, _, hero) = Game();
        hero.SpellPoints = 5;
        state.EndTurn();
        Assert.Equal(6, hero.SpellPoints);
    }

    [Fact]
    public void Un_heros_recrute_a_la_taverne_apprend_les_sorts()
    {
        var (state, castle, hero) = Game();
        Build(state, castle, "guilde-mages-1", "taverne");
        hero.Position = castle.Approach; // l'entrée est libre

        Assert.Equal(HireResult.Ok, state.HireHero(castle));

        Assert.All(castle.GuildSpells, s => Assert.True(state.CurrentHero!.Knows(s)));
    }

    [Fact]
    public void Les_sorts_de_la_guilde_sont_sauvegardes()
    {
        var (state, castle, _) = Game();
        Build(state, castle, "guilde-mages-1");

        var loaded = GameState.Load(state.Save(), TestData.Data);

        var town = loaded.Players[0].Towns.Single();
        Assert.Equal(castle.GuildSpells.Select(s => s.Id), town.GuildSpells.Select(s => s.Id));
        Assert.Equal(1, town.MageGuildLevel);
    }

    [Fact]
    public void Un_adversaire_construit_la_guilde_et_ses_heros_apprennent_des_sorts()
    {
        int withSpells = 0, withGuild = 0;
        foreach (int seed in new[] { 3, 7, 11 })
        {
            var state = new GameState(seed, TestData.Data, MapSize.Normal, opponents: 1);
            state.ChooseRules(new GameRules(VictoryCondition.DefeatOpponents));
            state.Players[0].Heroes[0].Army.Add(TestData.Creatures.Get("ange"), 50); // le joueur ne se fait pas prendre
            for (int day = 0; day < 28 && state.Outcome == GameOutcome.Playing; day++)
            {
                state.EndTurn();
                while (state.CurrentPlayer.IsComputer && state.Outcome == GameOutcome.Playing)
                {
                    ComputerPlayer.PlayTurn(state);
                    state.EndTurn();
                }
            }
            var rival = state.Players[1];
            if (rival.Towns.Any(t => t.MageGuildLevel > 0))
                withGuild++; // pas toujours en 4 semaines : il peut garder son or pour une habitation (une crypte…)
            if (rival.Heroes.Any(h => h.Spells.Count > 0))
                withSpells++;
        }
        Assert.True(withGuild >= 2);
        Assert.True(withSpells >= 2);
    }
}
