using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// Les ombres des objets de la carte, comme dans Heroes 1 et 2 : la lumière vient d'en haut à droite, chaque image (arbre,
/// sommet, mine, ville, tas, créature, héros) projette sa silhouette sur le sol, vers la gauche, sombre et
/// semi-transparente. Les ombres sont dessinées par le jeu (pas peintes dans les images) : une copie noire de l'image,
/// aplatie et penchée, posée sur le pied de l'objet.
/// Calque placé sous le décor et les objets. Les ombres sont d'abord dessinées opaques dans un groupe, que le jeu rend
/// ensuite transparent d'un coup : deux ombres qui se chevauchent (une forêt) ne foncent pas.
/// </summary>
public partial class ShadowLayer : CanvasGroup
{
    public GameState State { get; set; } = null!;
    public ActorLayer Actors { get; set; } = null!;

    /// <summary>L'opacité de l'ombre (0 : aucune, 1 : noire).</summary>
    public static float Strength { get; set; } = 0.35f;
    /// <summary>La longueur de l'ombre sur le sol, par rapport à la hauteur de l'image (1 : aussi longue qu'elle est haute).</summary>
    public static float Length { get; set; } = 0.5f;
    /// <summary>De combien l'ombre penche vers la gauche : décalage vers la gauche du haut de l'image, par rapport à sa hauteur.</summary>
    public static float Lean { get; set; } = 0.6f;

    private ShadowPainter _decor = null!;
    private ShadowPainter _actors = null!;

    public override void _Ready()
    {
        ReadOptions();
        // Les ombres du décor ne changent pas : dessinées une fois. Celles des objets et des héros, à chaque image.
        _decor = new ShadowPainter(DrawDecorShadows);
        _actors = new ShadowPainter(DrawActorShadows);
        AddChild(_decor);
        AddChild(_actors);
        Apply();
        if (OS.GetCmdlineUserArgs().Contains("--shadow-tune"))
            AddChild(new ShadowTuner(this));
    }

    public override void _Process(double delta) => _actors.QueueRedraw();

    /// <summary>Applique les réglages (après un changement) : l'opacité du groupe, la forme des ombres.</summary>
    public void Apply()
    {
        SelfModulate = new Color(0, 0, 0, Strength);
        _decor.QueueRedraw();
        _actors.QueueRedraw();
    }

    /// <summary>Développement : « --shadow=force,longueur,penche » (par exemple --shadow=0.35,0.5,0.6) pour les captures.</summary>
    private static void ReadOptions()
    {
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--shadow=")) is not { } option)
            return;
        var parts = option["--shadow=".Length..].Split(',');
        float Read(int i, float fallback) =>
            i < parts.Length && float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;
        Strength = Read(0, Strength);
        Length = Read(1, Length);
        Lean = Read(2, Lean);
    }

    private void DrawDecorShadows(CanvasItem ci)
    {
        foreach (var (foot, image) in DecorLayer.Sprites(State.Map))
        {
            var size = image.GetSize() * MapRenderer.ArtScale;
            DrawShadow(ci, image, new Rect2(foot - new Vector2(size.X / 2, size.Y), size));
        }
    }

    private void DrawActorShadows(CanvasItem ci)
    {
        foreach (var obj in State.Map.Objects)
            if (MapRenderer.ObjectArt(obj, ActorLayer.ToPixels(obj.Position)) is { } art)
                DrawShadow(ci, art.Image, art.Rect);
        foreach (var hero in State.AllHeroes)
        {
            var at = Actors.ShownAt.TryGetValue(hero, out var shown) ? shown : hero.Position;
            if (MapRenderer.HeroArt(hero.Owner, ActorLayer.ToPixels(at)) is { } art)
                DrawShadow(ci, art.Image, art.Rect);
        }
    }

    /// <summary>
    /// L'ombre d'une image dessinée en <paramref name="rect"/> : la même image, noire, couchée sur le sol à partir de son
    /// pied (sa dernière rangée opaque) ; le haut de l'image va vers la gauche et un peu vers le haut.
    /// </summary>
    private static void DrawShadow(CanvasItem ci, Texture2D image, Rect2 rect)
    {
        float scale = rect.Size.Y / image.GetHeight();
        float foot = rect.Position.Y + (OpaqueBottom(image) + 1) * scale;
        float x0 = rect.Position.X + rect.Size.X / 2;
        // Un point à la hauteur h au-dessus du pied va à h × penche vers la gauche et h × longueur vers le haut
        ci.DrawSetTransformMatrix(new Transform2D(new Vector2(1, 0), new Vector2(Lean, Length), new Vector2(x0, foot)));
        ci.DrawTextureRect(image, new Rect2(rect.Position.X - x0, rect.Position.Y - foot, rect.Size.X, rect.Size.Y), false, Colors.Black);
        ci.DrawSetTransformMatrix(Transform2D.Identity);
    }

    private static readonly Dictionary<Texture2D, int> OpaqueBottoms = new();

    /// <summary>La dernière rangée de l'image qui a un pixel opaque : le pied de l'objet (calculé une fois par image).</summary>
    private static int OpaqueBottom(Texture2D texture)
    {
        if (OpaqueBottoms.TryGetValue(texture, out int cached))
            return cached;
        int bottom = texture.GetHeight() - 1;
        if (texture.GetImage() is { } image && !image.IsEmpty())
        {
            if (image.IsCompressed())
                image.Decompress();
            for (int y = image.GetHeight() - 1; y >= 0; y--)
                if (Enumerable.Range(0, image.GetWidth()).Any(x => image.GetPixel(x, y).A > 0.5f))
                {
                    bottom = y;
                    break;
                }
        }
        OpaqueBottoms[texture] = bottom;
        return bottom;
    }
}
