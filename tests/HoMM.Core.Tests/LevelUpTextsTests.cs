using HoMM.Core;
using HoMM.Game;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>La fenêtre qui s'affiche quand un héros passe au niveau supérieur.</summary>
public class LevelUpTextsTests
{
    private static HeroLeveledUp LevelUp(HeroStat stat, HeroStatistics before, HeroStatistics after) =>
        new(new GameState(7, TestData.Data).CurrentHero!, stat, before, after);

    [Fact]
    public void La_fenetre_de_niveau_montre_les_valeurs_avant_et_apres()
    {
        var before = new HeroStatistics(1, 1, 1, 1, 1, 10);
        var after = new HeroStatistics(2, 2, 1, 1, 1, 11);

        var levelUp = LevelUp(HeroStat.Attack, before, after);

        Assert.Equal("Niveau 2 atteint !", Texts.LevelUpTitle(levelUp));
        var lines = Texts.LevelUpSummary(levelUp).Split('\n');
        Assert.Equal("Sire Roland passe au niveau 2.", lines[0]);
        Assert.Contains("Attaque : 1 → 2 (+1)", lines);
        Assert.Contains("Défense : 1 (inchangée)", lines);
        Assert.Contains("Charisme : 10 → 11 (+1)", lines);
        Assert.Contains("Commandement max. par troupe : 100 → 110 (+10)", lines);
    }

    [Fact]
    public void Le_journal_garde_une_ligne_courte_pour_le_niveau()
    {
        var levelUp = LevelUp(HeroStat.Power, new HeroStatistics(1, 1, 1, 1, 1, 10), new HeroStatistics(2, 1, 1, 2, 1, 11));

        Assert.Equal("Sire Roland passe au niveau 2 ! +1 puissance, +1 charisme.", Texts.LevelUp(levelUp));
    }
}
