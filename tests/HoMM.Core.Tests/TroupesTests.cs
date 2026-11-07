using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Glisser les troupes (étape 7) : déplacer, fusionner, échanger, partager ; et la taverne à deux héros.</summary>
public class TroupesTests
{
    private static CreatureType C(string id) => TestData.Creatures.Get(id);

    private static Army ArmyOf(params (string Id, int Count)[] stacks)
    {
        var army = new Army();
        foreach (var (id, count) in stacks)
            army.AddStack(C(id), count);
        return army;
    }

    private static string Describe(Army army) => string.Join(",", army.Stacks.Select(s => $"{s.Type.Id}:{s.Count}"));

    [Fact]
    public void Partager_une_troupe_dans_la_meme_armee()
    {
        var army = ArmyOf(("paysan", 30), ("archer", 15));

        Assert.True(army.MoveTroop(0, army, 5, 10)); // vers un emplacement vide

        Assert.Equal("paysan:20,archer:15,paysan:10", Describe(army));
    }

    [Fact]
    public void Fusionner_deux_troupes_de_la_meme_creature()
    {
        var army = ArmyOf(("paysan", 20), ("archer", 15), ("paysan", 10));

        Assert.True(army.MoveTroop(2, army, 0));

        Assert.Equal("paysan:30,archer:15", Describe(army));
    }

    [Fact]
    public void Echanger_deux_troupes_change_leur_ordre()
    {
        var army = ArmyOf(("paysan", 30), ("archer", 15), ("griffon", 4));

        Assert.True(army.MoveTroop(0, army, 2));

        Assert.Equal("griffon:4,archer:15,paysan:30", Describe(army));
    }

    [Fact]
    public void Entre_deux_armees_deplacer_fusionner_echanger()
    {
        var garrison = ArmyOf(("paysan", 16), ("archer", 10));
        var hero = ArmyOf(("paysan", 30), ("griffon", 4));

        Assert.True(garrison.MoveTroop(0, hero, 0)); // même créature : fusion
        Assert.Equal("archer:10", Describe(garrison));
        Assert.Equal("paysan:46,griffon:4", Describe(hero));

        Assert.True(garrison.MoveTroop(0, hero, 1)); // autre créature : échange
        Assert.Equal("griffon:4", Describe(garrison));
        Assert.Equal("paysan:46,archer:10", Describe(hero));

        Assert.True(hero.MoveTroop(0, garrison, 6, 6)); // partager vers un emplacement vide
        Assert.Equal("griffon:4,paysan:6", Describe(garrison));
        Assert.Equal("paysan:40,archer:10", Describe(hero));
    }

    [Fact]
    public void On_ne_partage_pas_vers_une_autre_creature_ni_dans_une_armee_pleine()
    {
        var full = ArmyOf(("paysan", 7), ("archer", 7), ("griffon", 7), ("epeiste", 7), ("moine", 7), ("cavalier", 7), ("ange", 7));
        var other = ArmyOf(("paysan", 10), ("archer", 10));

        Assert.False(other.MoveTroop(0, other, 1, 5)); // une partie des paysans sur les archers
        Assert.False(full.MoveTroop(0, full, 6, 3)); // plus de place pour une nouvelle troupe
        Assert.False(other.MoveTroop(0, full, 6, 5)); // l'autre armée est pleine (et ce ne sont pas les mêmes créatures)
        Assert.Equal("paysan:10,archer:10", Describe(other));
    }

    /// <summary>Une ville du joueur avec une taverne, et le héros dans la ville.</summary>
    private static (GameState State, Town Town, Hero Hero) HeroInTown()
    {
        var state = new GameState(7, TestData.Data);
        var hero = state.CurrentHero!;
        var town = new Town(hero.Position.Offset(2, 0), "Testville", "chateau", TestData.Data);
        town.AddBuilding(TestData.Data.Buildings.Get("salle-village"));
        town.AddBuilding(TestData.Data.Buildings.Get("taverne"));
        town.SetOwner(state.Players[0]);
        state.Map.AddObject(town);
        hero.Position = town.Position;
        return (state, town, hero);
    }

    [Fact]
    public void Le_heros_garde_toujours_au_moins_une_troupe()
    {
        var (state, town, hero) = HeroInTown();
        while (hero.Army.Stacks.Count > 1)
            Assert.True(state.MoveTroop(town, fromGarrison: false, 0, toGarrison: true, 6));

        Assert.False(state.MoveTroop(town, fromGarrison: false, 0, toGarrison: true, 6));
        Assert.Single(hero.Army.Stacks);
        // partager reste permis : il en garde une partie
        int count = hero.Army.Stacks[0].Count;
        Assert.True(state.MoveTroop(town, fromGarrison: false, 0, toGarrison: true, 6, 1));
        Assert.Equal(count - 1, hero.Army.Stacks[0].Count);
    }

    [Fact]
    public void Glisser_une_troupe_previent_l_affichage()
    {
        var (state, _, hero) = HeroInTown();
        state.TakeEvents();

        Assert.True(state.MoveTroop(hero, 0, 6, 5));

        Assert.Contains(state.TakeEvents(), e => e is TroopMoved moved && moved.Hero == hero);
    }

    [Fact]
    public void Deux_troupes_de_la_meme_creature_survivent_a_la_sauvegarde()
    {
        var (state, _, hero) = HeroInTown();
        int before = hero.Army.Stacks.Count;
        Assert.True(state.MoveTroop(hero, 0, 6, 5));

        var loaded = GameState.Load(state.Save(), TestData.Data);

        Assert.Equal(before + 1, loaded.HeroById(hero.Id)!.Army.Stacks.Count);
    }

    [Fact]
    public void La_taverne_propose_deux_heros_et_on_choisit()
    {
        var (state, town, hero) = HeroInTown();
        hero.Position = town.Position.Offset(0, 1); // la place devant la ville doit être libre pour le nouveau héros
        var offers = state.TavernOffers(town);
        Assert.Equal(2, offers.Count);
        Assert.Equal(HireResult.NoHeroLeft, state.HireHero(town, "Personne")); // pas un héros proposé

        Assert.Equal(HireResult.Ok, state.HireHero(town, offers[1]));

        Assert.Equal(offers[1], state.CurrentHero!.Name);
        Assert.DoesNotContain(offers[1], state.TavernOffers(town));
    }
}
