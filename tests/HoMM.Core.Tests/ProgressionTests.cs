using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

public class ProgressionTests
{
    private static Army ArmyOf(string id, int count)
    {
        var army = new Army();
        army.Add(TestData.Creatures.Get(id), count);
        return army;
    }

    private static Hero HeroOf(GameState state) => state.CurrentHero!;

    [Fact]
    public void Une_troupe_sous_la_limite_ne_se_revolte_pas()
    {
        // Charisme 10 : limite de 100 points. 100 piquiers (1 point chacun) tout juste.
        var piquier = TestData.Creatures.Get("piquier");

        Assert.False(Command.IsOverLimit(piquier, 100, 10));
        Assert.True(Command.IsOverLimit(piquier, 101, 10));
        Assert.Equal(0, Command.RevoltChance(piquier, 100, 10));
    }

    [Fact]
    public void La_chance_de_revolte_vaut_la_moitie_a_deux_fois_la_limite()
    {
        var piquier = TestData.Creatures.Get("piquier");

        Assert.Equal(0.5, Command.RevoltChance(piquier, 200, 10), precision: 6);
        Assert.Equal(Command.MaxRevoltChance, Command.RevoltChance(piquier, 1000, 10));
    }

    [Fact]
    public void Un_ange_coute_beaucoup_plus_de_commandement()
    {
        var ange = TestData.Creatures.Get("ange");
        var piquier = TestData.Creatures.Get("piquier");

        Assert.True(ange.CommandCost > 10 * piquier.CommandCost);
    }

    [Fact]
    public void Le_charisme_de_depart_permet_une_armee_de_depart_sans_revolte()
    {
        var hero = HeroOf(new GameState(7, TestData.Data));

        Assert.Equal(10, hero.Charisma);
        Assert.All(hero.Army.Stacks, s => Assert.False(Command.IsOverLimit(s.Type, s.Count, hero.Charisma)));
    }

    [Fact]
    public void Il_faut_1000_puis_3000_points_d_experience_pour_les_niveaux_2_et_3()
    {
        Assert.Equal(1000, Hero.ExperienceForLevel(2));
        Assert.Equal(3000, Hero.ExperienceForLevel(3));
    }

    [Fact]
    public void Monter_de_niveau_donne_du_charisme_et_la_caracteristique_choisie()
    {
        var hero = HeroOf(new GameState(7, TestData.Data));

        hero.LevelUp(HeroStat.Power);

        Assert.Equal(2, hero.Level);
        Assert.Equal(11, hero.Charisma);
        Assert.Equal(2, hero.Power);
        Assert.Equal(1, hero.Attack);
    }

    [Fact]
    public void L_attaque_du_heros_rend_les_combats_moins_meurtriers()
    {
        var weak = HeroOf(new GameState(7, TestData.Data));
        var strong = HeroOf(new GameState(7, TestData.Data));
        for (int i = 0; i < 15; i++)
            strong.LevelUp(HeroStat.Attack);

        var plain = Battle.Resolve(ArmyOf("archer", 10), ArmyOf("ogre", 10), weak);
        var boosted = Battle.Resolve(ArmyOf("archer", 10), ArmyOf("ogre", 10), strong);

        Assert.True(boosted.DefenderLosses.Sum(s => s.Count) > plain.DefenderLosses.Sum(s => s.Count));
    }

    [Fact]
    public void Les_troupes_revoltees_combattent_sans_compter_dans_les_pertes_ennemies()
    {
        var hero = ArmyOf("piquier", 30);
        var rebels = ArmyOf("gobelin", 5);

        var report = Battle.Resolve(hero, new Army(), null, rebels);

        Assert.True(report.AttackerWon);
        Assert.Equal(5, report.Rebels.Single().Count);
        Assert.Empty(report.DefenderLosses);
        Assert.Equal(0, report.Experience);
        Assert.Empty(report.RebelSurvivors);
    }

    [Fact]
    public void L_experience_vient_de_la_valeur_des_monstres_tues()
    {
        var gobelins = ArmyOf("gobelin", 10);
        var report = Battle.Resolve(ArmyOf("piquier", 40), gobelins);

        // Un gobelin vaut 40 or
        Assert.Equal(report.DefenderLosses.Sum(s => s.Count) * 40, report.Experience);
    }
}
