using System;

namespace HoMM.Core;

/// <summary>
/// Le commandement d'un héros (étape 3) : une troupe est obéissante si nombre × coût ≤ charisme × 10.
/// Dépasser la limite est permis, mais risqué : au début d'un combat, la troupe peut se révolter.
/// La limite s'applique à chaque troupe séparément, pas à toute l'armée.
/// </summary>
public static class Command
{
    public const int PointsPerCharisma = 10;

    /// <summary>Chance maximale de révolte, même très loin de la limite.</summary>
    public const double MaxRevoltChance = 0.95;

    public static int Limit(int charisma) => charisma * PointsPerCharisma;

    /// <summary>Points de commandement occupés par une troupe.</summary>
    public static int Cost(CreatureType type, int count) => count * type.CommandCost;

    public static bool IsOverLimit(CreatureType type, int count, int charisma) =>
        Cost(type, count) > Limit(charisma);

    /// <summary>
    /// Chance qu'une troupe se révolte : nulle sous la limite, 50 % à deux fois la limite,
    /// et plafonnée à <see cref="MaxRevoltChance"/>.
    /// </summary>
    public static double RevoltChance(CreatureType type, int count, int charisma) => RevoltChanceFor(type, count, Limit(charisma));

    /// <summary>La même chance, pour une limite de commandement donnée (celle d'un héros, compétences comprises).</summary>
    public static double RevoltChanceFor(CreatureType type, int count, int limit)
    {
        double ratio = (double)Cost(type, count) / Math.Max(1, limit);
        return Math.Clamp(0.5 * (ratio - 1), 0, MaxRevoltChance);
    }
}
