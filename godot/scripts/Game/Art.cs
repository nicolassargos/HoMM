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
///   art/creatures/battle/paysan.png : la créature au combat, plus grande et plus détaillée (sinon la petite ×3)
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
        if (texture != null)
            Names[texture] = name;
        return texture;
    }

    private static readonly Dictionary<Texture2D, string> Names = new();

    /// <summary>Le nom d'une image (« objects/mine-wood »), ou null : pour retrouver ce qui la concerne (ses pieds).</summary>
    public static string? NameOf(Texture2D texture) => Names.TryGetValue(texture, out var name) ? name : null;

    /// <summary>Donne un nom à une image faite par le jeu (le héros aux couleurs d'un joueur porte celui de son image).</summary>
    public static void Alias(Texture2D texture, string name) => Names[texture] = name;

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

    /// <summary>
    /// L'image d'une ville de cette faction sur la carte (art/objects/town-{faction}.png), sinon le château
    /// (art/objects/town.png), ou null.
    /// </summary>
    public static Texture2D? Town(string faction) => Get($"objects/town-{faction}") ?? Get("objects/town");

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
            // les ligatures s'écrivent en deux lettres (« Œil » : oeil)
            plain.Append(c switch { 'œ' => "oe", 'æ' => "ae", _ => char.IsLetterOrDigit(c) ? c.ToString() : "-" });
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
    public static void DrawSprite(CanvasItem ci, Texture2D texture, Rect2 area) =>
        ci.DrawTextureRect(texture, SpriteRect(texture, area), false);

    /// <summary>Où <see cref="DrawSprite"/> dessine l'image dans <paramref name="area"/> (en bas, centrée, sans flou).</summary>
    public static Rect2 SpriteRect(Texture2D texture, Rect2 area)
    {
        var size = texture.GetSize();
        float scale = Math.Min(area.Size.X / size.X, area.Size.Y / size.Y);
        if (scale >= 1)
            scale = MathF.Floor(scale);
        var drawn = size * scale;
        return new Rect2(new Vector2(area.Position.X + (area.Size.X - drawn.X) / 2, area.End.Y - drawn.Y), drawn);
    }

    private static readonly Dictionary<Texture2D, float> OpaqueTops = new();

    /// <summary>
    /// La hauteur (de 0, le haut de l'image, à 1, le bas) du premier pixel opaque de la colonne prise à
    /// <paramref name="column"/> de la largeur (0 à 1) : le haut du dessin à cet endroit, sous le fond transparent.
    /// 0 si la colonne est vide. Calculé une fois par image.
    /// </summary>
    public static float OpaqueTop(Texture2D texture, float column)
    {
        if (OpaqueTops.TryGetValue(texture, out var cached))
            return cached;
        float top = 0;
        if (texture.GetImage() is { } image && !image.IsEmpty())
        {
            if (image.IsCompressed())
                image.Decompress();
            int x = Math.Clamp((int)(column * image.GetWidth()), 0, image.GetWidth() - 1);
            for (int y = 0; y < image.GetHeight(); y++)
                if (image.GetPixel(x, y).A > 0.5f)
                {
                    top = (float)y / image.GetHeight();
                    break;
                }
        }
        OpaqueTops[texture] = top;
        return top;
    }
}
