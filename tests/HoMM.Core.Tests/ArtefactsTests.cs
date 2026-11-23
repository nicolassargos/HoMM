using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les artefacts (liste choisie avec l'utilisateur, 2026-11-21, et sept de ses idées le 2026-11-23) : 53 en trois rangs, et leurs effets sur le héros
/// (caractéristiques, moral, chance, vue, mouvement, points de sort, écoles de magie, revenus).
/// </summary>
public class ArtefactsTests
{
    private static ArtifactType Get(string id) => TestData.Data.Artifacts.Get(id);

    private static Hero Hero(params string[] artifacts)
    {
        var map = new AdventureMap(5, 5);
        var hero = new Hero("Aldo", new Player("Rouge", map), new Cell(1, 1));
        foreach (var id in artifacts)
            hero.Equip(Get(id));
        return hero;
    }

    [Fact]
    public void Cinquante_trois_artefacts_en_trois_rangs()
    {
        var all = TestData.Data.Artifacts.All;
        Assert.Equal(53, all.Count);
        Assert.Equal(14, all.Count(a => a.Rank == ArtifactRank.Minor));
        Assert.Equal(26, all.Count(a => a.Rank == ArtifactRank.Major));
        Assert.Equal(13, all.Count(a => a.Rank == ArtifactRank.Relic));
        Assert.All(all, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
    }

    [Fact]
    public void Les_ecoles_de_magie_existent_toutes_les_quatre()
    {
        var schools = TestData.Data.Artifacts.All.Where(a => a.School != null).Select(a => a.School).ToHashSet();
        Assert.Equal(new[] { "air", "eau", "feu", "terre" }, schools.OrderBy(s => s));
        Assert.Subset(TestData.Spells.All.Select(s => s.School).ToHashSet(), schools);
    }

    [Fact]
    public void Le_plastron_des_rois_donne_cinq_a_tout()
    {
        var hero = Hero();
        int attack = hero.Attack, defense = hero.Defense, power = hero.Power, knowledge = hero.Knowledge, charisma = hero.Charisma;

        hero.Equip(Get("plastron-des-rois"));

        Assert.Equal((attack + 5, defense + 5, power + 5, knowledge + 5, charisma + 5),
            (hero.Attack, hero.Defense, hero.Power, hero.Knowledge, hero.Charisma));
    }

    [Fact]
    public void Moral_et_chance_des_artefacts()
    {
        var hero = Hero("corne-appel", "collier-dents", "de-geant");

        Assert.Equal(3, Morale.OfArmy(hero).Artifacts);
        Assert.Equal(3, Morale.OfArmy(hero).Total);
        Assert.Equal(2, Morale.LuckOf(hero).Total);
    }

    [Fact]
    public void Vue_et_points_de_sort()
    {
        var plain = Hero();
        var hero = Hero("longue-vue", "cape-corbeau", "grimoire-anguille");

        Assert.Equal(plain.VisionRadius + 5, hero.VisionRadius);
        Assert.Equal(plain.MaxSpellPoints + 15, hero.MaxSpellPoints);
    }

    [Fact]
    public void Les_bottes_de_terre_ne_servent_pas_en_mer_et_inversement()
    {
        var plain = Hero();
        var land = Hero("bottes-arpenteur");
        var sea = Hero("bottes-liege");

        Assert.Equal(plain.MaxMovement * 125 / 100, land.MaxMovement);
        Assert.Equal(plain.SeaMovement(0), land.SeaMovement(0));
        Assert.Equal(plain.MaxMovement, sea.MaxMovement);
        Assert.Equal(plain.SeaMovement(0) * 150 / 100, sea.SeaMovement(0));
    }

    [Fact]
    public void Les_bottes_du_coureur_des_bois_effacent_le_surcout_des_terrains()
    {
        var map = new AdventureMap(3, 1);
        map.SetTerrain(new Cell(0, 0), TerrainType.Swamp);

        Assert.Equal(0, Hero().RoughTerrainReduction);
        Assert.Equal(100, Hero("bottes-coureur").RoughTerrainReduction);
        Assert.Equal(100, Pathfinder.CellCost(map, new Cell(0, 0), Hero("bottes-coureur").RoughTerrainReduction));
    }

    [Fact]
    public void Un_anneau_d_ecole_renforce_seulement_ses_sorts()
    {
        var hero = Hero("anneau-braises");

        Assert.Equal(50, hero.SpellBonus(TestData.Spells.Get("boule-de-feu")));
        Assert.Equal(0, hero.SpellBonus(TestData.Spells.Get("eclair")));
    }

    [Fact]
    public void Les_artefacts_rapportent_chaque_jour()
    {
        var hero = Hero("bourse-rapiecee", "brouette", "creuset-alchimiste");
        hero.Class = TestData.Data.Classes.Get("paladin");

        var income = hero.Owner.DailyIncome();

        Assert.Equal(250, income.GetValueOrDefault(ResourceType.Gold));
        Assert.Equal(1, income.GetValueOrDefault(ResourceType.Wood));
        Assert.Equal(1, income.GetValueOrDefault(ResourceType.Ore));
        Assert.Equal(1, income.GetValueOrDefault(ResourceType.Crystal));
    }

    [Fact]
    public void Les_talismans_a_double_tranchant_montent_deux_caracteristiques_et_en_baissent_une()
    {
        var hero = Hero();
        hero.Attack = 2; hero.Defense = 2; hero.Power = 5; hero.Knowledge = 5;

        hero.Equip(Get("talisman-brute"));

        Assert.Equal((6, 6, 2, 5), (hero.Attack, hero.Defense, hero.Power, hero.Knowledge));
    }

    [Fact]
    public void Un_talisman_ne_descend_jamais_une_caracteristique_sous_zero()
    {
        var hero = Hero();
        hero.Attack = 1;

        hero.Equip(Get("talisman-erudit"));

        Assert.Equal(0, hero.Attack);
    }

    [Fact]
    public void Les_talismans_forment_un_cycle_et_se_portent_tous_a_la_meme_place()
    {
        var talismans = new[] { "talisman-brute", "talisman-gardien", "talisman-erudit", "talisman-temeraire" }.Select(Get).ToList();
        Assert.All(talismans, t => Assert.Equal(ArtifactSlot.Misc, t.Slot));
        // chaque caractéristique monte deux fois et baisse une fois dans le cycle
        Assert.Equal(5, talismans.Sum(t => t.Attack));
        Assert.Equal(5, talismans.Sum(t => t.Defense));
        Assert.Equal(5, talismans.Sum(t => t.Power));
        Assert.Equal(5, talismans.Sum(t => t.Knowledge));
    }

    [Fact]
    public void L_oeil_du_recenseur_montre_le_nombre_exact_des_troupes_adverses()
    {
        var hero = Hero();
        Assert.False(hero.Owner.SeesExactArmies);

        hero.Equip(Get("oeil-recenseur"));

        Assert.True(hero.Owner.SeesExactArmies);
    }

    [Fact]
    public void La_longue_vue_des_guetteurs_montre_chaque_jour_les_alentours_de_toutes_les_tours()
    {
        var (state, tower) = CarteObjetsTests.GameNextTo(c => new ObservationTower(c));
        var hero = state.CurrentHero!;
        var fog = hero.Owner.Fog;
        var far = tower.Position.Offset(ObservationTower.Radius - 2, 0);
        if (!state.Map.InBounds(far))
            far = tower.Position.Offset(0, ObservationTower.Radius - 2);
        hero.Equip(Get("longue-vue-guetteurs"));
        Assert.False(fog.IsExplored(far));

        int day = state.Day;
        while (state.Day == day)
            state.EndTurn();

        Assert.True(fog.IsExplored(far));
        Assert.Empty(tower.Visitors); // sans y monter
    }

    [Fact]
    public void La_longue_vue_des_guetteurs_montre_les_tours_des_qu_on_la_ramasse()
    {
        var (state, pile) = CarteObjetsTests.GameNextTo(c => new ArtifactPile(c, Get("longue-vue-guetteurs")));
        var fog = state.CurrentHero!.Owner.Fog;
        var tower = state.Map.Objects.OfType<ObservationTower>().FirstOrDefault(t => !fog.IsExplored(t.Position));
        Assert.NotNull(tower);

        CarteObjetsTests.Visit(state, pile);

        Assert.True(fog.IsExplored(tower!.Position));
    }
}
