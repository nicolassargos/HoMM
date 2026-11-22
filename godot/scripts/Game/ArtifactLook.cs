using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Un artefact hors de la carte (écran du héros, fenêtres) : sur le fond sombre d'une case, son image seule se lisait
/// mal (remarque de l'utilisateur, 2026-11-21). D'abord un contour de la couleur de son rang ; puis, avec les artefacts
/// d'environ 48 pixels qui remplissent leur case, <b>une tuile de la couleur de son rang</b> (un dégradé doux, clair en haut à
/// gauche, et une ombre douce sous l'objet ; un pixel libre entre l'objet et le bord), comme les artefacts de Heroes 2. Faite à la taille de l'image
/// (pixels nets), montrée ×2 comme elle. Sur la carte, l'artefact reste posé au sol, sans tuile.
/// </summary>
public static class ArtifactLook
{
    /// <summary>La couleur d'un rang d'artefact : argent, bleu, violet.</summary>
    public static Color RankColor(ArtifactRank rank) => rank switch
    {
        ArtifactRank.Relic => MapRenderer.Rgb(190, 110, 240),
        ArtifactRank.Major => MapRenderer.Rgb(80, 150, 240),
        _ => MapRenderer.Rgb(200, 210, 220),
    };

    /// <summary>Le côté de la tuile, en pixels de l'image : l'objet (54 au plus), un pixel libre et un pixel de
    /// bord tout autour.</summary>
    public const int TileSize = 58;

    private static readonly Dictionary<string, Texture2D> Cache = new();

    /// <summary>L'image de l'artefact sur sa tuile (<see cref="TileSize"/> de côté), ou null.</summary>
    public static Texture2D? Image(ArtifactType artifact)
    {
        if (Art.Get($"artifacts/{artifact.Id}") is not { } texture)
            return null;
        if (Cache.TryGetValue(artifact.Id, out var done))
            return done;
        var source = texture.GetImage();
        source.Decompress();
        source.Convert(Godot.Image.Format.Rgba8);
        var color = RankColor(artifact.Rank);
        const int n = TileSize;
        var image = Godot.Image.CreateEmpty(n, n, false, Godot.Image.Format.Rgba8);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // le bord sombre, puis le dégradé (une teinte par diagonale) : de 0,55 fois la couleur (en haut à gauche)
                // à 0,25 fois ; plus clair, la tuile éclipsait l'objet (choix de l'utilisateur parmi quatre, 2026-11-22)
                bool edge = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                float t = (x + y) / (2f * (n - 1));
                image.SetPixel(x, y, Shade(color, edge ? 0.35f : 0.55f - 0.3f * t));
            }
        // l'objet sans ses bords vides, au milieu ; son ombre d'abord, un pixel à droite et deux plus bas
        var used = source.GetUsedRect();
        var at = new Vector2I((n - used.Size.X) / 2, (n - used.Size.Y) / 2);
        for (int pass = 0; pass < 2; pass++)
            for (int y = 0; y < used.Size.Y; y++)
                for (int x = 0; x < used.Size.X; x++)
                {
                    var pixel = source.GetPixel(used.Position.X + x, used.Position.Y + y);
                    if (pixel.A < 0.1f)
                        continue;
                    int px = at.X + x + (pass == 0 ? 1 : 0), py = at.Y + y + (pass == 0 ? 2 : 0);
                    if (px < 2 || py < 2 || px >= n - 2 || py >= n - 2)
                        continue;
                    var under = image.GetPixel(px, py);
                    image.SetPixel(px, py, pass == 0 ? Shade(under, 0.6f) : under.Lerp(pixel with { A = 1 }, pixel.A));
                }
        var result = ImageTexture.CreateFromImage(image);
        Cache[artifact.Id] = result;
        return result;
    }

    private static Color Shade(Color color, float k) =>
        new(Mathf.Clamp(color.R * k, 0, 1), Mathf.Clamp(color.G * k, 0, 1), Mathf.Clamp(color.B * k, 0, 1));
}
