using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Les héros (un nom) et leurs classes (le reste), étape 9.</summary>
public class ClassesTests
{
    private static HeroClass Paladin => TestData.Data.Classes.Get("paladin")!;

    [Fact]
    public void Le_heros_de_depart_est_un_paladin_le_plus_charismatique()
    {
        var hero = new GameState(5, TestData.Data, MapSize.Small).Players[0].Heroes[0];

        Assert.Equal("paladin", hero.Class?.Id);
        Assert.Equal((2, 2, 1, 2, 14), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
        Assert.Contains(hero.Spells, s => s.Id == "soins");
        Assert.Equal(20, hero.SpellPoints);
        // le Paladin a le plus de charisme de toutes les classes
        Assert.All(TestData.Data.Classes.All, c => Assert.True(c.Charisma <= Paladin.Charisma));
    }

    [Fact]
    public void Le_joueur_choisit_son_heros_et_les_adversaires_prennent_la_fin_de_la_liste()
    {
        var state = new GameState(5, TestData.Data, MapSize.Normal, opponents: 2, heroName: "Sire Yvain");

        Assert.Equal("Sire Yvain", state.Players[0].Heroes[0].Name);
        var names = TestData.Data.Heroes.All.Select(h => h.Name).ToList();
        // les adversaires : les derniers de la liste, sans celui du joueur
        Assert.Equal(new[] { names[^2], names[^3] }, state.Opponents.Select(p => p.Heroes[0].Name));
        // leur classe : celle de leur faction (tirée au sort), et leur château est de cette faction
        Assert.All(state.Opponents, p => Assert.Equal(TestData.Data.Classes.ForFaction(p.Towns.Single().Faction), p.Heroes[0].Class));
        // un héros inconnu : le premier de la liste
        Assert.Equal(names[0], new GameState(5, TestData.Data, MapSize.Small, heroName: "Personne").Players[0].Heroes[0].Name);
    }

    [Fact]
    public void Un_heros_de_taverne_prend_la_classe_de_la_ville_et_la_taverne_ne_propose_pas_les_heros_deja_la()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small);
        var player = state.Players[0];
        player.Resources[ResourceType.Gold] = 10_000;
        player.Resources[ResourceType.Wood] = 50;
        var castle = player.Towns.Single();
        state.Build(castle, "taverne");
        player.Heroes[0].Position = castle.Approach;
        Assert.DoesNotContain(player.Heroes[0].Name, state.TavernOffers());

        Assert.Equal(HireResult.Ok, state.HireHero(castle));

        var hired = state.CurrentHero!;
        Assert.Equal("paladin", hired.Class?.Id);
        Assert.Equal(Paladin.Charisma, hired.Charisma);
    }

    [Fact]
    public void La_classe_est_sauvegardee()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small);

        var loaded = GameState.Load(state.Save(), TestData.Data).Players[0].Heroes[0];

        Assert.Equal("paladin", loaded.Class?.Id);
        Assert.Equal(14, loaded.Charisma);
    }

    [Fact]
    public void Une_classe_dont_la_faction_ou_un_sort_n_existe_pas_est_refusee()
    {
        var errors = new List<string>();
        DataValidation.CheckHeroes(new[] { new HeroType { Name = "A" }, new HeroType { Name = "A" } },
            new[] { Paladin with { Faction = "elfes", Spells = new List<string> { "inconnu" } } },
            TestData.Creatures.All, TestData.Spells.All, errors);

        Assert.Contains(errors, e => e.Contains("héros en double"));
        Assert.Contains(errors, e => e.Contains("faction inconnue"));
        Assert.Contains(errors, e => e.Contains("sort inconnu"));
    }
}
