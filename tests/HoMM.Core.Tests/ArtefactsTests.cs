using System.Collections.Generic;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Les artefacts (liste choisie avec l'utilisateur, 2026-11-21) : 46 en trois rangs, et leurs effets sur le héros
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
    public void Quarante_six_artefacts_en_trois_rangs()
    {
        var all = TestData.Data.Artifacts.All;
        Assert.Equal(46, all.Count);
        Assert.Equal(14, all.Count(a => a.Rank == ArtifactRank.Minor));
        Assert.Equal(19, all.Count(a => a.Rank == ArtifactRank.Major));
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
}
