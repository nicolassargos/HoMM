using System;
using System.Collections.Generic;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Les images du jeu (étape 5), trouvées par convention à partir des identifiants, sans chemin dans les règles :
///   art/terrain/grass.png (variantes : grass-2.png, grass-3.png…)
///   art/decor/pine.png, oak.png, autumn.png, peak.png, peak-small.png (variantes aussi) : arbres et
///   sommets posés sur l'herbe (<see cref="DecorLayer"/>)
///   art/objects/pile-gold.png, chest.png, mine-gold.png, town.png
///   art/creatures/paysan.png, art/heroes/hero.png, art/icons/gold.png
///   art/heroes/portraits/sire-roland.png : le portrait d'un héros, d'après son nom (<see cref="Portrait"/>)
/// Une image absente renvoie null : le dessin par formes (<see cref="MapRenderer"/>) prend alors le relais.
/// Les PNG sont lus directement (pas besoin de les importer avec l'éditeur Godot).
/// </summary>
public static class Art
{
    public const string Root = "res://art/";

    private static readonly Dictionary<string, Texture2D?> Cache = new();
    private static readonly Dictionary<string, Texture2D[]> Variants = new();

    /// <summary>L'image <c>art/{name}.png</c>, ou null si elle n'existe pas.</summary>
    public static Texture2D? Get(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
            return cached;
        var texture = Load($"{Root}{name}.png");
        Cache[name] = texture;
        return texture;
    }

    private static Texture2D? Load(string path) => LoadImageFile(path) is { } image ? ImageTexture.CreateFromImage(image) : null;

    /// <summary>L'image <c>art/{name}.png</c> elle-même (pour la retoucher avant de l'afficher), ou null.</summary>
    public static Image? GetImage(string name) => LoadImageFile($"{Root}{name}.png");

    private static Image? LoadImageFile(string path)
    {
        if (!Godot.FileAccess.FileExists(path))
            return null;
        var image = Image.LoadFromFile(path);
        if (image == null || image.IsEmpty())
        {
            GD.PrintErr($"Image illisible : {path}");
            return null;
        }
        return image;
    }

    /// <summary>Le portrait d'un héros (art/heroes/portraits/{nom en minuscules, sans accents, avec des tirets}), ou null.</summary>
    public static Texture2D? Portrait(string heroName) => Get($"heroes/portraits/{Slug(heroName)}");

    /// <summary>« Dame Énide » → « dame-enide » : minuscules, sans accents, tirets à la place des espaces.</summary>
    public static string Slug(string text)
    {
        var plain = new System.Text.StringBuilder();
        foreach (char c in text.Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant())
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
                continue;
            plain.Append(char.IsLetterOrDigit(c) ? c : '-');
        }
        return string.Join('-', plain.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Nom d'un terrain dans les fichiers (grass, sand…).</summary>
    public static string Name(TerrainType terrain) => terrain.ToString().ToLowerInvariant();

    /// <summary>Nom d'une ressource dans les fichiers (gold, wood…).</summary>
    public static string Name(ResourceType resource) => resource.ToString().ToLowerInvariant();

    /// <summary>
    /// Une image du terrain, choisie parmi ses variantes (grass, grass-2, grass-3…) selon <paramref name="pick"/>.
    /// Null si le terrain n'a pas d'image.
    /// </summary>
    public static Texture2D? Terrain(TerrainType terrain, int pick) => Variant($"terrain/{Name(terrain)}", pick);

    /// <summary>
    /// Une image choisie parmi les variantes de <paramref name="name"/> (name, name-2, name-3…) selon
    /// <paramref name="pick"/>. Null s'il n'y en a aucune.
    /// </summary>
    public static Texture2D? Variant(string name, int pick)
    {
        var variants = VariantsOf(name);
        return variants.Length == 0 ? null : variants[Math.Abs(pick) % variants.Length];
    }

    /// <summary>Toutes les variantes de <paramref name="name"/> (name, name-2, name-3…), peut-être aucune.</summary>
    public static Texture2D[] VariantsOf(string name)
    {
        if (!Variants.TryGetValue(name, out var variants))
        {
            var found = new List<Texture2D>();
            if (Get(name) is { } first)
            {
                found.Add(first);
                for (int i = 2; Get($"{name}-{i}") is { } next; i++)
                    found.Add(next);
            }
            variants = found.ToArray();
            Variants[name] = variants;
        }
        return variants;
    }

    /// <summary>
    /// Dessine une image dans une zone, posée en bas et centrée, sans la déformer. Agrandie d'un nombre
    /// entier de fois quand c'est possible, pour que les pixels restent nets.
    /// </summary>
    public static void DrawSprite(CanvasItem ci, Texture2D texture, Rect2 area)
    {
        var size = texture.GetSize();
        float scale = Math.Min(area.Size.X / size.X, area.Size.Y / size.Y);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        var drawn = size * scale;
        var position = new Vector2(area.Position.X + (area.Size.X - drawn.X) / 2, area.End.Y - drawn.Y);
        ci.DrawTextureRect(texture, new Rect2(position, drawn), false);
    }
}
