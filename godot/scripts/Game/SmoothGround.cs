using System;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Le sol de toute la carte en une seule image, dessinée par le shader <c>shaders/terrain.gdshader</c> :
/// les terrains se rejoignent par des bords ondulés au lieu de carrés (étape 5).
/// Le shader reçoit le terrain de chaque case et l'image de chaque terrain ; il n'y a pas d'images de bords
/// à préparer, et changer l'image d'un terrain change aussi ses bords.
/// </summary>
public partial class SmoothGround : ColorRect
{
    public const string ShaderPath = "res://shaders/terrain.gdshader";

    /// <summary>Images par terrain : l'image et sa variante (art/terrain/grass-2.png), mélangées par le shader.</summary>
    private const int Variants = 2;

    /// <summary>
    /// Rang de chaque terrain : un terrain recouvre les bords de ceux de rang plus faible.
    /// L'herbe est au-dessus de tout : les rives et les chemins deviennent ondulés.
    /// </summary>
    public static int Rank(TerrainType terrain) => terrain switch
    {
        TerrainType.Water => 0,
        TerrainType.Road => 1,
        TerrainType.Sand => 2,
        TerrainType.Swamp => 3,
        _ => 4, // herbe (et le sol des forêts et des montagnes)
    };

    /// <summary>
    /// Développement : « -- --ground=edge_width:9,foam:0.6 » change des réglages du shader sans recompiler,
    /// pour comparer des essais sur des captures d'écran.
    /// </summary>
    private static void ApplyDevelopmentSettings(ShaderMaterial material)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--ground="));
        if (arg == null)
            return;
        foreach (var setting in arg["--ground=".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = setting.Split(':');
            if (parts.Length == 2 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value))
                material.SetShaderParameter(parts[0], value);
            else
                GD.PrintErr($"Réglage du sol illisible : {setting}");
        }
    }

    /// <summary>
    /// Le sol de la carte, ou null s'il manque le shader ou les images des terrains
    /// (le sol est alors dessiné case par case, sans bords).
    /// </summary>
    public static SmoothGround? Create(AdventureMap map)
    {
        if (!Godot.FileAccess.FileExists(ShaderPath))
            return null;
        var terrains = Enum.GetValues<TerrainType>();
        // Il faut au moins l'image de l'herbe ; un terrain sans image prend sa couleur unie
        if (Art.GetImage($"terrain/{Art.Name(TerrainType.Grass)}") is not { } grass)
            return null;
        var size = grass.GetSize();

        // Deux couches par terrain : l'image et sa variante (terrain/grass-2.png), ou deux fois l'image
        var tiles = new Godot.Collections.Array<Image>();
        foreach (var terrain in terrains)
        {
            var image = Art.GetImage($"terrain/{Art.Name(terrain)}");
            if (image == null || image.GetSize() != size)
            {
                image = Image.CreateEmpty(size.X, size.Y, false, Image.Format.Rgba8);
                image.Fill(MapRenderer.TerrainColor(terrain));
            }
            image.Convert(Image.Format.Rgba8);
            tiles.Add(image);
            var variant = Art.GetImage($"terrain/{Art.Name(terrain)}-2");
            if (variant == null || variant.GetSize() != size)
                variant = image;
            variant.Convert(Image.Format.Rgba8);
            tiles.Add(variant);
        }
        var tileArray = new Texture2DArray();
        tileArray.CreateFromImages(tiles);

        // Le terrain de chaque case ; les forêts et les montagnes ont un sol d'herbe (leur décor est dessiné à part)
        var cells = Image.CreateEmpty(map.Width, map.Height, false, Image.Format.R8);
        for (int y = 0; y < map.Height; y++)
        for (int x = 0; x < map.Width; x++)
        {
            var terrain = map.GetTerrain(new Cell(x, y));
            var ground = DecorLayer.Covers(terrain) ? TerrainType.Grass : terrain;
            cells.SetPixel(x, y, new Color((int)ground / 255f, 0, 0));
        }

        var material = new ShaderMaterial { Shader = new Shader { Code = Godot.FileAccess.GetFileAsString(ShaderPath) } };
        material.SetShaderParameter("terrain_map", ImageTexture.CreateFromImage(cells));
        material.SetShaderParameter("tiles", tileArray);
        material.SetShaderParameter("variants", Variants);
        material.SetShaderParameter("map_cells", new Vector2I(map.Width, map.Height));
        var ranks = new int[8];
        foreach (var terrain in terrains)
            ranks[(int)terrain] = Rank(terrain);
        material.SetShaderParameter("priority", ranks);
        material.SetShaderParameter("water", (int)TerrainType.Water);
        ApplyDevelopmentSettings(material);

        return new SmoothGround
        {
            Material = material,
            Size = new Vector2(map.Width, map.Height) * MapRenderer.TileSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
    }
}
