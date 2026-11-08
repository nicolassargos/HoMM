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
    /// <summary>L'opacité de l'ombre de contact, au pied des objets (0 : aucune ; pas encore au point, éteinte par défaut).</summary>
    public static float Contact { get; set; } = 0f;

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
        CastImages.Clear(); // la forme des ombres portées a peut-être changé
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
    /// de son pied ; le haut de l'image va vers la gauche et un peu vers le haut.
    /// </summary>
    private static void DrawCast(CanvasItem ci, Texture2D image, Rect2 rect)
    {
        var (shadow, padLeft, padTop) = CastImage(image);
        float scale = rect.Size.Y / image.GetHeight();
        var at = rect.Position - new Vector2(padLeft, padTop) * scale;
        ci.DrawTextureRect(shadow, new Rect2(at, shadow.GetSize() * scale), false);
    }

    private static readonly Dictionary<Texture2D, (Texture2D Shadow, int PadLeft, int PadTop)> CastImages = new();

    /// <summary>Largeur (en pixels d'art) au-delà de laquelle un morceau d'image est un grand objet vu de trois quarts.</summary>
    private const int WidePiece = 40;

    /// <summary>
    /// Un morceau plus petit (en pixels) ou plus creux (part de son rectangle couverte) ne fait pas d'ombre : un point
    /// isolé, des rails, un chemin, un détail posé à plat sur le sol.
    /// </summary>
    private const int TinyPiece = 12;
    private const float HollowPiece = 0.3f;

    /// <summary>Un morceau étroit (en pixels d'art) qui touche le bas de l'image est un chemin vers l'entrée : pas d'ombre.</summary>
    private const int PathWidth = 8;

    /// <summary>De combien (en pixels d'art) on bouche les trous et les creux de l'ombre : une tache simple, pas un dessin.</summary>
    private const int Smoothing = 2;

    /// <summary>
    /// L'ombre portée d'une image, en pixels d'art (calculée une fois par image et par réglage). L'image est découpée en
    /// morceaux (les pixels opaques qui se touchent : un arbre, un rocher, un tas de bûches). Un morceau étroit tourne
    /// autour de son pied, son pixel le plus bas au milieu du morceau (le tronc d'un arbre, les pieds d'une créature) : un
    /// pixel à la hauteur h au-dessus du pied va à h × penche vers la gauche et h × longueur vers le haut.
    /// Un morceau large (les remparts d'une ville) est vu de trois quarts : le bas d'une tour du fond est plus haut dans
    /// l'image que le bas du mur de devant ; chacune de ses colonnes se tient alors sur son propre sol (son pixel le plus
    /// bas). Les morceaux minuscules ou creux (rails, chemins) n'en font pas, et l'ombre est lissée (trous et creux
    /// bouchés). Renvoie aussi la marge ajoutée à gauche et en haut.
    /// </summary>
    private static (Texture2D Shadow, int PadLeft, int PadTop) CastImage(Texture2D texture)
    {
        if (CastImages.TryGetValue(texture, out var cached))
            return cached;
        int w = texture.GetWidth(), h = texture.GetHeight();
        int padSide = (int)MathF.Ceiling(Math.Abs(Lean) * h) + 1;
        int padTop = (int)MathF.Ceiling(Math.Max(0, Length - 1) * h) + 1;
        int padBottom = (int)MathF.Ceiling(Length * h) + 1; // les pixels plus bas que le pied (les côtés d'un tas)
        int sw = w + 2 * padSide, sh = h + padTop + padBottom;
        var mask = new bool[sw, sh];
        if (texture.GetImage() is { } image && !image.IsEmpty())
        {
            if (image.IsCompressed())
                image.Decompress();
            foreach (var whole in Pieces(image))
            {
                var piece = whole;
                int minX = piece.Min(p => p.X), maxX = piece.Max(p => p.X);
                int minY = piece.Min(p => p.Y), maxY = piece.Max(p => p.Y);
                if (piece.Count < TinyPiece || piece.Count < HollowPiece * (maxX - minX + 1) * (maxY - minY + 1))
                    continue;
                if (maxY == h - 1 && maxX - minX + 1 <= PathWidth)
                    continue; // un chemin qui part de l'entrée vers la case de devant : posé à plat
                bool wide = maxX - minX + 1 > WidePiece;
                if (wide)
                {
                    // un grand objet : ses parties fines (chemin vers l'entrée, rails, mâts) ne comptent ni pour le sol ni
                    // pour l'ombre, sinon le sol de ces colonnes descendrait jusqu'au bout du chemin
                    piece = Core(piece, minX, minY, maxX, maxY);
                    if (piece.Count == 0)
                        continue;
                }
                // le sol de chaque colonne du morceau (son pixel le plus bas), et le pied (le plus bas du tiers du milieu)
                var bottoms = piece.GroupBy(p => p.X).ToDictionary(g => g.Key, g => g.Max(p => p.Y));
                int third = (maxX - minX + 1) / 3;
                int pivot = bottoms.Where(b => b.Key >= minX + third && b.Key <= maxX - third).Select(b => b.Value)
                    .DefaultIfEmpty(bottoms.Values.Max()).Max();
                foreach (var (x, y) in piece)
                {
                    int ground = wide ? bottoms[x] : pivot;
                    // le pixel (x, y) couvre, sur le sol, un petit parallélogramme : on remplit le rectangle qui l'entoure
                    float up = ground + 1 - y, down = ground - y;
                    float x0 = x - Lean * up, x1 = x + 1 - Lean * down;
                    float y0 = ground + 1 - Length * up, y1 = ground + 1 - Length * down;
                    for (int sy = (int)MathF.Floor(Math.Min(y0, y1)); sy < (int)MathF.Ceiling(Math.Max(y0, y1)); sy++)
                    for (int sx = (int)MathF.Floor(Math.Min(x0, x1)); sx < (int)MathF.Ceiling(Math.Max(x0, x1)); sx++)
                    {
                        int px = sx + padSide, py = sy + padTop;
                        if (px >= 0 && py >= 0 && px < sw && py < sh)
                            mask[px, py] = true;
                    }
                }
            }
        }
        mask = Grow(Grow(mask, Smoothing, true), Smoothing, false); // grossir puis amincir : les trous et les creux se bouchent
        var shadow = Image.CreateEmpty(sw, sh, false, Image.Format.Rgba8);
        for (int y = 0; y < sh; y++)
        for (int x = 0; x < sw; x++)
            if (mask[x, y])
                shadow.SetPixel(x, y, Colors.Black);
        var result = (ImageTexture.CreateFromImage(shadow), padSide, padTop);
        CastImages[texture] = result;
        return result;
    }

    /// <summary>Épaisseur (en pixels d'art) sous laquelle une partie d'un grand objet est fine (un chemin, des rails, un mât).</summary>
    private const int ThinPart = 3;

    /// <summary>Le cœur d'un morceau : ses pixels sans les parties fines (amincir puis regrossir de <see cref="ThinPart"/>).</summary>
    private static List<(int X, int Y)> Core(List<(int X, int Y)> piece, int minX, int minY, int maxX, int maxY)
    {
        var mask = new bool[maxX - minX + 1, maxY - minY + 1];
        foreach (var (x, y) in piece)
            mask[x - minX, y - minY] = true;
        var opened = Grow(Grow(mask, ThinPart, false), ThinPart, true);
        return piece.Where(p => opened[p.X - minX, p.Y - minY]).ToList();
    }

    /// <summary>
    /// Grossit (<paramref name="grow"/>) ou amincit une tache de <paramref name="radius"/> pixels, dans un disque.
    /// </summary>
    private static bool[,] Grow(bool[,] mask, int radius, bool grow)
    {
        int w = mask.GetLength(0), h = mask.GetLength(1);
        var result = new bool[w, h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // grossir : vrai si un voisin du disque est vrai ; amincir : vrai si tous le sont (le bord compte comme vide)
            bool value = !grow;
            for (int dy = -radius; dy <= radius && value != grow; dy++)
            for (int dx = -radius; dx <= radius && value != grow; dx++)
            {
                if (dx * dx + dy * dy > radius * radius + radius)
                    continue;
                int nx = x + dx, ny = y + dy;
                bool inside = nx >= 0 && ny >= 0 && nx < w && ny < h && mask[nx, ny];
                if (inside == grow)
                    value = grow;
            }
            result[x, y] = value;
        }
        return result;
    }

    /// <summary>Les morceaux d'une image : les groupes de pixels opaques qui se touchent (par un côté ou un coin).</summary>
    private static List<List<(int X, int Y)>> Pieces(Image image)
    {
        int w = image.GetWidth(), h = image.GetHeight();
        var seen = new bool[w, h];
        var pieces = new List<List<(int X, int Y)>>();
        for (int sy = 0; sy < h; sy++)
        for (int sx = 0; sx < w; sx++)
        {
            if (seen[sx, sy] || image.GetPixel(sx, sy).A <= 0.5f)
                continue;
            var piece = new List<(int X, int Y)>();
            var todo = new Stack<(int X, int Y)>();
            todo.Push((sx, sy));
            seen[sx, sy] = true;
            while (todo.Count > 0)
            {
                var (x, y) = todo.Pop();
                piece.Add((x, y));
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < w && ny < h && !seen[nx, ny] && image.GetPixel(nx, ny).A > 0.5f)
                    {
                        seen[nx, ny] = true;
                        todo.Push((nx, ny));
                    }
                }
            }
            pieces.Add(piece);
        }
        return pieces;
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
