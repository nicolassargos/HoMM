using System;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Une sauvegarde de la liste (voir <see cref="SaveFiles"/>) : son fichier, son nom, son résumé (null si illisible) et
/// sa date d'écriture. Sans Godot, pour que les tests des textes la connaissent.
/// </summary>
public sealed record SaveEntry(string Path, string Name, SaveSummary? Summary, DateTime Written)
{
    /// <summary>Le nom de la sauvegarde rapide (F5 / F9) et celui de la sauvegarde automatique (à chaque fin de tour).</summary>
    public const string QuickName = "rapide", AutoName = "auto";

    public bool IsQuick => Name == QuickName;
    public bool IsAuto => Name == AutoName;
}
