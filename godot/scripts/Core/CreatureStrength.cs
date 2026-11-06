using System;
using System.Collections.Concurrent;

namespace HoMM.Core;

/// <summary>
/// La force d'une créature, mesurée au combat : combien de paysans (sans héros) il faut pour en vaincre une seule,
/// capacités comprises. Calculée une fois par créature (quelques combats rapides), puis gardée.
/// </summary>
public static class CreatureStrength
{
    private static readonly ConcurrentDictionary<CreatureType, int> Cache = new();

    public static int PeasantsToBeat(CreatureType type, CreatureCatalog creatures) =>
        Cache.GetOrAdd(type, t => Measure(t, creatures));

    private static int Measure(CreatureType type, CreatureCatalog creatures)
    {
        CreatureType peasant;
        try { peasant = creatures.Get("paysan"); }
        catch (System.Collections.Generic.KeyNotFoundException) { return 1; }
        bool Wins(int count)
        {
            var army = new Army();
            army.Add(peasant, count);
            var enemy = new Army();
            enemy.Add(type, 1);
            return Battle.Resolve(army, enemy).AttackerWon;
        }
        // on double jusqu'à gagner, puis on cherche le plus petit nombre qui gagne
        int high = 1;
        while (!Wins(high) && high < 100_000)
            high *= 2;
        int low = high / 2;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (Wins(middle))
                high = middle;
            else
                low = middle;
        }
        return Math.Max(1, high);
    }
}
