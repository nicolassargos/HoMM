using System;
using System.Globalization;
using System.Linq;
using Godot;

namespace HoMM.Game;

/// <summary>
/// Le vent de la carte (2026-11-25) : des rafales qui la traversent en bandes larges, toutes dans le même sens. Les arbres
/// penchent et s'agitent sous une rafale (le shader du décor, <see cref="DecorLayer"/>), les feuilles s'en détachent
/// (<see cref="LeafLayer"/>). Les deux lisent la même rafale : le shader en a une copie (<c>mountain_slopes.gdshader</c>).
/// Développement : « -- --wind=tree_sway:2,leaf_rate:0.05 » change ces réglages, « wind_at:3 » fige l'instant (captures).
/// </summary>
public static class Wind
{
    /// <summary>Le sens du vent (vers la droite, un peu vers le bas).</summary>
    public static Vector2 Direction { get; private set; } = new Vector2(1f, 0.3f).Normalized();
    /// <summary>L'écart entre deux rafales, en pixels de la carte.</summary>
    public static float GustLength { get; private set; } = 7f * MapRenderer.TileSize;
    /// <summary>La vitesse des rafales, en pixels de la carte par seconde.</summary>
    public static float GustSpeed { get; private set; } = 1.5f * MapRenderer.TileSize;
    /// <summary>De combien le haut d'un arbre penche au plus fort d'une rafale, en pixels d'art.</summary>
    public static float TreeSway { get; private set; } = 1.5f;
    /// <summary>Les feuilles qui se détachent d'un arbre par seconde, au plus fort d'une rafale.</summary>
    public static float LeafRate { get; private set; } = 0.03f;
    /// <summary>La vitesse des feuilles portées par une rafale, en pixels de la carte par seconde.</summary>
    public static float LeafSpeed { get; private set; } = 0.6f * MapRenderer.TileSize;

    private static float? _frozenAt;

    /// <summary>L'instant du vent, en secondes (figé pour une capture).</summary>
    public static float Now => _frozenAt ?? (float)(Time.GetTicksMsec() / 1000.0 % 3600.0);

    /// <summary>
    /// La force de la rafale à cet endroit (0 : calme, 1 : au plus fort) : des bandes qui avancent dans le sens du vent,
    /// plus ou moins fortes sur leur longueur. La même formule que <c>gust()</c> dans le shader.
    /// </summary>
    public static float Gust(Vector2 p, float t)
    {
        const float tau = MathF.PI * 2f;
        float along = p.Dot(Direction), across = p.Dot(new Vector2(-Direction.Y, Direction.X));
        float band = 0.5f + 0.5f * MathF.Sin(tau * (along - GustSpeed * t) / GustLength);
        float vary = 0.5f + 0.5f * MathF.Sin(tau * across / (GustLength * 1.7f) + 0.8f * MathF.Sin(tau * along / (GustLength * 2.9f)) + 0.21f * t);
        float x = Math.Clamp((band * (0.35f + 0.65f * vary) - 0.3f) / 0.7f, 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    /// <summary>Lit « --wind=… » (une fois) et donne les réglages au shader du décor.</summary>
    public static void Apply(ShaderMaterial? material)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--wind="));
        foreach (var setting in arg?["--wind=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>())
        {
            var parts = setting.Split(':');
            if (parts.Length != 2 || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            {
                GD.PrintErr($"Réglage du vent illisible : {setting}");
                continue;
            }
            switch (parts[0])
            {
                case "gust_length": GustLength = v * MapRenderer.TileSize; break;
                case "gust_speed": GustSpeed = v * MapRenderer.TileSize; break;
                case "tree_sway": TreeSway = v; break;
                case "leaf_rate": LeafRate = v; break;
                case "leaf_speed": LeafSpeed = v * MapRenderer.TileSize; break;
                case "wind_at": _frozenAt = v; break;
                default: GD.PrintErr($"Réglage du vent inconnu : {parts[0]}"); break;
            }
        }
        if (material == null)
            return;
        material.SetShaderParameter("wind_dir", Direction);
        material.SetShaderParameter("gust_length", GustLength);
        material.SetShaderParameter("gust_speed", GustSpeed);
        material.SetShaderParameter("tree_sway", TreeSway);
        material.SetShaderParameter("art_scale", (float)MapRenderer.ArtScale);
    }
}
