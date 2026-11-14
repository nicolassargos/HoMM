using System.Linq;
using HoMM.Core;
using Xunit;
using static HoMM.Core.Tests.MerTests;

namespace HoMM.Core.Tests;

/// <summary>
/// Les objets de la mer (étape 3 de la mer, 2026-11-14) : épave flottante, coffre de la mer, bouée et sirènes, tourbillon,
/// phare, et la compétence Navigation.
/// </summary>
public class ObjetsMerTests
{
    /// <summary>La partie de <see cref="MerTests.GameByTheSea"/>, le héros déjà en mer en (7, 4), au début d'un nouveau jour.</summary>
    private static (GameState State, Hero Hero) AtSea()
    {
        var (state, hero) = GameByTheSea();
        Walk(state, new Cell(7, 4));
        state.EndTurn();
        state.TakeEvents();
        return (state, hero);
    }

    [Fact]
    public void L_epave_flottante_donne_du_bois_et_de_l_or()
    {
        var (state, hero) = AtSea();
        state.Map.AddObject(new Flotsam(new Cell(9, 4), 10, 500));
        var resources = hero.Owner.Resources;
        int wood = resources[ResourceType.Wood], gold = resources[ResourceType.Gold];

        Walk(state, new Cell(9, 4));

        Assert.Equal(wood + 10, resources[ResourceType.Wood]);
        Assert.Equal(gold + 500, resources[ResourceType.Gold]);
        Assert.Null(state.Map.ObjectAt(new Cell(9, 4)));
        Assert.True(hero.AtSea(state.Map));
    }

    [Fact]
    public void Le_coffre_de_la_mer_donne_de_l_or()
    {
        var (state, hero) = AtSea();
        state.Map.AddObject(new SeaChest(new Cell(10, 6), 1500));
        int gold = hero.Owner.Resources[ResourceType.Gold];

        Walk(state, new Cell(10, 6));

        Assert.Equal(gold + 1500, hero.Owner.Resources[ResourceType.Gold]);
        Assert.Contains(state.TakeEvents(), e => e is SeaChestOpened { Gold: 1500 });
    }

    [Fact]
    public void La_bouee_donne_du_moral_et_les_sirenes_de_la_chance()
    {
        var (state, hero) = AtSea();
        state.Map.AddObject(new BlessingSite(new Cell(9, 4), Blessing.Morale) { Sea = true });
        state.Map.AddObject(new BlessingSite(new Cell(11, 4), Blessing.Luck) { Sea = true });

        Walk(state, new Cell(9, 4));
        Walk(state, new Cell(11, 4));

        Assert.Equal(1, hero.SiteMorale);
        Assert.Equal(1, hero.SiteLuck);
        Assert.NotNull(state.Map.ObjectAt(new Cell(9, 4))); // elles restent
    }

    [Fact]
    public void Le_tourbillon_emporte_le_bateau_et_quelques_creatures()
    {
        var (state, hero) = AtSea();
        state.Map.AddObject(new Whirlpool(new Cell(9, 4), new Cell(10, 30)));
        state.Map.AddObject(new Whirlpool(new Cell(10, 30), new Cell(9, 4)));
        var weakest = hero.Army.Stacks.OrderBy(s => s.Type.Tier).ThenBy(s => s.Count).First();
        var (type, count) = (weakest.Type, weakest.Count);
        int total = hero.Army.TotalCount;

        Walk(state, new Cell(9, 4));

        Assert.Equal(new Cell(10, 30), hero.Position);
        Assert.Null(state.Path);
        int lost = System.Math.Max(1, (int)System.Math.Round(count * Whirlpool.LossPercent / 100.0));
        Assert.Equal(total - lost, hero.Army.TotalCount);
        Assert.Contains(state.TakeEvents(), e => e is WhirlpoolCrossed c && c.LostType == type && c.Lost == lost);
    }

    [Fact]
    public void Le_tourbillon_ne_prend_jamais_la_derniere_creature()
    {
        var (state, hero) = AtSea();
        foreach (var stack in hero.Army.Stacks)
            stack.Count = 0;
        hero.Army.RemoveEmpty();
        hero.Army.Add(TestData.Creatures.Get("paysan"), 1);
        state.Map.AddObject(new Whirlpool(new Cell(9, 4), new Cell(10, 30)));
        state.Map.AddObject(new Whirlpool(new Cell(10, 30), new Cell(9, 4)));

        Walk(state, new Cell(9, 4));

        Assert.Equal(1, hero.Army.TotalCount);
        Assert.Equal(new Cell(10, 30), hero.Position);
    }

