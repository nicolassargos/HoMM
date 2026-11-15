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
    public void Le_joueur_choisit_son_heros_et_les_adversaires_prennent_le_dernier_de_leur_faction()
    {
        var state = new GameState(5, TestData.Data, 54, 40, opponents: 2, heroName: "Sire Yvain",
            opponentFactions: new[] { Chateau, Undead });

        Assert.Equal("Sire Yvain", state.Players[0].Heroes[0].Name);
        // les adversaires : le dernier héros de leur faction pas encore pris (Sire Yvain, le dernier du Château, est au joueur)
        var chateau = TestData.Data.Heroes.OfFaction(Chateau).Select(h => h.Name).ToList();
        var undead = TestData.Data.Heroes.OfFaction(Undead).Select(h => h.Name).ToList();
        Assert.Equal("Sire Yvain", chateau[^1]);
        Assert.Equal(new[] { chateau[^2], undead[^1] }, state.Opponents.Select(p => p.Heroes[0].Name));
        // leur classe : celle de leur faction, et leur château est de cette faction
        Assert.All(state.Opponents, p => Assert.Equal(TestData.Data.Classes.ForFaction(p.Towns.Single().Faction), p.Heroes[0].Class));
        // un héros inconnu : le premier de sa faction
        Assert.Equal(chateau[0], new GameState(5, TestData.Data, MapSize.Small, heroName: "Personne").Players[0].Heroes[0].Name);
        Assert.Equal(undead[0], new GameState(5, TestData.Data, MapSize.Small, faction: Undead).Players[0].Heroes[0].Name);
    }

    private const string Chateau = "chateau", Undead = "morts-vivants";

    [Fact]
    public void Chaque_heros_a_une_faction_jouable_ou_prevue_et_chaque_faction_jouable_a_des_heros()
    {
        var known = TestData.Data.Creatures.All.Select(c => c.Faction).ToHashSet();
        Assert.All(TestData.Data.Heroes.All, h => Assert.True(
            h.Faction != null && (known.Contains(h.Faction) || DataValidation.PlannedFactions.Contains(h.Faction)), h.Name));
        // assez de héros pour quatre joueurs de la même faction
        Assert.All(TestData.Data.Classes.PlayableFactions, f => Assert.True(TestData.Data.Heroes.OfFaction(f).Count >= 4, f));
    }

    [Fact]
    public void La_taverne_propose_les_heros_de_la_faction_de_la_ville_puis_les_autres()
    {
        var state = new GameState(5, TestData.Data, MapSize.Small, faction: Undead);
        var castle = state.Players[0].Towns.Single();
        var undead = TestData.Data.Heroes.OfFaction(Undead).Select(h => h.Name).ToList();

        // le joueur a le premier ; la taverne propose les deux suivants
        Assert.Equal(undead.Skip(1).Take(2), state.TavernOffers(castle));
        // tous les morts-vivants pris : les autres héros (ceux du Château d'abord, dans l'ordre de la liste)
        var player = state.Players[0];
        player.Resources[ResourceType.Gold] = 100_000;
        player.Resources[ResourceType.Wood] = 50;
        state.Build(castle, "taverne");
        player.Heroes[0].Position = castle.Approach;
        for (int i = 1; i < undead.Count; i++)
        {
            Assert.Equal(HireResult.Ok, state.HireHero(castle));
            state.CurrentHero!.Position = castle.Approach.Offset(i, 1);
        }
        Assert.Equal(undead, player.Heroes.Select(h => h.Name));
        Assert.Equal(TestData.Data.Heroes.FactionFirst(Undead).Skip(undead.Count).Take(2).Select(h => h.Name), state.TavernOffers(castle));
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
        Assert.DoesNotContain(player.Heroes[0].Name, state.TavernOffers(castle));

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
