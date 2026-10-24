using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

public class RecruitTests
{
    private static CreatureType C(string id) => TestData.Creatures.Get(id);

    /// <summary>Une partie avec une ville du joueur, et le héros dedans si <paramref name="heroInside"/>.</summary>
    private static (GameState State, Town Town, Player Player) OwnedTown(bool heroInside)
    {
        var state = new GameState(7, TestData.Data);
        var hero = state.CurrentHero!;
        var cell = heroInside ? hero.Position : hero.Position.Offset(2, 0);
        var town = new Town(cell, "Testville", "chateau", TestData.Data);
        town.AddBuilding(TestData.Data.Buildings.Get("salle-village"));
        town.AddBuilding(TestData.Data.Buildings.Get("corps-de-garde"));
        town.SetOwner(state.Players[0]);
        state.Map.AddObject(town);
        return (state, town, state.Players[0]);
    }

    [Fact]
    public void Recruter_paie_et_remplit_la_garnison()
    {
        var (state, town, player) = OwnedTown(heroInside: false);
        int gold = player.Resources[ResourceType.Gold];

        Assert.Equal(RecruitResult.Ok, state.Recruit(town, C("piquier"), 10));

        Assert.Equal(gold - 600, player.Resources[ResourceType.Gold]);
        Assert.Equal(4, town.Available[C("piquier")]);
        Assert.Equal(10, town.Garrison.Stacks.Single().Count);
        Assert.Equal(new CreaturesRecruited(town, C("piquier"), 10, false),
            state.TakeEvents().OfType<CreaturesRecruited>().Single());
    }

    [Fact]
    public void On_ne_recrute_pas_plus_que_les_disponibles_ni_que_ses_moyens()
    {
        var (state, town, player) = OwnedTown(heroInside: false);

        Assert.Equal(RecruitResult.NotAvailable, state.Recruit(town, C("piquier"), 15));
        Assert.Equal(RecruitResult.NotAvailable, state.Recruit(town, C("archer"), 1));
        player.Resources[ResourceType.Gold] = 100;
        Assert.Equal(1, town.MaxRecruitable(C("piquier"), player));
        Assert.Equal(RecruitResult.NotEnoughResources, state.Recruit(town, C("piquier"), 2));
        Assert.True(town.Garrison.IsEmpty);
    }

    [Fact]
    public void Garnison_pleine_les_recrues_rejoignent_le_heros_present()
    {
        var (state, town, _) = OwnedTown(heroInside: true);
        foreach (var other in TestData.Creatures.All.Where(c => c.Id != "piquier").Take(Army.MaxStacks))
            town.Garrison.Add(other, 1);

        Assert.Equal(RecruitResult.Ok, state.Recruit(town, C("piquier"), 5));

        Assert.Equal(35, state.CurrentHero!.Army.Stacks.Single(s => s.Type == C("piquier")).Count);
        Assert.True(state.TakeEvents().OfType<CreaturesRecruited>().Single().ToHero);
    }

    [Fact]
    public void Sans_place_on_ne_recrute_pas()
    {
        var (state, town, _) = OwnedTown(heroInside: false);
        foreach (var other in TestData.Creatures.All.Where(c => c.Id != "piquier").Take(Army.MaxStacks))
            town.Garrison.Add(other, 1);

        Assert.Equal(RecruitResult.NoRoom, state.Recruit(town, C("piquier"), 5));
    }

    [Fact]
    public void Echanger_des_troupes_entre_garnison_et_heros()
    {
        var (state, town, _) = OwnedTown(heroInside: true);
        var hero = state.CurrentHero!;
        town.Garrison.Add(C("piquier"), 10);
        town.Garrison.Add(C("moine"), 2);

        // Les piquiers de la garnison rejoignent ceux du héros
        Assert.True(state.TransferStack(town, fromGarrison: true, index: 0));
        Assert.Equal(40, hero.Army.Stacks.Single(s => s.Type == C("piquier")).Count);
        Assert.Equal(C("moine"), town.Garrison.Stacks.Single().Type);

        // Et les archers du héros vont dans la garnison
        int archers = hero.Army.Stacks.Single(s => s.Type == C("archer")).Count;
        int index = hero.Army.Stacks.ToList().FindIndex(s => s.Type == C("archer"));
        Assert.True(state.TransferStack(town, fromGarrison: false, index: index));
        Assert.Equal(archers, town.Garrison.Stacks.Single(s => s.Type == C("archer")).Count);
        Assert.DoesNotContain(hero.Army.Stacks, s => s.Type == C("archer"));
    }

    [Fact]
    public void Le_heros_garde_toujours_une_troupe()
    {
        var (state, town, _) = OwnedTown(heroInside: true);
        var hero = state.CurrentHero!;

        Assert.True(state.TransferStack(town, fromGarrison: false, index: 0));
        Assert.True(state.TransferStack(town, fromGarrison: false, index: 0));
        Assert.False(state.TransferStack(town, fromGarrison: false, index: 0));
        Assert.Single(hero.Army.Stacks);
    }

    [Fact]
    public void Pas_d_echange_sans_heros_dans_la_ville()
    {
        var (state, town, _) = OwnedTown(heroInside: false);
        town.Garrison.Add(C("piquier"), 10);

        Assert.False(state.TransferStack(town, fromGarrison: true, index: 0));
    }
}