    [Fact]
    public void Le_phare_donne_plus_de_mouvement_en_mer_a_son_proprietaire()
    {
        var (state, hero) = GameByTheSea();
        // un phare sur la rive, à côté du héros
        state.Map.AddObject(new Lighthouse(new Cell(5, 8)));
        Walk(state, new Cell(5, 8));
        Assert.Equal(hero.Owner, state.Map.Objects.OfType<Lighthouse>().Single().Owner);
        state.EndTurn();
        Walk(state, new Cell(7, 4)); // à bord
        state.EndTurn(); // un jour commencé en mer

        Assert.Equal(hero.MaxMovement + Lighthouse.SeaMovementBonus, hero.Movement);
        Assert.Equal(hero.Movement, hero.DayMovement);
        // à terre, rien de plus
        Assert.Equal(hero.MaxMovement, state.DayMovement(hero, atSea: false));
    }

    [Fact]
    public void La_navigation_fait_aller_plus_loin_en_mer_et_baisse_le_prix_des_bateaux()
    {
        var (state, hero) = AtSea();
        Assert.Equal(1000, state.BoatCost(hero.Owner)[ResourceType.Gold]);
        hero.AddSkillRank(TestData.Data.Skills.Get(Skills.Navigation));

        state.EndTurn();

        Assert.Equal(hero.MaxMovement * 150 / 100, hero.Movement);
        Assert.Equal(800, state.BoatCost(hero.Owner)[ResourceType.Gold]);
        Assert.Equal(8, state.BoatCost(hero.Owner)[ResourceType.Wood]);
    }

    [Fact]
    public void Les_objets_de_la_mer_se_sauvegardent()
    {
        var (state, hero) = AtSea();
        var map = state.Map;
        map.AddObject(new Flotsam(new Cell(9, 10), 5, 200));
        map.AddObject(new SeaChest(new Cell(9, 14), 1000));
        map.AddObject(new BlessingSite(new Cell(9, 18), Blessing.Luck) { Sea = true });
        map.AddObject(new Whirlpool(new Cell(9, 22), new Cell(11, 30)));
        map.AddObject(new Lighthouse(new Cell(5, 20)) { Owner = hero.Owner });

        var loaded = GameState.Load(state.Save(), TestData.Data).Map;

        Assert.Equal((5, 200), loaded.ObjectAt(new Cell(9, 10)) is Flotsam f ? (f.Wood, f.Gold) : default);
        Assert.Equal(1000, Assert.IsType<SeaChest>(loaded.ObjectAt(new Cell(9, 14))).Gold);
        Assert.True(Assert.IsType<BlessingSite>(loaded.ObjectAt(new Cell(9, 18))).Sea);
        Assert.Equal(new Cell(11, 30), Assert.IsType<Whirlpool>(loaded.ObjectAt(new Cell(9, 22))).Exit);
        Assert.Equal(hero.Owner.Id, Assert.IsType<Lighthouse>(loaded.ObjectAt(new Cell(5, 20))).Owner?.Id);
    }

    [Theory]
    [InlineData(MapType.Seas)]
    [InlineData(MapType.Islands)]
    [InlineData(MapType.Archipelago)]
    public void Le_generateur_pose_les_objets_de_la_mer(MapType type)
    {
        var map = new GameState(1, TestData.Data, MapSize.Normal, opponents: 1, mapType: type).Map;
        Assert.Contains(map.Objects, o => o is Flotsam);
        Assert.Contains(map.Objects, o => o is SeaChest);
        Assert.Contains(map.Objects, o => o is BlessingSite { Sea: true });
        Assert.Contains(map.Objects, o => o is Lighthouse);
        // sur l'eau, sauf le phare (sur la côte) ; les tourbillons par paires
        Assert.All(map.Objects.Where(o => o is Flotsam or SeaChest or Whirlpool or BlessingSite { Sea: true }), o => Assert.True(map.IsWater(o.Position)));
        Assert.All(map.Objects.OfType<Lighthouse>(), l => Assert.False(map.IsWater(l.Position)));
        var whirlpools = map.Objects.OfType<Whirlpool>().ToList();
        Assert.All(whirlpools, w => Assert.Contains(whirlpools, other => other.Position == w.Exit && other.Exit == w.Position));
    }

    [Fact]
    public void Le_continent_n_a_pas_d_objets_de_la_mer()
    {
        var map = new GameState(1, TestData.Data, MapSize.Normal, opponents: 1).Map;
        Assert.DoesNotContain(map.Objects, o => o is Flotsam or SeaChest or Whirlpool or Lighthouse or BlessingSite { Sea: true });
    }
}
