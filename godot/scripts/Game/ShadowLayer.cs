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
/// aplatie et penchée, posée sur le pied de l'objet. Sous le pied, une ombre de contact (une tache sombre et floue, là
/// où l'objet touche le sol) l'ancre au sol.
/// Calque placé sous le décor et les objets. Chaque sorte d'ombre est d'abord dessinée opaque dans un groupe, que le jeu
/// rend ensuite transparent d'un coup : deux ombres qui se chevauchent (une forêt) ne foncent pas.
/// </summary>
public partial class ShadowLayer : Node2D
{
    public GameState State { get; set; } = null!;
    public ActorLayer Actors { get; set; } = null!;

    /// <summary>L'opacité de l'ombre portée (0 : aucune, 1 : noire).</summary>
    public static float Strength { get; set; } = 0.35f;
    /// <summary>La longueur de l'ombre sur le sol, par rapport à la hauteur de l'image (1 : aussi longue qu'elle est haute).</summary>
    public static float Length { get; set; } = 0.5f;
    /// <summary>De combien l'ombre penche vers la gauche : décalage vers la gauche du haut de l'image, par rapport à sa hauteur.</summary>
    public static float Lean { get; set; } = 0.6f;
    /// <summary>L'opacité de l'ombre de contact, au pied des objets (0 : aucune).</summary>
    public static float Contact { get; set; } = 0.5f;

    private CanvasGroup _cast = null!;
    private CanvasGroup _contact = null!;
    private readonly List<ShadowPainter> _painters = new();
    private readonly List<ShadowPainter> _moving = new();

    public override void _Ready()
    {
        ReadOptions();
        _cast = MakeGroup(c => ForEachDecor(c, DrawCast), c => ForEachActor(c, DrawCast), TextureFilterEnum.Nearest);
        _contact = MakeGroup(c => ForEachDecor(c, DrawContact), c => ForEachActor(c, DrawContact), TextureFilterEnum.Linear);
        Apply();
        if (OS.GetCmdlineUserArgs().Contains("--shadow-tune"))
            AddChild(new ShadowTuner(this));
    }

    /// <summary>
    /// Un groupe d'ombres : celles du décor, qui ne changent pas (dessinées une fois), et celles des objets et des héros
    /// (à chaque image).
    /// </summary>
    private CanvasGroup MakeGroup(Action<CanvasItem> decor, Action<CanvasItem> actors, TextureFilterEnum filter)
    {
        var group = new CanvasGroup();
        AddChild(group);
        var still = new ShadowPainter(decor) { Filter = filter };
        var moving = new ShadowPainter(actors) { Filter = filter };
        group.AddChild(still);
        group.AddChild(moving);
        _painters.Add(still);
        _painters.Add(moving);
        _moving.Add(moving);
        return group;
    }

    public override void _Process(double delta)
    {
        foreach (var painter in _moving)
            painter.QueueRedraw();
    }

    /// <summary>Applique les réglages (après un changement) : l'opacité des groupes, la forme des ombres.</summary>
    public void Apply()
    {
        _cast.SelfModulate = new Color(0, 0, 0, Strength);
        _contact.SelfModulate = new Color(0, 0, 0, Contact);
        foreach (var painter in _painters)
            painter.QueueRedraw();
    }

