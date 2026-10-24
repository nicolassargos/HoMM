using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

public class TownTests
{
    private static BuildingType B(string id) => TestData.Data.Buildings.Get(id);

    /// <summary>Une partie avec une ville de test juste à côté du héros (deux cases à droite).</summary>
    private static (GameState State, Town Town) GameWithTownNearby(int garrisonPiquiers = 0, string garrisonId = "piquier")
    {
        var state = new GameState(7, TestData.Data);
        var cell = state.CurrentHero!.Position.Offset(2, 0);
        var town = new Town(cell, "Testville", "chateau", TestData.Data);
        town.AddBuilding(B("salle-village"));
        town.AddBuilding(B("corps-de-garde"));
        if (garrisonPiquiers > 0)
            town.Garrison.Add(TestData.Creatures.Get(garrisonId), garrisonPiquiers);
        state.Map.AddObject(town);
        return (state, town);
    }

    private static void WalkTo(GameState state, Cell target)
    {
        state.SetDestination(target);
        while (state.StepAlongPath()) { }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void La_carte_a_une_ville_neutre_gardee_a_mi_distance(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();
        var town = Assert.Single(map.Objects.OfType<Town>());

        Assert.Null(town.Owner);
        Assert.False(town.Garrison.IsEmpty);
        Assert.True(town.Has("salle-village"));
        Assert.True(town.Has("corps-de-garde"));
        double distance = System.Math.Sqrt(System.Math.Pow(town.Position.X - map.Start.X, 2) + System.Math.Pow(town.Position.Y - map.Start.Y, 2));
        Assert.InRange(distance, 10, 16);
        Assert.NotNull(Pathfinder.FindPath(map, map.Start, town.Position));
        Assert.False(map.IsGuarded(town.Position));
    }

    [Fact]
    public void Vaincre_la_garnison_donne_la_ville()
    {
        var (state, town) = GameWithTownNearby(garrisonPiquiers: 3);
        var player = state.Players[0];
        state.TakeEvents();

        WalkTo(state, town.Position);

        var events = state.TakeEvents();
        var battle = events.OfType<BattleFought>().Single();
        Assert.Same(town, battle.Defender);
        Assert.True(battle.Report.AttackerWon);
        Assert.IsType<TownCaptured>(events[^1]);
        Assert.Same(player, town.Owner);
        Assert.Contains(town, player.Towns);
        Assert.True(town.Garrison.IsEmpty);
    }

    [Fact]
    public void Perdre_contre_la_garnison_laisse_la_ville_neutre()
    {
        var (state, town) = GameWithTownNearby(garrisonPiquiers: 40, garrisonId: "ange");

        WalkTo(state, town.Position);

        Assert.Null(town.Owner);
        Assert.Equal(GameOutcome.Defeat, state.Outcome);
    }

    [Fact]
    public void Entrer_dans_sa_ville()
    {
        var (state, town) = GameWithTownNearby();
        town.SetOwner(state.Players[0]);
        state.TakeEvents();

        WalkTo(state, town.Position);

        var entered = Assert.IsType<TownEntered>(state.TakeEvents()[^1]);
        Assert.Same(town, entered.Town);
        Assert.Equal(town.Position, state.CurrentHero!.Position);
        Assert.Same(state.CurrentHero, state.VisitingHero(town));
    }

    [Fact]
    public void Une_ville_rapporte_de_l_or_chaque_jour()
    {
        var (state, town) = GameWithTownNearby();
        var player = state.Players[0];
        town.SetOwner(player);
        int gold = player.Resources[ResourceType.Gold];

        state.EndTurn();

        Assert.Equal(gold + 500, player.Resources[ResourceType.Gold]);
    }

    [Fact]
    public void Construire_paie_le_batiment_une_seule_fois_par_jour()
    {
        var (state, town) = GameWithTownNearby();
        var player = state.Players[0];
        town.SetOwner(player);
        int gold = player.Resources[ResourceType.Gold], wood = player.Resources[ResourceType.Wood];

        Assert.Equal(BuildResult.Ok, state.Build(town, "taverne"));
        Assert.True(town.Has("taverne"));
        Assert.Equal(gold - 500, player.Resources[ResourceType.Gold]);
        Assert.Equal(wood - 5, player.Resources[ResourceType.Wood]);
        Assert.Equal(new BuildingBuilt(town, B("taverne")), state.TakeEvents().OfType<BuildingBuilt>().Single());

        Assert.Equal(BuildResult.AlreadyBuiltToday, state.Build(town, "tour-archers"));
        // Ce qui manque durablement passe avant « un bâtiment par jour »
        Assert.Equal(BuildResult.MissingRequirement, state.Build(town, "caserne"));
        state.EndTurn();
        Assert.Equal(BuildResult.Ok, state.Build(town, "tour-archers"));
    }

    [Fact]
    public void Construire_demande_les_batiments_requis_et_les_ressources()
    {
        var (state, town) = GameWithTownNearby();
        var player = state.Players[0];

        Assert.Equal(BuildResult.NotOwner, state.Build(town, "taverne"));
        town.SetOwner(player);
        Assert.Equal(BuildResult.AlreadyBuilt, state.Build(town, "corps-de-garde"));
        Assert.Equal(BuildResult.MissingRequirement, state.Build(town, "caserne"));
        player.Resources[ResourceType.Gold] = 100;
        Assert.Equal(BuildResult.NotEnoughResources, state.Build(town, "taverne"));
        Assert.False(town.Has("taverne"));
    }

    [Fact]
    public void L_hotel_de_ville_remplace_la_salle_du_village()
    {
        var (_, town) = GameWithTownNearby();
        Assert.Equal(500, town.DailyGold);
        town.AddBuilding(B("taverne"));
        town.AddBuilding(B("hotel-ville"));
        Assert.Equal(1000, town.DailyGold);
    }

    [Fact]
    public void Les_habitations_produisent_des_creatures_chaque_semaine()
    {
        var (state, town) = GameWithTownNearby();
        town.SetOwner(state.Players[0]);
        var piquier = TestData.Creatures.Get("piquier");
        var archer = TestData.Creatures.Get("archer");
        Assert.Equal(14, town.Available[piquier]); // une semaine offerte à la construction

        Assert.Equal(BuildResult.Ok, state.Build(town, "tour-archers"));
        Assert.Equal(9, town.Available[archer]);

        for (int i = 0; i < 7; i++)
            state.EndTurn();

        Assert.Equal(28, town.Available[piquier]);
        Assert.Equal(18, town.Available[archer]);
    }

    [Fact]
    public void Avec_une_ville_perdre_son_heros_ne_fait_pas_perdre_la_partie()
    {
        var (state, town) = GameWithTownNearby();
        town.SetOwner(state.Players[0]);
        var start = state.CurrentHero!.Position;
        state.Map.AddObject(new MonsterGuard(start.Offset(0, 2), TestData.Creatures.Get("dragon"), 50));

        WalkTo(state, start.Offset(0, 1));

        Assert.Null(state.CurrentHero);
        Assert.Equal(GameOutcome.Playing, state.Outcome);
    }

    [Fact]
    public void Tous_les_batiments_existent_et_leurs_prerequis_aussi()
    {
        foreach (var building in TestData.Data.Buildings.All)
        {
            foreach (var required in building.Requires)
                Assert.NotNull(TestData.Data.Buildings.Get(required));
            if (building.Dwelling is { } creature)
                Assert.NotNull(TestData.Creatures.Get(creature));
        }
        Assert.Equal(7, TestData.Data.Buildings.All.Count(b => b.Dwelling != null));
    }
}
