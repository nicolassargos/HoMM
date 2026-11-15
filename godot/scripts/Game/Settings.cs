using Godot;

namespace HoMM.Game;

/// <summary>
/// Les réglages du joueur, gardés d'une partie à l'autre dans <c>user://reglages.cfg</c> (pour l'instant : la vitesse des
/// projectiles au combat, les volumes du son). Lus au lancement (<see cref="Load"/>), écrits à chaque changement.
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

    /// <summary>Le volume de la musique, de 0 à 1 ; le changer l'applique (<see cref="Audio"/>) et l'enregistre.</summary>
    public static double MusicVolume
    {
        get => _musicVolume;
        set => SetVolume(ref _musicVolume, value);
    }

    /// <summary>Le volume des sons (boutons, combat, carte), de 0 à 1.</summary>
    public static double EffectsVolume
    {
        get => _effectsVolume;
        set => SetVolume(ref _effectsVolume, value);
    }

    /// <summary>Le volume des ambiances (le vent, la mer…), de 0 à 1.</summary>
    public static double AmbienceVolume
    {
        get => _ambienceVolume;
        set => SetVolume(ref _ambienceVolume, value);
    }

    private static double _musicVolume = 0.7;
    private static double _effectsVolume = 0.8;
    private static double _ambienceVolume = 0.6;

    private static void SetVolume(ref double volume, double value)
    {
        volume = Mathf.Clamp(value, 0, 1);
        Audio.ApplyVolumes();
        Save();
    }

    /// <summary>Lit les réglages enregistrés (un fichier absent ou abîmé garde les valeurs par défaut).</summary>
    public static void Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok)
            return;
        BattleEffects.ProjectileSpeed = Mathf.Clamp((double)file.GetValue("combat", "vitesse_projectiles", 1.0), 0.25, 4);
        _musicVolume = Mathf.Clamp((double)file.GetValue("son", "musique", _musicVolume), 0, 1);
        _effectsVolume = Mathf.Clamp((double)file.GetValue("son", "effets", _effectsVolume), 0, 1);
        _ambienceVolume = Mathf.Clamp((double)file.GetValue("son", "ambiance", _ambienceVolume), 0, 1);
    }

    private static void Save()
    {
        var file = new ConfigFile();
        file.Load(Path); // garde les autres réglages, s'il y en a
        file.SetValue("combat", "vitesse_projectiles", BattleEffects.ProjectileSpeed);
        file.SetValue("son", "musique", _musicVolume);
        file.SetValue("son", "effets", _effectsVolume);
        file.SetValue("son", "ambiance", _ambienceVolume);
        file.Save(Path);
    }
}
