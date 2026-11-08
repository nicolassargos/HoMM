using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

public class TownTests
{
    private static BuildingType B(string id) => TestData.Data.Buildings.Get(id);

    /// <summary>Une partie avec une ville de test juste à côté du héros (deux cases à droite).</summary>
    private static (GameState State, Town Town) GameWithTownNearby(int garrisonPaysans = 0, string garrisonId = "paysan")
    {
        var state = new GameState(7, TestData.Data, MapSize.Normal, startingTowns: false);
        var cell = state.CurrentHero!.Position.Offset(2, 0);
        var town = new Town(cell, "Testville", "chateau", TestData.Data);
        town.AddBuilding(B("salle-village"));
        town.AddBuilding(B("corps-de-garde"));
        if (garrisonPaysans > 0)
            town.Garrison.Add(TestData.Creatures.Get(garrisonId), garrisonPaysans);
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
    public void La_carte_a_des_villes_neutres_mieux_gardees_au_loin(int seed)
    {
        // les villes neutres (le château de départ n'en est pas une)
        var map = new MapGenerator(seed, TestData.Data).Generate();
        var towns = map.Objects.OfType<Town>().Where(t => t.Position != map.Start)
            .OrderBy(t => Pathfinder.FindPath(map, map.Start, t.Position)!.Count)
            .ToList();

        Assert.True(towns.Count >= 2);
        Assert.Equal(towns.Count, towns.Select(t => t.Name).Distinct().Count());
        foreach (var town in towns)
        {
            Assert.Null(town.Owner);
            Assert.False(town.Garrison.IsEmpty);
            Assert.True(town.Has("salle-village"));
            Assert.True(town.Has(town.DwellingOfTier(1)!.Id)); // l'habitation du niveau 1 de sa faction
            Assert.False(map.IsGuarded(town.Position));
        }
        // La ville la plus proche a la plus petite garnison
        // la force : combien de paysans valent ses troupes (les factions n'ont pas les mêmes points de vie)
        static int Strength(Town t) => t.Garrison.Stacks.Sum(s => s.Count * CreatureStrength.PeasantsToBeat(s.Type, TestData.Creatures));
        Assert.All(towns.Skip(1), t => Assert.True(Strength(towns[0]) <= Strength(t)));
    }

    [Fact]
    public void La_ville_occupe_5x4_cases_et_ne_s_ouvre_que_par_son_entree()
    {
        var (state, town) = GameWithTownNearby();
        var map = state.Map;

        // 5 cases de large, 4 de haut, l'entrée au milieu de la rangée du bas
        Assert.True(town.Large);
        Assert.Equal(20, town.Footprint.Count());
        Assert.Equal(town.Position.X - 2, town.Footprint.Min(c => c.X));
        Assert.Equal(town.Position.X + 2, town.Footprint.Max(c => c.X));
        Assert.Equal(town.Position.Y - 3, town.Footprint.Min(c => c.Y));
        Assert.Equal(town.Position.Y, town.Footprint.Max(c => c.Y));
        foreach (var cell in town.Footprint)
        {
            Assert.Same(town, map.OccupantAt(cell));
            Assert.Equal(cell == town.Position, map.IsPassable(cell));
        }

        // Le chemin finit par la case d'approche (juste sous l'entrée), puis l'entrée
        var path = Pathfinder.FindPath(map, state.CurrentHero!.Position, town.Position)!;
        Assert.Equal(town.Approach, path[^2]);
        Assert.Equal(town.Position, path[^1]);
    }

    [Fact]
    public void Les_villes_d_une_ancienne_sauvegarde_gardent_3x3_cases()
    {
        var (state, _) = GameWithTownNearby();
        // une sauvegarde d'avant (version 3) : ses villes gardent 3 × 3 cases
        var json = state.Save().Replace($"\"version\":{GameState.SaveVersion}", "\"version\":3");
        Assert.Contains("\"version\":3", json);
        var towns = GameState.Load(json, TestData.Data).Map.Objects.OfType<Town>().ToList();
        Assert.NotEmpty(towns);
        Assert.All(towns, t => Assert.False(t.Large));
        Assert.All(towns, t => Assert.Equal(9, t.Footprint.Count()));
        // sauvegardées de nouveau (version d'aujourd'hui), elles gardent toujours 3 × 3 cases
        var again = GameState.Load(json, TestData.Data);
        Assert.All(GameState.Load(again.Save(), TestData.Data).Map.Objects.OfType<Town>(), t => Assert.False(t.Large));
        // la sauvegarde d'aujourd'hui les garde sur 5 × 4
        Assert.All(GameState.Load(state.Save(), TestData.Data).Map.Objects.OfType<Town>(), t => Assert.True(t.Large));
    }

    [Fact]
    public void Un_clic_sur_les_murs_mene_a_l_entree()
    {
        var (state, town) = GameWithTownNearby();
        var wall = town.Position.Offset(-2, -3); // le coin du haut à gauche

        state.SetDestination(wall);

        Assert.Equal(town.Position, state.Path![^1]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    [InlineData(5)]
    [InlineData(13)]
    public void La_ville_generee_laisse_tout_accessible(int seed)
    {
        var map = new MapGenerator(seed, TestData.Data).Generate();
        foreach (var town in map.Objects.OfType<Town>())
        {
            foreach (var cell in town.Footprint)
            {
                Assert.True(map.InBounds(cell));
                Assert.True(TerrainInfo.IsPassable(map.GetTerrain(cell)));
                if (cell != town.Position)
                    Assert.Null(map.ObjectAt(cell));
            }
            Assert.True(map.IsPassable(town.Approach));
            Assert.Null(map.ObjectAt(town.Approach));
        }

        // Les murs de la ville ne coupent l'accès à rien
        foreach (var obj in map.Objects.Where(o => o is not MonsterGuard && o.Position != map.Start))
            Assert.NotNull(Pathfinder.FindPath(map, map.Start, obj.Position));
    }

    [Fact]
    public void Vaincre_la_garnison_donne_la_ville()
    {
        var (state, town) = GameWithTownNearby(garrisonPaysans: 3);
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
        var (state, town) = GameWithTownNearby(garrisonPaysans: 40, garrisonId: "ange");

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

        Assert.Equal(gold + 150, player.Resources[ResourceType.Gold]);
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
        Assert.Equal(150, town.DailyGold);
        town.AddBuilding(B("taverne"));
        town.AddBuilding(B("hotel-ville"));
        Assert.Equal(300, town.DailyGold);
    }

    [Fact]
    public void Les_habitations_produisent_des_creatures_chaque_semaine()
    {
        var (state, town) = GameWithTownNearby();
        town.SetOwner(state.Players[0]);
        var paysan = TestData.Creatures.Get("paysan");
        var archer = TestData.Creatures.Get("archer");
        Assert.Equal(28, town.Available[paysan]); // une semaine offerte à la construction

        Assert.Equal(BuildResult.Ok, state.Build(town, "tour-archers"));
        Assert.Equal(18, town.Available[archer]);

        for (int i = 0; i < 7; i++)
            state.EndTurn();

        Assert.Equal(56, town.Available[paysan]);
        Assert.Equal(36, town.Available[archer]);
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
            foreach (var required in building.RequirementChoices.SelectMany(c => c))
                Assert.NotNull(TestData.Data.Buildings.Get(required));
            if (building.Dwelling is { } creature)
                Assert.NotNull(TestData.Creatures.Get(creature));
        }
        // 7 habitations par faction jouable
        foreach (var faction in TestData.Data.Classes.PlayableFactions)
            Assert.Equal(7, TestData.Data.Buildings.All.Count(b => b.Dwelling != null && b.Faction == faction));
    }
}
