using System;
using System.Collections.Generic;

namespace HoMM.Core;

/// <summary>
/// Une case hexagonale du champ de bataille, repérée par sa colonne et sa rangée. Les rangées impaires sont
/// décalées d'une demi-case vers la droite (comme dans Heroes) : chaque case a 6 voisines.
/// </summary>
public readonly record struct Hex(int Col, int Row)
{
    // Voisines d'une case d'une rangée paire, puis impaire : à droite, en haut à droite, en haut à gauche,
    // à gauche, en bas à gauche, en bas à droite
    private static readonly (int Col, int Row)[] EvenRow = { (1, 0), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1) };
    private static readonly (int Col, int Row)[] OddRow = { (1, 0), (1, -1), (0, -1), (-1, 0), (0, 1), (1, 1) };

    /// <summary>Les 6 cases voisines (certaines peuvent être hors du champ de bataille).</summary>
    public IEnumerable<Hex> Neighbours()
    {
        foreach (var (dc, dr) in (Row & 1) == 0 ? EvenRow : OddRow)
            yield return new Hex(Col + dc, Row + dr);
    }

    /// <summary>Nombre de pas d'une case à l'autre, en ligne droite (sans tenir compte des obstacles).</summary>
    public int DistanceTo(Hex other)
    {
        var (ax, ay, az) = Cube();
        var (bx, by, bz) = other.Cube();
        return (Math.Abs(ax - bx) + Math.Abs(ay - by) + Math.Abs(az - bz)) / 2;
    }

    public bool IsNextTo(Hex other) => DistanceTo(other) == 1;

    /// <summary>Coordonnées « cubiques » : les distances s'y calculent simplement.</summary>
    private (int X, int Y, int Z) Cube()
    {
        int x = Col - (Row - (Row & 1)) / 2;
        int z = Row;
        return (x, -x - z, z);
    }

    public override string ToString() => $"[{Col}, {Row}]";
}

/// <summary>Le champ de bataille : 11 × 9 cases, comme dans les premiers Heroes, avec quelques obstacles.</summary>
public sealed class Battlefield
{
    public const int Columns = 11;
    public const int Rows = 9;

    public HashSet<Hex> Obstacles { get; } = new();

    public static bool InBounds(Hex h) => h.Col >= 0 && h.Row >= 0 && h.Col < Columns && h.Row < Rows;

    /// <summary>Une case où l'on peut se tenir (dans le champ, sans obstacle).</summary>
    public bool IsOpen(Hex h) => InBounds(h) && !Obstacles.Contains(h);
}
