using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Core;

/// <summary>
/// La force d'une créature, mesurée au combat (capacités comprises), calculée une fois par créature puis gardée.
/// </summary>
public static class CreatureStrength
{
    private static readonly ConcurrentDictionary<CreatureType, int> Cache = new();
    private static readonly ConcurrentDictionary<CreatureType, double> ArmyCache = new();

    /// <summary>Combien de paysans (sans héros) il faut pour vaincre une seule de ces créatures.</summary>
    public static int PeasantsToBeat(CreatureType type, CreatureCatalog creatures) =>
        Cache.GetOrAdd(type, t => Measure(t, creatures));

    /// <summary>
    /// L'armée du Château qui sert à mesurer (<see cref="GoldToBeat"/>) : des troupes nombreuses des quatre premiers
    /// niveaux, dans les proportions de leur croissance, pour que l'armée grandisse par petits pas.
    /// </summary>
    private static readonly (string Id, int Count)[] MeasuringArmy = { ("paysan", 30), ("archer", 18), ("griffon", 14), ("epeiste", 8) };

    /// <summary>La troupe mesurée vaut à peu près tant d'or (une seule créature au moins).</summary>
    private const int MeasuredStackGold = 3000;

    /// <summary>
    /// La force d'une créature contre une vraie armée (2026-11-11, pour les gardiens de la carte) : l'or d'une armée du
    /// Château (<see cref="MeasuringArmy"/>) qu'il faut pour vaincre une troupe d'environ <see cref="MeasuredStackGold"/>
    /// or de ces créatures en perdant au plus 30 % de ses points de vie, divisé par le nombre de créatures. Plus juste que
    /// <see cref="PeasantsToBeat"/> : un tireur seul y paraissait très fort (il tire pendant que les paysans approchent).
    /// </summary>
    public static double GoldToBeat(CreatureType type, CreatureCatalog creatures) =>
        ArmyCache.GetOrAdd(type, t => MeasureAgainstArmy(t, creatures));

    private static double MeasureAgainstArmy(CreatureType type, CreatureCatalog creatures)
    {
        var mix = MeasuringArmy.Select(m => (Type: creatures.All.FirstOrDefault(c => c.Id == m.Id), m.Count)).ToList();
        if (mix.Any(m => m.Type == null))
            return 1;
        int gold = Math.Max(1, type.Cost.GetValueOrDefault(ResourceType.Gold));
        int count = Math.Max(1, (int)Math.Round((double)MeasuredStackGold / gold));
        int Size(int perWeek, double k) => (int)Math.Round(perWeek * k);
        double Gold(double k) => mix.Sum(m => Size(m.Count, k) * m.Type!.Cost.GetValueOrDefault(ResourceType.Gold));
        bool Beats(double k)
        {
            var army = new Army();
            foreach (var (t, n) in mix.Where(m => Size(m.Count, k) > 0))
                army.Add(t!, Size(n, k));
            if (army.Stacks.Count == 0)
                return false;
            double hp = army.Stacks.Sum(s => s.Count * s.Type.HitPoints);
            var enemy = new Army();
            enemy.Add(type, count);
            var report = Battle.Resolve(army, enemy);
            return report.AttackerWon && report.AttackerLosses.Sum(s => s.Count * s.Type.HitPoints) <= 0.3 * hp;
        }
        // on grandit l'armée jusqu'à gagner, puis on cherche entre les deux derniers pas
        double high = 0.02;
        while (!Beats(high) && high < 5000)
            high *= 1.5;
        double low = high / 1.5;
        for (int i = 0; i < 10; i++)
        {
            double middle = (low + high) / 2;
            if (Beats(middle))
                high = middle;
            else
                low = middle;
        }
        return Gold(high) / count;
    }

    /// <summary>
    /// Mesure d'un coup toutes les créatures (en parallèle : chaque mesure est indépendante et donne toujours le même
    /// résultat), avant de générer une carte : quelques centaines de combats rapides, une seule fois par lancement.
    /// </summary>
    public static void Prepare(CreatureCatalog creatures) =>
        System.Threading.Tasks.Parallel.ForEach(creatures.All, type =>
        {
            PeasantsToBeat(type, creatures);
            GoldToBeat(type, creatures);
        });

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
