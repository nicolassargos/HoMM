using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Les fichiers de sauvegarde (étape 7) : un dossier dans celui de l'utilisateur (géré par Godot), un fichier JSON par
/// sauvegarde, nommé d'après le nom choisi. Deux noms à part : la sauvegarde rapide (F5 / F9) et la sauvegarde
/// automatique (au début de chaque tour), chacune remplacée à chaque fois.
/// </summary>
public static class SaveFiles
{
    private const string Folder = "user://sauvegardes";
    public const string QuickName = SaveEntry.QuickName;
    public const string AutoName = SaveEntry.AutoName;

    /// <summary>L'ancien fichier unique (avant l'étape 7) : il devient la sauvegarde rapide.</summary>
    private const string OldSavePath = "user://sauvegarde.json";

    private static string PathOf(string name) => $"{Folder}/{name}.json";

    private static void EnsureFolder()
    {
        DirAccess.MakeDirRecursiveAbsolute(Folder);
        if (Godot.FileAccess.FileExists(OldSavePath) && !Godot.FileAccess.FileExists(PathOf(QuickName)))
            DirAccess.RenameAbsolute(OldSavePath, PathOf(QuickName));
    }

    /// <summary>Un nom de fichier sûr pour ce nom de sauvegarde (sans les caractères interdits par Windows).</summary>
    public static string FileName(string name)
    {
        var cleaned = new string(name.Trim().Select(c => "\\/:*?\"<>|".Contains(c) || char.IsControl(c) ? '-' : c).ToArray());
        return cleaned.TrimEnd('.', ' ');
    }

    /// <summary>Les sauvegardes, la plus récente d'abord.</summary>
    public static List<SaveEntry> List()
    {
        EnsureFolder();
        var entries = new List<SaveEntry>();
        foreach (var file in DirAccess.GetFilesAt(Folder).Where(f => f.EndsWith(".json")))
        {
            string path = $"{Folder}/{file}";
            var summary = GameState.ReadSummary(Godot.FileAccess.GetFileAsString(path));
            var written = DateTimeOffset.FromUnixTimeSeconds((long)Godot.FileAccess.GetModifiedTime(path)).LocalDateTime;
            entries.Add(new SaveEntry(path, file[..^".json".Length], summary, written));
        }
        return entries.OrderByDescending(e => e.Written).ToList();
    }

    /// <summary>La sauvegarde la plus récente qu'on peut charger (pour « Continuer » et « --load »).</summary>
    public static SaveEntry? Latest() => List().FirstOrDefault(e => e.Summary != null);

    public static bool Exists(string name) => Godot.FileAccess.FileExists(PathOf(FileName(name)));

    /// <summary>Écrit la partie sous ce nom (remplace la sauvegarde du même nom). Faux si l'écriture a échoué.</summary>
    public static bool Write(string name, GameState state)
    {
        EnsureFolder();
        using var file = Godot.FileAccess.Open(PathOf(FileName(name)), Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"Sauvegarde impossible : {Godot.FileAccess.GetOpenError()}");
            return false;
        }
        file.StoreString(state.Save());
        return true;
    }

    /// <summary>La partie de ce fichier, ou null si elle ne peut pas être chargée.</summary>
    public static GameState? Read(string path, GameData data)
    {
        if (!Godot.FileAccess.FileExists(path))
            return null;
        try
        {
            return GameState.Load(Godot.FileAccess.GetFileAsString(path), data);
        }
        catch (FormatException e)
        {
            GD.PrintErr($"Chargement impossible : {e.Message}");
            return null;
        }
    }

    public static GameState? ReadNamed(string name, GameData data)
    {
        EnsureFolder();
        return Read(PathOf(name), data);
    }

    public static void Delete(SaveEntry entry) => DirAccess.RemoveAbsolute(entry.Path);
}
