using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// Un ange ne doit pas tomber sous un millier de paysans, avec un héros de départ. (Avec ses capacités, il vole et
/// frappe sans riposte ; avec 1200 PV et 80 de dégâts, il en faut environ 1560.)
/// </summary>
public class AngelTests
{
    private static bool Wins(int peasants, Hero hero)
    {
        var army = new Army();
        army.Add(TestData.Creatures.Get("paysan"), peasants);
        var angel = new Army();
        angel.Add(TestData.Creatures.Get("ange"), 1);
        return Battle.Resolve(army, angel, hero).AttackerWon;
    }

    [Fact]
    public void Il_faut_au_moins_mille_paysans_pour_vaincre_un_ange()
    {
        var hero = new GameState(7, TestData.Data).CurrentHero!;

        Assert.False(Wins(999, hero));
        Assert.True(Wins(2000, hero));
    }
}
