using System;
using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>Au niveau supérieur, le joueur choisit une caractéristique parmi trois.</summary>
public class LevelChoiceTests
{
    private static Hero HeroOf(GameState state) => state.CurrentHero!;

    [Fact]
    public void Atteindre_un_niveau_propose_les_quatre_caracteristiques_sans_monter_tout_de_suite()
    {
        var state = new GameState(7, TestData.Data);
        var hero = HeroOf(state);
        state.TakeEvents();

        state.GrantExperience(hero, Hero.ExperienceForLevel(2));

        var choice = state.TakeEvents().OfType<HeroLevelChoice>().Single();
        Assert.Equal(2, choice.Level);
        Assert.Equal(Enum.GetValues<HeroStat>().OrderBy(s => s), choice.Options.OrderBy(s => s));
        Assert.Equal(1, hero.Level);
        Assert.Empty(state.TakeEvents().OfType<HeroLeveledUp>());
    }

    [Fact]
    public void Le_choix_fait_monter_le_heros_avec_la_caracteristique_choisie()
    {
        var state = new GameState(7, TestData.Data);
        var hero = HeroOf(state);
        state.GrantExperience(hero, Hero.ExperienceForLevel(2));
        var choice = state.TakeEvents().OfType<HeroLevelChoice>().Single();
        var stat = choice.Options[1];
        int before = hero.Statistics.Charisma;

        Assert.True(state.ChooseStat(choice, stat));

        Assert.Equal(2, hero.Level);
        Assert.Equal(before + 1, hero.Charisma);
        var levelUp = state.TakeEvents().OfType<HeroLeveledUp>().Single();
        Assert.Equal(stat, levelUp.Stat);
        Assert.Equal(2, levelUp.After.Level);
    }

    [Fact]
    public void Un_choix_ne_sert_qu_une_fois()
    {
        var state = new GameState(7, TestData.Data);
        var hero = HeroOf(state);
        state.GrantExperience(hero, Hero.ExperienceForLevel(2));
        var choice = state.TakeEvents().OfType<HeroLevelChoice>().Single();

        Assert.True(state.ChooseStat(choice, HeroStat.Power));
        Assert.False(state.ChooseStat(choice, HeroStat.Power));
        Assert.Equal(2, hero.Level);
        Assert.Equal(2, hero.Power);
    }

    [Fact]
    public void Gagner_beaucoup_d_experience_d_un_coup_propose_un_choix_par_niveau()
    {
        var state = new GameState(7, TestData.Data);
        var hero = HeroOf(state);

        state.GrantExperience(hero, Hero.ExperienceForLevel(4));

        var levels = state.TakeEvents().OfType<HeroLevelChoice>().Select(c => c.Level);
        Assert.Equal(new[] { 2, 3, 4 }, levels);
    }
}
