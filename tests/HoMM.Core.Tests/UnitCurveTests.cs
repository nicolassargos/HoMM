using System.Linq;
using HoMM.Core;
using Xunit;

namespace HoMM.Core.Tests;

/// <summary>
/// La progression des unités du Château : combien de paysans il faut pour vaincre une unité
/// (avec un héros de départ). Chaque palier doit être environ deux fois plus fort que le précédent (depuis le 2026-11-25,
/// mesuré contre une vraie armée et avec plus de marge : voir le test).
/// Avec le combat tactique et les capacités (étape 6) : Archer 8, Griffon 13, Épéiste 26, Moine 58, Cavalier 112 ;
/// Ange 1560. Le Cavalier au niveau 4 et le Chevalier au niveau 6 (2026-11-17) : Archer 8, Griffon 14, Cavalier 25,
/// Moine 61, Chevalier 93, Ange 1597.
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

    /// <summary>
    /// Mesuré contre une vraie armée (2026-11-25, <see cref="CreatureStrength.GoldToBeat"/>) : les paysans surestiment les
    /// tireurs (l'Archer et le Moine les abattent avant qu'ils n'arrivent), et la courbe en paysans s'opposait à
    /// l'équilibre des duels à prix égal. Le double par palier est un repère, pas une règle d'or (choix de
    /// l'utilisateur : quelques unités un peu meilleures pour leur prix, pourvu que ce ne soit pas flagrant, sont un
    /// avantage à découvrir ; la mesure qui compte est l'armée entière contre celle d'une autre faction) : chaque palier
    /// doit être nettement plus fort que le précédent, de ×1,1 à ×3,5 (Archer 29, Griffon 90, Cavalier 152, Moine 175,
    /// Chevalier 490 or de l'armée de mesure).
    /// </summary>
    [Fact]
    public void Chaque_palier_du_chateau_est_nettement_plus_fort()
    {
        var ids = new[] { "archer", "griffon", "cavalier", "moine", "chevalier" };
        var values = ids.Select(id => CreatureStrength.GoldToBeat(TestData.Creatures.Get(id), TestData.Creatures)).ToList();

        for (int i = 1; i < values.Count; i++)
        {
            double ratio = values[i] / values[i - 1];
            Assert.True(ratio is >= 1.1 and <= 3.5,
                $"{ids[i]} : x{ratio:0.00} par rapport à {ids[i - 1]} ({string.Join(", ", values.Select(v => v.ToString("0.#")))})");
        }
    }

    [Fact]
    public void L_ange_reste_loin_au_dessus_du_chevalier()
    {
        var hero = new GameState(7, TestData.Data).CurrentHero!;

        Assert.True(PeasantsToBeat("ange", hero) >= 10 * PeasantsToBeat("chevalier", hero));
    }
}
