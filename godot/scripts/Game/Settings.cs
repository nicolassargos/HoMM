using Godot;

namespace HoMM.Game;

/// <summary>
/// Les réglages du joueur, gardés d'une partie à l'autre dans <c>user://reglages.cfg</c> (pour l'instant : la vitesse des
/// projectiles au combat). Lus au lancement (<see cref="Load"/>), écrits à chaque changement.
/// </summary>
public static class Settings
{
    private const string Path = "user://reglages.cfg";

    /// <summary>Les vitesses des projectiles proposées (comme les options de combat de Heroes 3) : lent, normal, rapide.</summary>
    public static readonly double[] ProjectileSpeeds = { 0.6, 1, 1.6 };

    /// <summary>La vitesse des projectiles (une de <see cref="ProjectileSpeeds"/>) ; la changer l'applique et l'enregistre.</summary>
    public static double ProjectileSpeed
    {
        get => BattleEffects.ProjectileSpeed;
        set
        {
            BattleEffects.ProjectileSpeed = value;
            Save();
        }
    }

    /// <summary>Lit les réglages enregistrés (un fichier absent ou abîmé garde les valeurs par défaut).</summary>
    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok)
            return;
        BattleEffects.ProjectileSpeed = Mathf.Clamp((double)file.GetValue("combat", "vitesse_projectiles", 1.0), 0.25, 4);
    }

    private static void Save()
    {
        var file = new ConfigFile();
        file.Load(Path); // garde les autres réglages, s'il y en a
        file.SetValue("combat", "vitesse_projectiles", BattleEffects.ProjectileSpeed);
        file.Save(Path);
    }
}
