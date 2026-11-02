using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// La progression des unités du Château : combien de paysans il faut pour vaincre une unité
/// (avec un héros de départ). Chaque palier doit être environ deux fois plus fort que le précédent.
/// </summary>
public class UnitCurveTests
{
    private static bool Wins(int peasants, CreatureType target, Hero hero)
    {
        var army = new Army();
        army.Add(TestData.Creatures.Get("paysan"), peasants);
        var enemy = new Army();
        enemy.Add(target, 1);
        return Battle.Resolve(army, enemy, hero).AttackerWon;
    }

    /// <summary>Le plus petit nombre de paysans qui bat une unité.</summary>
    private static int PeasantsToBeat(string id, Hero hero)
    {
        var target = TestData.Creatures.Get(id);
        int lo = 1, hi = 20000;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (Wins(mid, target, hero)) hi = mid;
            else lo = mid + 1;
        }
        return lo;
    }

    [Fact]
    public void Chaque_palier_du_chateau_est_environ_deux_fois_plus_fort()
    {
        var hero = new GameState(7, TestData.Data).CurrentHero!;
        var ids = new[] { "archer", "griffon", "epeiste", "moine", "cavalier" };
        var values = ids.Select(id => PeasantsToBeat(id, hero)).ToList();

        for (int i = 1; i < values.Count; i++)
        {
            double ratio = (double)values[i] / values[i - 1];
            Assert.True(ratio is >= 1.5 and <= 2.5, $"{ids[i]} : x{ratio:0.00} par rapport à {ids[i - 1]}");
        }
    }

    [Fact]
    public void L_ange_reste_loin_au_dessus_du_cavalier()
    {
        var hero = new GameState(7, TestData.Data).CurrentHero!;

        Assert.True(PeasantsToBeat("ange", hero) >= 10 * PeasantsToBeat("cavalier", hero));
    }
}
