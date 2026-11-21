using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Un artefact hors de la carte (écran du héros, fenêtres) : sur le fond sombre d'une case, son image seule se lisait
/// mal (remarque de l'utilisateur, 2026-11-21). Choisi parmi quatre essais (une lueur derrière, un contour clair, un fond
/// plus clair) : <b>un contour d'un pixel de la couleur de son rang, et un halo plus pâle autour</b>. Fait à la taille de
/// l'image (pixels nets), montré ×2 comme elle.
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

    /// <summary>Opacité du halo, le second anneau autour du contour.</summary>
    private const float HaloAlpha = 0.4f;

    private static readonly Dictionary<string, Texture2D> Cache = new();

    /// <summary>L'image de l'artefact avec son contour et son halo (2 pixels de plus de chaque côté), ou null.</summary>
    public static Texture2D? Image(ArtifactType artifact)
    {
        if (Art.Get($"artifacts/{artifact.Id}") is not { } texture)
            return null;
        if (Cache.TryGetValue(artifact.Id, out var done))
            return done;
        var source = texture.GetImage();
        source.Decompress();
        const int rings = 2;
        var outline = RankColor(artifact.Rank);
        int w = source.GetWidth() + 2 * rings, h = source.GetHeight() + 2 * rings;
        var image = Godot.Image.CreateEmpty(w, h, false, Godot.Image.Format.Rgba8);
        image.BlitRect(source, new Rect2I(0, 0, source.GetWidth(), source.GetHeight()), new Vector2I(rings, rings));
        // chaque anneau : les pixels vides qui touchent (haut, bas, gauche, droite) le dessin ou l'anneau d'avant
        for (int ring = 1; ring <= rings; ring++)
        {
            var before = (Godot.Image)image.Duplicate();
            bool Filled(int x, int y) => x >= 0 && x < w && y >= 0 && y < h && before.GetPixel(x, y).A > 0.1f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (!Filled(x, y) && (Filled(x + 1, y) || Filled(x - 1, y) || Filled(x, y + 1) || Filled(x, y - 1)))
                        image.SetPixel(x, y, ring == 1 ? outline : outline with { A = HaloAlpha });
        }
        var result = ImageTexture.CreateFromImage(image);
        Cache[artifact.Id] = result;
        return result;
    }
}
