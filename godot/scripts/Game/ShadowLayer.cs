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
    public static float Strength { get; set; } = 0.45f;
    /// <summary>La longueur de l'ombre sur le sol, par rapport à la hauteur de l'image (1 : aussi longue qu'elle est haute).</summary>
    public static float Length { get; set; } = 0.5f;
    /// <summary>De combien l'ombre penche vers la gauche : décalage vers la gauche du haut de l'image, par rapport à sa hauteur.</summary>
    public static float Lean { get; set; } = 0.45f;
    /// <summary>L'opacité de l'ombre de contact, au pied des objets (0 : aucune).</summary>
    public static float Contact { get; set; } = 0.5f;
    /// <summary>Le flou du bord de l'ombre portée, en pixels d'art (écart d'un flou gaussien ; 0 : bord net).</summary>
    public static float Blur { get; set; } = 0.6f;

    private CanvasGroup _cast = null!;
    private CanvasGroup _contact = null!;
    private readonly List<ShadowPainter> _painters = new();
    private readonly List<ShadowPainter> _moving = new();

    public override void _Ready()
    {
        ReadOptions();
        _cast = MakeGroup(c => ForEachDecor(c, DrawCast), c => ForEachActor(c, DrawCast), TextureFilterEnum.Linear);
        _contact = MakeGroup(c => ForEachDecor(c, DrawContact), c => ForEachActor(c, DrawContact), TextureFilterEnum.Nearest);
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
        // les ombres de contact sont blanches, teintes image par image (couleur et opacité de pieds.json) : le groupe
        // ne règle que la force d'ensemble
        _contact.SelfModulate = new Color(1, 1, 1, Contact);
        CastImages.Clear(); // la forme des ombres a peut-être changé
        ContactImages.Clear();
        foreach (var painter in _painters)
            painter.QueueRedraw();
    }

    /// <summary>
    /// Développement : « --shadow=force,longueur,penche,contact,flou » (par exemple --shadow=0.45,0.5,0.45,0.5,0.6) pour les captures.
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
        Blur = Read(4, Blur);
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
    private static void DrawCast(CanvasItem ci, Texture2D image, Rect2 rect) =>
        DrawMask(ci, image, rect, CastImages, CastImage);

    /// <summary>
    /// L'ombre de contact d'une image dessinée ailleurs que sur la carte (le décor du champ de bataille) : la même tache que
    /// sur la carte, d'après les pieds de l'image (godot/art/pieds.json), teinte en noir à l'opacité <paramref name="opacity"/> (sinon <see cref="Contact"/>).
    /// Dessinée directement (sans groupe) : deux ombres qui se chevauchent foncent un peu.
    /// </summary>
    public static void DrawContactShadow(CanvasItem ci, Texture2D image, Rect2 rect, float? opacity = null)
    {
        if (!ContactImages.TryGetValue(image, out var shadow))
            ContactImages[image] = shadow = ContactImage(image);
        float scale = rect.Size.Y / image.GetHeight();
        var at = rect.Position - new Vector2(shadow.PadLeft, shadow.PadTop) * scale;
        var tint = ContactTint(image);
        ci.DrawTextureRect(shadow.Texture, new Rect2(at, shadow.Texture.GetSize() * scale), false, new Color(tint, tint.A * (opacity ?? Contact)));
    }

    /// <summary>L'ombre de contact d'une image dessinée en <paramref name="rect"/> : une tache sombre et floue autour de son pied.</summary>
    private static void DrawContact(CanvasItem ci, Texture2D image, Rect2 rect) =>
        DrawMask(ci, image, rect, ContactImages, ContactImage, ContactTint(image));

    /// <summary>Dessine une image d'ombre (calculée une fois par image et par réglage) à la place de l'image <paramref name="image"/>.</summary>
    private static void DrawMask(CanvasItem ci, Texture2D image, Rect2 rect, Dictionary<Texture2D, ShadowImage> cache,
        Func<Texture2D, ShadowImage> make, Color? tint = null)
    {
        if (!cache.TryGetValue(image, out var shadow))
            cache[image] = shadow = make(image);
        float scale = rect.Size.Y / image.GetHeight();
        var at = rect.Position - new Vector2(shadow.PadLeft, shadow.PadTop) * scale;
        ci.DrawTextureRect(shadow.Texture, new Rect2(at, shadow.Texture.GetSize() * scale), false, tint ?? Colors.White);
    }

    /// <summary>Une image d'ombre, et la marge ajoutée à gauche et en haut de l'image de l'objet.</summary>
    private readonly record struct ShadowImage(Texture2D Texture, int PadLeft, int PadTop);

    private static readonly Dictionary<Texture2D, ShadowImage> CastImages = new();
    private static readonly Dictionary<Texture2D, ShadowImage> ContactImages = new();

    /// <summary>
    /// L'ombre portée d'une image, en pixels d'art. Chaque morceau de l'image (voir <see cref="PartsOf"/>) se couche sur
    /// le sol : un pixel à la hauteur h au-dessus de son sol va à h × penche vers la gauche et h × longueur vers le haut.
    /// Le bord est flou (<see cref="Blur"/>).
    /// </summary>
    private static ShadowImage CastImage(Texture2D texture)
    {
        int h = texture.GetHeight();
        var canvas = new Canvas(texture.GetWidth(), h, (int)MathF.Ceiling(Math.Abs(Lean) * h),
            (int)MathF.Ceiling(Math.Max(0, Length - 1) * h), (int)MathF.Ceiling(Length * h), Blur);
        foreach (var part in PartsOf(texture))
            foreach (var (x, y) in part.Pixels)
            {
                int ground = part.GroundAt(x);
                if (part.Wide && y > ground)
                    continue; // sous le sol du grand objet : posé à plat (le bout du chemin)
                // le pixel (x, y) couvre, sur le sol, un petit parallélogramme : on remplit le rectangle qui l'entoure
                float up = ground + 1 - y, down = ground - y;
                canvas.Fill(x - Lean * up, x + 1 - Lean * down, ground + 1 - Length * up, ground + 1 - Length * down, 1);
            }
        return canvas.ToImage(Blur);
    }

    /// <summary>
    /// Les bagues de l'ombre de contact, de la plus proche du pied à la plus éloignée (part de noir, avant
    /// <see cref="Contact"/>) : de 55 % à presque rien, en <paramref name="spread"/> pas (l'étalement de l'image, 3 par défaut).
    /// </summary>
    private static float[] ContactShades(int spread) =>
        Enumerable.Range(0, spread).Select(i => 0.55f * MathF.Pow((float)(spread - i) / spread, 1.4f)).ToArray();

    private const int DefaultSpread = 3;
    /// <summary>La hauteur de l'arc qui ferme la forme du pied, par rapport à sa largeur, et au plus (en pixels d'art).</summary>
    private const float ArcRatio = 0.12f, ArcMax = 2.5f;
    /// <summary>Jusqu'où (en pixels d'art, au-dessus et au-dessous du segment du pied) on cherche le bas du dessin.</summary>
    private const int FootSearch = 8;

    /// <summary>
    /// L'ombre de contact d'une image, en pixels d'art, d'après ses pieds (godot/art/pieds.json, revus par l'utilisateur ;
    /// tools/art/pieds.py) : pour chaque pied, du pixel le plus à gauche au plus à droite de la base, on suit le bas du
    /// dessin, on ferme la forme par un arc au-dessus, on la remplit, puis on l'entoure de bagues de plus en plus claires.
    /// La page de revue (revue-pieds.html) calcule le même aperçu.
    /// </summary>
    private static ShadowImage ContactImage(Texture2D texture)
    {
        var (feet, spread) = FeetOf(texture);
        var shades = ContactShades(spread);
        int w = texture.GetWidth(), h = texture.GetHeight(), rings = shades.Length;
        var canvas = new Canvas(w, h, rings + 1, rings + 1, rings + 1, 0);
        if (feet.Count == 0 || texture.GetImage() is not { } image || image.IsEmpty())
            return canvas.ToImage(0, white: true);
        if (image.IsCompressed())
            image.Decompress();
        var opaque = WithoutPath(image);
        // la forme des pieds, dans une grille avec une marge pour les bagues
        int m = rings + 1, gw = w + 2 * m, gh = h + 2 * m;
        var shape = new bool[gw, gh];
        foreach (var ((xl, yl), (xr, yr)) in feet)
        {
            int a = Math.Min(xl, xr), b = Math.Max(xl, xr);
            float ya = xl <= xr ? yl : yr, yb = xl <= xr ? yr : yl;
            int width = b - a + 1;
            float arc = Math.Min(ArcMax, Math.Max(1, ArcRatio * width));
            for (int x = a; x <= b; x++)
            {
                float t = (x + 0.5f - a) / width;
                float line = ya + (yb - ya) * (b > a ? (float)(x - a) / (b - a) : 0);
                // le bas du dessin dans cette colonne, près du segment (sinon le segment : un creux enjambé)
                float bottom = line;
                if (x >= 0 && x < w)
                    for (int y = Math.Min(h - 1, (int)MathF.Round(line) + FootSearch); y >= Math.Max(0, (int)MathF.Round(line) - FootSearch); y--)
                        if (opaque[x, y])
                        {
                            bottom = y;
                            break;
                        }
                float top = line - arc * MathF.Sqrt(Math.Max(0, 1 - (2 * t - 1) * (2 * t - 1)));
                for (int y = (int)MathF.Floor(Math.Min(top, bottom)); y <= (int)MathF.Round(bottom); y++)
                    if (x + m >= 0 && y + m >= 0 && x + m < gw && y + m < gh)
                        shape[x + m, y + m] = true;
            }
        }
        // les bagues : on grossit la forme d'un pixel (en croix) à chaque fois, de plus en plus clair
        var current = GrowCross(shape);
        foreach (float shade in shades)
        {
            for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
                if (current[x, y])
                    canvas.Fill(x - m, x - m + 1, y - m, y - m + 1, shade);
            current = GrowCross(current);
        }
        return canvas.ToImage(0, white: true);
    }

    private static bool[,] GrowCross(bool[,] mask)
    {
        int w = mask.GetLength(0), h = mask.GetLength(1);
        var result = new bool[w, h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            result[x, y] = mask[x, y] || (y > 0 && mask[x, y - 1]) || (y < h - 1 && mask[x, y + 1])
                || (x > 0 && mask[x - 1, y]) || (x < w - 1 && mask[x + 1, y]);
        return result;
    }

    /// <summary>
    /// Les pixels opaques d'une image, sans le chemin étroit (<see cref="PathWidth"/> pixels au plus) qui part du bas de
    /// l'image vers la case d'entrée d'une mine : il est posé à plat.
    /// </summary>
    private static bool[,] WithoutPath(Image image)
    {
        int w = image.GetWidth(), h = image.GetHeight();
        var m = new bool[w, h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            m[x, y] = image.GetPixel(x, y).A > 0.5f;
        for (int x0 = 0; x0 < w; x0++)
        {
            if (!m[x0, h - 1] || (x0 > 0 && m[x0 - 1, h - 1]))
                continue;
            int x1 = x0;
            while (x1 + 1 < w && m[x1 + 1, h - 1])
                x1++;
            int lo = x0, hi = x1;
            for (int y = h - 1; y >= 0; y--)
            {
                var row = Enumerable.Range(Math.Max(0, lo - 2), Math.Min(w, hi + 3) - Math.Max(0, lo - 2)).Where(x => m[x, y]).ToList();
                if (row.Count == 0 || row.Max() - row.Min() + 1 > PathWidth)
                    break;
                foreach (int x in row)
                    m[x, y] = false;
                lo = row.Min();
                hi = row.Max();
            }
        }
        return m;
    }

    private static Dictionary<string, (List<((int X, int Y) Left, (int X, int Y) Right)> Feet, int Spread)>? _feet;
    private static readonly Dictionary<string, Color> Tints = new();

    /// <summary>
    /// La teinte de l'ombre de contact d'une image : sa couleur, et dans l'alpha un facteur de son opacité (pieds.json :
    /// « couleur », « opacite » ; noire, × 1 sinon). Le facteur peut dépasser 1.
    /// </summary>
    public static Color ContactTint(Texture2D texture)
    {
        FeetOf(texture); // lit pieds.json la première fois
        return Art.NameOf(texture) is { } name && Tints.TryGetValue(name, out var tint) ? tint : new Color(0, 0, 0, 1);
    }

    /// <summary>Les pieds d'une image et son étalement, d'après godot/art/pieds.json (aucun pied si l'image n'y est pas).</summary>
    private static (List<((int X, int Y) Left, (int X, int Y) Right)> Feet, int Spread) FeetOf(Texture2D texture)
    {
        if (_feet == null)
        {
            _feet = new();
            const string path = Art.Root + "pieds.json";
            if (Godot.FileAccess.FileExists(path))
                using (var doc = System.Text.Json.JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path)))
                    foreach (var entry in doc.RootElement.EnumerateObject())
                    {
                        var list = new List<((int, int), (int, int))>();
                        foreach (var foot in entry.Value.GetProperty("pieds").EnumerateArray())
                        {
                            var l = foot[0];
                            var r = foot[1];
                            list.Add(((l[0].GetInt32(), l[1].GetInt32()), (r[0].GetInt32(), r[1].GetInt32())));
                        }
                        int spread = entry.Value.TryGetProperty("etalement", out var e) ? Math.Clamp(e.GetInt32(), 1, 6) : DefaultSpread;
                        _feet[entry.Name] = (list, spread);
                        // la couleur et l'opacité de l'ombre de contact (revues sur la page des pieds), sinon noire, × 1
                        float opacity = entry.Value.TryGetProperty("opacite", out var o) ? o.GetSingle() : 1;
                        var color = entry.Value.TryGetProperty("couleur", out var c) && c.GetString() is { } hex ? Color.FromHtml(hex) : Colors.Black;
                        Tints[entry.Name] = new Color(color, opacity);
                    }
        }
        return Art.NameOf(texture) is { } name && _feet.TryGetValue(name, out var feet) ? feet : (new(), DefaultSpread);
    }

    /// <summary>
    /// Une image d'ombre en cours de dessin : l'image de l'objet avec des marges autour (pour ce qui déborde et pour le
    /// flou), en pixels d'art.
    /// </summary>
    private sealed class Canvas
    {
        private readonly float[,] _alpha;
        private readonly int _left, _top;

        public Canvas(int width, int height, int side, int top, int bottom, float blur)
        {
            int margin = 3 * (int)MathF.Ceiling(blur) + 1;
            _left = side + margin;
            _top = top + margin;
            _alpha = new float[width + 2 * _left, height + _top + bottom + margin];
        }

        /// <summary>Remplit le rectangle de x0 à x1 et de y0 à y1 (dans l'ordre qu'on veut, en pixels de l'image de l'objet).</summary>
        public void Fill(float x0, float x1, float y0, float y1, float value)
        {
            for (int y = (int)MathF.Floor(Math.Min(y0, y1)); y < (int)MathF.Ceiling(Math.Max(y0, y1)); y++)
            for (int x = (int)MathF.Floor(Math.Min(x0, x1)); x < (int)MathF.Ceiling(Math.Max(x0, x1)); x++)
            {
                int px = x + _left, py = y + _top;
                if (px >= 0 && py >= 0 && px < _alpha.GetLength(0) && py < _alpha.GetLength(1))
                    _alpha[px, py] = Math.Max(_alpha[px, py], value);
            }
        }

        /// <summary>
        /// L'image finie, floutée de <paramref name="blur"/> pixels (un flou gaussien ; 0 : bord net) : noire, ou blanche
        /// (<paramref name="white"/> : une ombre de contact, teinte au dessin).
        /// </summary>
        public ShadowImage ToImage(float blur, bool white = false)
        {
            var alpha = blur > 0 ? GaussianBlur(_alpha, blur) : _alpha;
            int w = alpha.GetLength(0), h = alpha.GetLength(1);
            var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (alpha[x, y] > 0.004f)
                    image.SetPixel(x, y, new Color(white ? 1 : 0, white ? 1 : 0, white ? 1 : 0, Math.Min(1, alpha[x, y])));
            return new ShadowImage(ImageTexture.CreateFromImage(image), _left, _top);
        }
    }

    /// <summary>Un flou gaussien d'écart <paramref name="sigma"/> (en pixels), d'abord en ligne, puis en colonne.</summary>
    private static float[,] GaussianBlur(float[,] values, float sigma)
    {
        int radius = (int)MathF.Ceiling(2.5f * sigma);
        var kernel = new float[2 * radius + 1];
        for (int d = -radius; d <= radius; d++)
            kernel[d + radius] = MathF.Exp(-d * d / (2 * sigma * sigma));
        float total = kernel.Sum();
        for (int k = 0; k < kernel.Length; k++)
            kernel[k] /= total;
        int w = values.GetLength(0), h = values.GetLength(1);
        var rows = new float[w, h];
        var result = new float[w, h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float sum = 0;
            for (int d = -radius; d <= radius; d++)
                if (x + d >= 0 && x + d < w)
                    sum += values[x + d, y] * kernel[d + radius];
            rows[x, y] = sum;
        }
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float sum = 0;
            for (int d = -radius; d <= radius; d++)
                if (y + d >= 0 && y + d < h)
                    sum += rows[x, y + d] * kernel[d + radius];
            result[x, y] = sum;
        }
        return result;
    }

    // ------------------------------------------------------------------ morceaux

    /// <summary>Largeur (en pixels d'art) au-delà de laquelle un morceau d'image est un grand objet vu de trois quarts.</summary>
    private const int WidePiece = 40;

    /// <summary>
    /// Un morceau plus petit (en pixels) ou plus creux (part de son rectangle couverte) ne fait pas d'ombre : un point
    /// isolé, des rails, un détail posé à plat sur le sol.
    /// </summary>
    private const int TinyPiece = 12;
    private const float HollowPiece = 0.3f;

    /// <summary>Un morceau étroit (en pixels d'art) qui touche le bas de l'image est un chemin vers l'entrée : pas d'ombre.</summary>
    private const int PathWidth = 8;

    /// <summary>Épaisseur (en pixels d'art) sous laquelle une partie d'un grand objet est fine (un chemin, des rails, un mât).</summary>
    private const int ThinPart = 3;

    /// <summary>
    /// Un morceau d'image qui fait une ombre, et son sol. Un morceau étroit (un arbre, un rocher, une créature) tourne
    /// autour de son pied : son pixel le plus bas au milieu du morceau. Un morceau large (les remparts d'une ville) est vu
    /// de trois quarts : le bas d'une tour du fond est plus haut dans l'image que le bas du mur de devant ; chacune de ses
    /// colonnes se tient alors sur son propre sol.
    /// </summary>
    private sealed record Part(List<(int X, int Y)> Pixels, bool Wide, Dictionary<int, int> Ground, int Pivot)
    {
        private readonly int[] _columns = Ground.Keys.ToArray();

        /// <summary>Le sol sous la colonne <paramref name="x"/> (celui de la colonne la plus proche si elle n'en a pas).</summary>
        public int GroundAt(int x) =>
            !Wide ? Pivot : Ground.TryGetValue(x, out int g) ? g : Ground[_columns.MinBy(c => Math.Abs(c - x))];
    }

    private static readonly Dictionary<Texture2D, List<Part>> Parts = new();

    /// <summary>
    /// Les morceaux d'une image qui font une ombre (calculés une fois par image) : les pixels opaques qui se touchent,
    /// sans les morceaux minuscules ou creux (rails) ni les chemins étroits qui touchent le bas de l'image.
    /// </summary>
    private static List<Part> PartsOf(Texture2D texture)
    {
        if (Parts.TryGetValue(texture, out var cached))
            return cached;
        var parts = new List<Part>();
        if (texture.GetImage() is { } image && !image.IsEmpty())
        {
            if (image.IsCompressed())
                image.Decompress();
            int h = image.GetHeight();
            foreach (var piece in Pieces(image))
            {
                int minX = piece.Min(p => p.X), maxX = piece.Max(p => p.X);
                int minY = piece.Min(p => p.Y), maxY = piece.Max(p => p.Y);
                if (piece.Count < TinyPiece || piece.Count < HollowPiece * (maxX - minX + 1) * (maxY - minY + 1))
                    continue;
                if (maxY == h - 1 && maxX - minX + 1 <= PathWidth)
                    continue; // un chemin qui part de l'entrée vers la case de devant : posé à plat
                bool wide = maxX - minX + 1 > WidePiece;
                // le sol de chaque colonne : son pixel le plus bas ; pour un grand objet, sans ses parties fines (chemin
                // vers l'entrée, rails, mâts), sinon le sol de ces colonnes descendrait jusqu'au bout du chemin
                var ground = (wide ? Core(piece, minX, minY, maxX, maxY) : piece)
                    .GroupBy(p => p.X).ToDictionary(g => g.Key, g => g.Max(p => p.Y));
                if (ground.Count == 0)
                    continue;
                // le pied d'un morceau étroit : le plus bas du tiers du milieu
                int third = (maxX - minX + 1) / 3;
                int pivot = ground.Where(b => b.Key >= minX + third && b.Key <= maxX - third).Select(b => b.Value)
                    .DefaultIfEmpty(ground.Values.Max()).Max();
                parts.Add(new Part(piece, wide, ground, pivot));
            }
        }
        Parts[texture] = parts;
        return parts;
    }

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
}
