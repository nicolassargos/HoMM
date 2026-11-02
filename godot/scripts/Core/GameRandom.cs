namespace HoMM.Core;

/// <summary>
/// Le hasard de la partie (révoltes…), tiré à partir de la graine. Contrairement à System.Random,
/// son état tient dans un nombre : il est enregistré dans la sauvegarde, et une partie chargée
/// continue exactement comme elle l'aurait fait (algorithme SplitMix64).
/// </summary>
public sealed class GameRandom
{
    public GameRandom(int seed) => State = (ulong)(uint)seed;

    /// <summary>L'état complet du générateur (pour la sauvegarde).</summary>
    public ulong State { get; internal set; }

    public ulong NextULong()
    {
        ulong z = State += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Un nombre entre 0 (compris) et 1 (exclu).</summary>
    public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));
}
