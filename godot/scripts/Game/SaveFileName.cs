using System;
using System.Collections.Generic;
using System.Linq;

namespace HoMM.Game;

/// <summary>La conversion d'un nom de sauvegarde en nom de fichier sûr (Windows compris).</summary>
public static class SaveFileName
{
    private static readonly HashSet<string> ReservedNames = Reserved().ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string FromSaveName(string name)
    {
        var cleaned = new string(name.Trim().Select(c => "\\/:*?\"<>|".Contains(c) || char.IsControl(c) ? '-' : c).ToArray())
            .TrimEnd('.', ' ');
        if (cleaned.Length == 0)
            return string.Empty;
        return IsReserved(cleaned) ? $"_{cleaned}" : cleaned;
    }

    private static bool IsReserved(string name)
    {
        string root = name.Split('.', 2)[0].TrimEnd('.', ' ');
        return ReservedNames.Contains(root);
    }

    private static IEnumerable<string> Reserved()
    {
        yield return "CON";
        yield return "PRN";
        yield return "AUX";
        yield return "NUL";
        for (int i = 1; i <= 9; i++)
        {
            yield return $"COM{i}";
            yield return $"LPT{i}";
        }
    }
}