    /// <summary>
    /// Développement : « --shadow=force,longueur,penche,contact » (par exemple --shadow=0.35,0.5,0.6,0.5) pour les captures.
    /// </summary>
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
        Contact = Read(3, Contact);
    }

    private void ForEachDecor(CanvasItem ci, Action<CanvasItem, Texture2D, Rect2> draw)
    {
        foreach (var (foot, image) in DecorLayer.Sprites(State.Map))
        {
            var size = image.GetSize() * MapRenderer.ArtScale;
            draw(ci, image, new Rect2(foot - new Vector2(size.X / 2, size.Y), size));
        }
    }

    private void ForEachActor(CanvasItem ci, Action<CanvasItem, Texture2D, Rect2> draw)
    {
        foreach (var obj in State.Map.Objects)
            if (MapRenderer.ObjectArt(obj, ActorLayer.ToPixels(obj.Position)) is { } art)
                draw(ci, art.Image, art.Rect);
        foreach (var hero in State.AllHeroes)
        {
            var at = Actors.ShownAt.TryGetValue(hero, out var shown) ? shown : hero.Position;
            if (MapRenderer.HeroArt(hero.Owner, ActorLayer.ToPixels(at)) is { } art)
                draw(ci, art.Image, art.Rect);
        }
    }

    /// <summary>
    /// L'ombre portée d'une image dessinée en <paramref name="rect"/> : la même image, noire, couchée sur le sol à partir
    /// de son pied (sa dernière rangée opaque) ; le haut de l'image va vers la gauche et un peu vers le haut.
    /// </summary>
    private static void DrawCast(CanvasItem ci, Texture2D image, Rect2 rect)
    {
        float scale = rect.Size.Y / image.GetHeight();
        float foot = rect.Position.Y + (ProfileOf(image).Bottom + 1) * scale;
        float x0 = rect.Position.X + rect.Size.X / 2;
        // Un point à la hauteur h au-dessus du pied va à h × penche vers la gauche et h × longueur vers le haut
        ci.DrawSetTransformMatrix(new Transform2D(new Vector2(1, 0), new Vector2(Lean, Length), new Vector2(x0, foot)));
        ci.DrawTextureRect(image, new Rect2(rect.Position.X - x0, rect.Position.Y - foot, rect.Size.X, rect.Size.Y), false, Colors.Black);
        ci.DrawSetTransformMatrix(Transform2D.Identity);
    }

    /// <summary>
    /// L'ombre de contact d'une image dessinée en <paramref name="rect"/> : une tache floue sous la base de l'objet (la
    /// largeur de son bas), un peu poussée vers la gauche, à l'opposé de la lumière.
    /// </summary>
    private static void DrawContact(CanvasItem ci, Texture2D image, Rect2 rect)
    {
        float scale = rect.Size.Y / image.GetHeight();
        var profile = ProfileOf(image);
        float left = rect.Position.X + profile.Left * scale, right = rect.Position.X + (profile.Right + 1) * scale;
        float foot = rect.Position.Y + (profile.Bottom + 1) * scale;
        float width = (right - left) * 1.2f + 6;
        float height = Math.Clamp(width * 0.3f, 8, MapRenderer.TileSize * 0.6f);
        var center = new Vector2((left + right) / 2 - width * 0.06f, foot - height * 0.2f);
        ci.DrawTextureRect(Blot, new Rect2(center - new Vector2(width, height) / 2, width, height), false);
    }

    /// <summary>Une tache ronde, noire au centre et transparente au bord.</summary>
    private static readonly GradientTexture2D Blot = new()
    {
        Width = 64,
        Height = 64,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1f, 0.5f),
        Gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.45f, 1f },
            Colors = new[] { Colors.Black, new Color(0, 0, 0, 0.75f), new Color(0, 0, 0, 0) },
        },
    };

    /// <summary>Le bas du dessin d'une image : sa dernière rangée opaque (le pied) et la largeur de sa base.</summary>
    private readonly record struct Profile(int Bottom, int Left, int Right);

    private static readonly Dictionary<Texture2D, Profile> Profiles = new();

    /// <summary>
    /// Le pied de l'image (sa dernière rangée opaque) et la largeur de sa base (les pixels opaques du bas, sur le
    /// cinquième de la hauteur du dessin). Calculé une fois par image.
    /// </summary>
    private static Profile ProfileOf(Texture2D texture)
    {
        if (Profiles.TryGetValue(texture, out var cached))
            return cached;
        int w = texture.GetWidth(), h = texture.GetHeight();
        var profile = new Profile(h - 1, 0, w - 1);
        if (texture.GetImage() is { } image && !image.IsEmpty())
        {
            if (image.IsCompressed())
                image.Decompress();
            bool Row(int y) => Enumerable.Range(0, w).Any(x => image.GetPixel(x, y).A > 0.5f);
            int bottom = Enumerable.Range(0, h).Reverse().FirstOrDefault(Row, -1);
            int top = Enumerable.Range(0, h).FirstOrDefault(Row, -1);
            if (bottom >= 0)
            {
                int band = Math.Max(2, (bottom - top + 1) / 5);
                int left = w, right = -1;
                for (int y = bottom - band + 1; y <= bottom; y++)
                for (int x = 0; x < w; x++)
                    if (image.GetPixel(x, y).A > 0.5f)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
                profile = new Profile(bottom, left, right);
            }
        }
        Profiles[texture] = profile;
        return profile;
    }
}
