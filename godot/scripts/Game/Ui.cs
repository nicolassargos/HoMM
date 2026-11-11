using System;
using System.Collections.Generic;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>Couleurs et petits outils communs à toute l'interface (panneau, écran de ville…).</summary>
public static class Ui
{
    public static readonly Color PanelColor = MapRenderer.Rgb(40, 32, 24);
    public static readonly Color DarkPanelColor = MapRenderer.Rgb(28, 22, 16);
    public static readonly Color BorderColor = MapRenderer.Rgb(190, 160, 90);
    public static readonly Color TextColor = MapRenderer.Rgb(240, 228, 200);
    public static readonly Color SoftTextColor = MapRenderer.Rgb(180, 168, 140);
    public static readonly Color GoodColor = MapRenderer.Rgb(90, 190, 90);
    public static readonly Color DimColor = MapRenderer.Rgb(110, 100, 85);

    /// <summary>L'argent du cadre et des boutons, pour les traits des portraits, de la carte réduite, des barres.</summary>
    public static readonly Color SilverColor = MapRenderer.Rgb(168, 172, 178);

    /// <summary>Le vert du mouvement, sourd (un vert vif criait plus fort que tout le reste).</summary>
    public static readonly Color MovementColor = MapRenderer.Rgb(72, 136, 60);

    public const string FontPath = "res://fonts/VT323-Regular.ttf";

    /// <summary>
    /// L'échelle de l'interface (1920 × 1080) : les écrans sont décrits dans les unités de l'ancien écran (1280 × 720) et
    /// <see cref="Px"/>, <see cref="V"/>, <see cref="At{T}"/> et <see cref="Box"/> les mettent à l'échelle, arrondies à
    /// la grille des pixels d'art (<see cref="Grid"/>). Les images en pixel art sont toujours agrandies ×2.
    /// </summary>
    public const float Scale = 1.5f;

    /// <summary>Agrandissement des cadres et des boutons (leurs images) : un nombre entier, pour des pixels réguliers.</summary>
    public const int ArtScale = 2;

    /// <summary>
    /// La grille de l'interface : un pixel d'art fait <see cref="Grid"/> pixels de l'écran. Toutes les longueurs (places,
    /// tailles, marges, bords) en sont des multiples, pour que les cadres, les boutons et les images tombent sur les mêmes
    /// pixels (comme Heroes 1 et 2, dessinés sur une seule grille).
    /// </summary>
    public const int Grid = ArtScale;

    /// <summary>Une longueur de l'écran arrondie à la grille des pixels d'art.</summary>
    public static float Snap(float pixels) => MathF.Round(pixels / Grid) * Grid;

    /// <summary>Une longueur de l'écran arrondie à la grille, vers le bas (pour centrer sans déborder).</summary>
    public static float SnapDown(float pixels) => MathF.Floor(pixels / Grid) * Grid;

    /// <summary>Une longueur comptée en pixels d'art, en pixels de l'écran.</summary>
    public static float ArtPx(float artPixels) => artPixels * Grid;

    /// <summary>Une longueur de l'interface, mise à l'échelle et arrondie à la grille.</summary>
    public static float Px(float value) => Snap(value * Scale);

    /// <summary>Une position ou une taille de l'interface, mise à l'échelle.</summary>
    public static Vector2 V(float x, float y) => new(Px(x), Px(y));

    /// <summary>La taille de l'écran dans les unités de l'interface (1280 × 720 pour un écran de 1920 × 1080).</summary>
    public static Vector2 Units(Vector2 screen) => screen / Scale;

    // ------------------------------------------------------------ les tailles communes (en pixels de l'écran)
    // Chaque bouton, cadre et texte prend l'une de ces tailles : les écrans ne s'en inventent pas d'autres.

    /// <summary>Hauteur d'un petit bouton (flèches d'une liste, « Max »).</summary>
    public static readonly float SmallButtonHeight = ArtPx(16);

    /// <summary>Hauteur d'un bouton ordinaire (Fermer, Recruter, onglets de la ville…).</summary>
    public static readonly float ButtonHeight = ArtPx(22);

    /// <summary>Hauteur d'un grand bouton (Fin du tour, Commencer, le menu principal, les choix d'une fenêtre).</summary>
    public static readonly float LargeButtonHeight = ArtPx(30);

    /// <summary>Côté d'un bouton carré à icône (l'icône de 32 pixels d'art ×2, et 4 pixels d'art de pierre autour).</summary>
    public static readonly float IconButtonSide = ArtPx(40);

    /// <summary>Largeur du bouton « Fermer » des grandes fenêtres, toujours en haut à droite.</summary>
    public static readonly float CloseButtonWidth = ArtPx(64);

    /// <summary>Marge entre le bord d'une fenêtre et son contenu, et écart entre deux boutons voisins.</summary>
    public static readonly float WindowMargin = ArtPx(12), ButtonGap = ArtPx(4);

    /// <summary>
    /// Les tailles du texte : la police VT323 est dessinée sur une grille de 12,5 pixels par taille, elle n'est donc nette
    /// qu'à 25 (un pixel de la police = un pixel d'art, comme Heroes 1 et 2), 50 (les titres) et 75 (le nom du jeu).
    /// </summary>
    public const int NormalText = 25, TitleText = 50, BigText = 75;

    private static Font? _sharpFont;

    /// <summary>
    /// La taille réelle d'un texte prévu à la taille <paramref name="size"/> (les tailles de l'ancienne police) : le texte
    /// ordinaire (moins de 18), les titres et les grands boutons (18 à 39), le nom du jeu (40 et plus). Sans la police
    /// pixel, l'ancienne taille.
    /// </summary>
    public static int TextSize(int size) => _sharpFont == null ? (int)MathF.Round(size * Scale)
        : size >= 40 ? BigText : size >= 18 ? TitleText : NormalText;

    /// <summary>
    /// La police pixel (VT323, licence OFL) devient la police de toute l'interface et des textes de la carte, nette (sans
    /// lissage) aux tailles <see cref="NormalText"/>, <see cref="TitleText"/> et <see cref="BigText"/>.
    /// Lue directement depuis le fichier, sans import par l'éditeur. Sans le fichier, la police de Godot reste.
    /// </summary>
    public static void UsePixelFont()
    {
        if (!Godot.FileAccess.FileExists(FontPath))
            return;
        _sharpFont = new FontFile
        {
            Data = Godot.FileAccess.GetFileAsBytes(FontPath),
            Antialiasing = TextServer.FontAntialiasing.None,
            Hinting = TextServer.Hinting.Normal, // calé sur la grille de pixels
            SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
        };
        // le thème par défaut sert à tous les contrôles, même ceux des CanvasLayer (qui n'héritent pas du thème de la fenêtre)
        var theme = ThemeDB.GetDefaultTheme();
        theme.DefaultFont = _sharpFont;
        theme.DefaultFontSize = NormalText;
        ThemeDB.FallbackFont = theme.DefaultFont; // pour les textes dessinés sur la carte
    }

    /// <summary>Donne à un contrôle la taille réelle de son texte.</summary>
    public static void SetFontSize(Control control, int fontSize) =>
        control.AddThemeFontSizeOverride("font_size", TextSize(fontSize));

    /// <summary>Place un contrôle à (x, y), en unités de l'interface (mises à l'échelle).</summary>
    public static T At<T>(T control, float x, float y) where T : Control
    {
        control.Position = V(x, y);
        return control;
    }

    public static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeColorOverride("font_color", color);
        SetFontSize(label, fontSize);
        return label;
    }

    public static StyleBoxFlat Box(Color background, Color? border,
        int left = 0, int top = 0, int right = 0, int bottom = 0, int radius = 0)
    {
        var box = new StyleBoxFlat { BgColor = background };
        // les bords à l'échelle de l'interface, en pixels d'art entiers (un bord d'un pixel en fait deux)
        static int Width(int w) => w == 0 ? 0 : Math.Max(Grid, (int)Px(w));
        if (border is { } b)
        {
            box.BorderColor = b;
            box.BorderWidthLeft = Width(left);
            box.BorderWidthTop = Width(top);
            box.BorderWidthRight = Width(right);
            box.BorderWidthBottom = Width(bottom);
        }
        box.SetCornerRadiusAll(Width(radius));
        return box;
    }

    // ------------------------------------------------------------ cadres en pixel art
    // Les cadres (art/ui/panel.png) et boutons (art/ui/button.png) sont découpés en 9 morceaux : les coins
    // restent tels quels, les bords et le centre sont répétés (jamais étirés), à l'échelle 1, pixels nets.

    /// <summary>
    /// Taille des coins du cadre et du bouton dans leurs images, et marges du texte dans un cadre (en pixels). Une image
    /// d'une autre taille vient avec « art/ui/ui.json » : { "panelCorner": 28, "panelContentX": 20, "panelContentY": 14,
    /// "buttonCorner": 10 } (chaque valeur est facultative).
    /// </summary>
    private static int FrameCorner = 12, FrameContentX = 14, FrameContentY = 6, ButtonCorner = 6;

    /// <summary>Agrandissement du cadre à l'écran (« panelScale » dans ui.json ; par défaut celui des images).</summary>
    private static int FrameScale = ArtScale;

    /// <summary>Agrandissement des boutons à l'écran (« buttonScale » dans ui.json ; par défaut celui des images).</summary>
    private static int ButtonScale = ArtScale;

    /// <summary>Ne répéter que la bande sans détail des bords du bouton (vrai pour l'ancien bouton à clous).</summary>
    private static bool ButtonPlainBand = true;

    private static void LoadSlices()
    {
        const string path = "res://art/ui/ui.json";
        if (!Godot.FileAccess.FileExists(path))
            return;
        if (Json.ParseString(Godot.FileAccess.GetFileAsString(path)).AsGodotDictionary() is not { } values)
            return;
        int Get(string key, int fallback) => values.TryGetValue(key, out var v) ? v.AsInt32() : fallback;
        FrameCorner = Get("panelCorner", FrameCorner);
        FrameContentX = Get("panelContentX", FrameContentX);
        FrameContentY = Get("panelContentY", FrameContentY);
        ButtonCorner = Get("buttonCorner", ButtonCorner);
        FrameScale = Get("panelScale", FrameScale);
        ButtonScale = Get("buttonScale", ButtonScale);
        ButtonPlainBand = Get("buttonPlainBand", ButtonPlainBand ? 1 : 0) != 0;
        if (values.TryGetValue("panelTexture", out var texture))
            PanelTexture = texture.AsString();
        if (values.TryGetValue("panelTextureStrength", out var strength))
            PanelTextureStrength = (float)strength.AsDouble();
        if (values.TryGetValue("buttonTextureStrength", out var buttonStrength))
            ButtonTextureStrength = (float)buttonStrength.AsDouble();
    }

    private static readonly Dictionary<Color, StyleBox> Frames = new();
    private static Image? _frameImage, _buttonImage;
    private static bool _imagesLoaded;

    private static void LoadImages()
    {
        if (_imagesLoaded)
            return;
        _imagesLoaded = true;
        LoadSlices();
        _frameImage = Art.GetImage("ui/panel") is { } frame
            ? Enlarged(WithLargeMiddle(WithoutRepeatedDetails(frame, FrameCorner), FrameCorner), FrameScale) : null;
        // un bouton garde tout son milieu (« buttonPlainBand » : seulement la bande sans détail, pour des bords à clous) :
        // une pierre texturée répétée par petites bandes laissait des raccords visibles
        _buttonImage = Art.GetImage("ui/button") is { } button
            ? Enlarged(ButtonPlainBand ? WithoutRepeatedDetails(button, ButtonCorner) : StoneGrained(WithLargeMiddle(Trimmed(button), ButtonCorner), ButtonCorner), ButtonScale) : null;
    }

    /// <summary>L'image réduite à sa partie non transparente.</summary>
    private static Image Trimmed(Image source)
    {
        var image = source.GetRegion(source.GetUsedRect());
        image.Convert(Image.Format.Rgba8);
        return image;
    }

    /// <summary>L'image agrandie <see cref="ArtScale"/> fois, pixels nets (chaque pixel devient un carré).</summary>
    private static Image Enlarged(Image image, int scale = ArtScale)
    {
        var copy = (Image)image.Duplicate();
        copy.Resize(image.GetWidth() * scale, image.GetHeight() * scale, Image.Interpolation.Nearest);
        return copy;
    }

    /// <summary>
    /// Un cadre de bois et d'or rempli de <paramref name="fill"/> (le centre de l'image est transparent).
    /// Sans image, une boîte unie bordée d'or.
    /// </summary>
    public static StyleBox Frame(Color fill)
    {
        if (Frames.TryGetValue(fill, out var cached))
            return cached;
        LoadImages();
        StyleBox box = _frameImage == null
            ? Box(fill, BorderColor, 2, 2, 2, 2)
            : Sliced(FillInside(_frameImage, fill, FrameCorner), FrameCorner * FrameScale, (int)Px(FrameContentX), (int)Px(FrameContentY));
        Frames[fill] = box;
        return box;
    }

    /// <summary>Le fond d'un bouton, teinté par <paramref name="tint"/> (survol, appui, désactivé).</summary>
    /// <summary>La pierre d'un bouton (A1), teintée : <see cref="StoneButton"/> pour les états ordinaires.</summary>
    public static StyleBox StoneButton(float light = 1f) =>
        ButtonBox(new Color(light, light * 0.97f, light * 0.92f), MapRenderer.Rgb(90, 66, 30), BorderColor);

    private static StyleBox ButtonBox(Color tint, Color flatColor, Color border)
    {
        LoadImages();
        if (_buttonImage == null)
            return Box(flatColor, border, 2, 2, 2, 2, radius: 6);
        var box = Sliced(_buttonImage, ButtonCorner * ButtonScale, (int)Px(ButtonCorner + 8), (int)Px(5));
        box.ModulateColor = tint;
        return box;
    }

    /// <summary>
    /// Les bords d'un cadre ont des détails (clous dorés…) qui, répétés, se verraient partout. On ne garde donc
    /// entre les coins que la plus longue bande de bord sans détail, en largeur comme en hauteur : c'est elle qui
    /// sera répétée. L'image est d'abord réduite à sa partie non transparente.
    /// </summary>
    private static Image WithoutRepeatedDetails(Image source, int corner)
    {
        var image = source.GetRegion(source.GetUsedRect());
        image.Convert(Image.Format.Rgba8);
        var (left, right) = PlainRun(image, corner, vertical: false);
        image = Keep(image, corner, left, right, vertical: false);
        var (top, bottom) = PlainRun(image, corner, vertical: true);
        return Keep(image, corner, top, bottom, vertical: true);
    }

    /// <summary>
    /// La plus longue suite de colonnes (ou de rangées si <paramref name="vertical"/>) entre les coins dont le bord
    /// ressemble au bord habituel : pour chaque ligne du bord, la couleur médiane sert de référence.
    /// </summary>
    private static (int Start, int End) PlainRun(Image image, int corner, bool vertical)
    {
        int length = vertical ? image.GetHeight() : image.GetWidth();
        int depth = vertical ? image.GetWidth() : image.GetHeight();
        Color At(int along, int across) => vertical ? image.GetPixel(across, along) : image.GetPixel(along, across);
        var edge = new List<int>();
        for (int d = 0; d < corner; d++) { edge.Add(d); edge.Add(depth - 1 - d); }
        var median = new Dictionary<int, Color>();
        foreach (int d in edge)
        {
            var colors = new List<Color>();
            for (int a = corner; a < length - corner; a++)
                colors.Add(At(a, d));
            colors.Sort((x, y) => (x.R + x.G + x.B).CompareTo(y.R + y.G + y.B));
            median[d] = colors[colors.Count / 2];
        }
        bool Plain(int a)
        {
            foreach (int d in edge)
            {
                var c = At(a, d);
                var m = median[d];
                if (Mathf.Abs(c.R - m.R) + Mathf.Abs(c.G - m.G) + Mathf.Abs(c.B - m.B) + Mathf.Abs(c.A - m.A) > 0.35f)
                    return false;
            }
            return true;
        }
        int bestStart = corner, bestEnd = corner + 1, start = -1;
        for (int a = corner; a <= length - corner; a++)
        {
            if (a < length - corner && Plain(a))
            {
                if (start < 0)
                    start = a;
                continue;
            }
            if (start >= 0 && a - start > bestEnd - bestStart)
                (bestStart, bestEnd) = (start, a);
            start = -1;
        }
        return (bestStart, bestEnd);
    }

    /// <summary>Recompose l'image avec ses deux coins et seulement la bande [start, end) entre eux.</summary>
    private static Image Keep(Image image, int corner, int start, int end, bool vertical)
    {
        int w = image.GetWidth(), h = image.GetHeight();
        int length = vertical ? h : w;
        int kept = corner + (end - start) + corner;
        var result = Image.CreateEmpty(vertical ? w : kept, vertical ? kept : h, false, Image.Format.Rgba8);
        Rect2I Span(int from, int size) => vertical ? new Rect2I(0, from, w, size) : new Rect2I(from, 0, size, h);
        Vector2I To(int at) => vertical ? new Vector2I(0, at) : new Vector2I(at, 0);
        result.BlitRect(image, Span(0, corner), To(0));
        result.BlitRect(image, Span(start, end - start), To(corner));
        result.BlitRect(image, Span(length - corner, corner), To(corner + end - start));
        return result;
    }

    /// <summary>Une image découpée en 9 morceaux, réduite à sa partie non transparente.</summary>
    private static StyleBoxTexture Sliced(Image image, int corner, int contentX, int contentY)
    {
        var box = new StyleBoxTexture
        {
            Texture = ImageTexture.CreateFromImage(image),
            RegionRect = image.GetUsedRect(),
            AxisStretchHorizontal = StyleBoxTexture.AxisStretchMode.Tile,
            AxisStretchVertical = StyleBoxTexture.AxisStretchMode.Tile,
        };
        box.SetTextureMarginAll(corner);
        box.SetContentMarginAll(contentY);
        box.ContentMarginLeft = box.ContentMarginRight = contentX;
        return box;
    }

    /// <summary>
    /// Taille du milieu du cadre (entre les coins), en pixels d'art : la bande sans détail y est répétée, pour que le
    /// grain du fond (<see cref="Grain"/>) ne revienne que toutes les <see cref="FrameMiddle"/> pixels.
    /// </summary>
    private const int FrameMiddle = 64;

    /// <summary>Le cadre avec un milieu de <see cref="FrameMiddle"/> pixels de côté (la bande du milieu répétée).</summary>
    private static Image WithLargeMiddle(Image image, int corner)
    {
        Image Stretch(Image source, bool vertical)
        {
            int w = source.GetWidth(), h = source.GetHeight();
            int band = (vertical ? h : w) - 2 * corner;
            var result = Image.CreateEmpty(vertical ? w : 2 * corner + FrameMiddle, vertical ? 2 * corner + FrameMiddle : h, false, Image.Format.Rgba8);
            Rect2I Span(int from, int size) => vertical ? new Rect2I(0, from, w, size) : new Rect2I(from, 0, size, h);
            Vector2I To(int at) => vertical ? new Vector2I(0, at) : new Vector2I(at, 0);
            result.BlitRect(source, Span(0, corner), To(0));
            for (int at = 0; at < FrameMiddle; at += band)
                result.BlitRect(source, Span(corner, Math.Min(band, FrameMiddle - at)), To(corner + at));
            result.BlitRect(source, Span(corner + band, corner), To(corner + FrameMiddle));
            return result;
        }
        return Stretch(Stretch(image, vertical: false), vertical: true);
    }

    /// <summary>
    /// Le fond un peu texturé des cadres (« panelTexture » dans ui.json) : « doux » (marbrure légère et quelques points),
    /// « pierre » (grain fin de granit), « cuir » (grandes taches douces), « toile » (tissage très léger) ou « uni ».
    /// (x, y) en pixels d'art depuis le coin du milieu ; le motif se répète tous les <see cref="FrameMiddle"/> pixels,
    /// sans raccord visible.
    /// </summary>
    private static string PanelTexture = "doux";

    /// <summary>Force du motif du fond (« panelTextureStrength » dans ui.json, 1 par défaut).</summary>
    private static float PanelTextureStrength = 1;

    /// <summary>Force du grain sur la pierre des boutons (« buttonTextureStrength » dans ui.json, aucun par défaut).</summary>
    private static float ButtonTextureStrength = 0;

    /// <summary>
    /// Le grain sur le milieu uni du bouton (sa pierre), pas sur son bord : les pixels reliés au pixel du milieu et de
    /// couleur proche. (x, y) comptés depuis le coin du milieu, comme pour les cadres.
    /// </summary>
    private static Image StoneGrained(Image image, int corner)
    {
        if (ButtonTextureStrength <= 0)
            return image;
        int w = image.GetWidth(), h = image.GetHeight();
        var reference = image.GetPixel(w / 2, h / 2);
        bool Close(Color c) => c.A > 0.5f && Mathf.Abs(c.R - reference.R) + Mathf.Abs(c.G - reference.G) + Mathf.Abs(c.B - reference.B) < 0.16f;
        var seen = new bool[w, h];
        var todo = new Stack<(int X, int Y)>();
        todo.Push((w / 2, h / 2));
        while (todo.Count > 0)
        {
            var (x, y) = todo.Pop();
            if (x < 0 || y < 0 || x >= w || y >= h || seen[x, y] || !Close(image.GetPixel(x, y)))
                continue;
            seen[x, y] = true;
            image.SetPixel(x, y, Grain(image.GetPixel(x, y), x - corner, y - corner, ButtonTextureStrength));
            todo.Push((x + 1, y)); todo.Push((x - 1, y)); todo.Push((x, y + 1)); todo.Push((x, y - 1));
        }
        return image;
    }

    private static readonly Dictionary<(Color, Color?, bool), StyleBox> Insets = new();

    /// <summary>
    /// Une boîte creusée dans la pierre (sections, listes, cartes) : un biseau d'un pixel d'art, sombre en haut et à
    /// gauche, clair en bas et à droite, puis une ombre intérieure, et le grain de pierre dans <paramref name="fill"/>.
    /// <paramref name="outline"/> (héros choisi, bâtiment possible, troupe à risque…) remplace le biseau par un trait
    /// de cette couleur. Agrandie comme le cadre.
    /// </summary>
    public static StyleBox Inset(Color fill, Color? outline = null) => StoneBox(fill, outline, sunken: true);

    /// <summary>
    /// Une plaque à plat (ce que contient une boîte creusée : lignes d'une liste, cartes de bâtiment ou de créature,
    /// vignettes du royaume) : le grain de pierre et un trait fin (<paramref name="outline"/>, sinon discret).
    /// </summary>
    public static StyleBox Plate(Color fill, Color? outline = null) => StoneBox(fill, outline ?? DimColor, sunken: false);

    private static StyleBox StoneBox(Color fill, Color? outline, bool sunken)
    {
        LoadImages(); // les réglages (grain, agrandissement) d'abord
        if (Insets.TryGetValue((fill, outline, sunken), out var cached))
            return cached;
        const int edge = 2;
        int size = 2 * edge + FrameMiddle;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        Color dark = fill.Darkened(0.5f), light = fill.Lightened(0.22f), shade = fill.Darkened(0.2f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int ring = Math.Min(Math.Min(x, y), Math.Min(size - 1 - x, size - 1 - y));
            bool topLeft = x + y < size - 1 && (x == ring || y == ring);
            Color color = ring switch
            {
                0 => outline ?? (topLeft ? dark : light),
                1 when sunken && topLeft => shade,
                _ => Grain(fill, x - edge, y - edge),
            };
            image.SetPixel(x, y, color);
        }
        var box = Sliced(Enlarged(image, FrameScale), edge * FrameScale, (int)Px(6), (int)Px(4));
        Insets[(fill, outline, sunken)] = box;
        return box;
    }

    /// <summary>Un grand fond (l'écran de ville) : seulement le grain de pierre, sans bord.</summary>
    public static StyleBox Stone(Color fill)
    {
        LoadImages();
        var image = Image.CreateEmpty(FrameMiddle, FrameMiddle, false, Image.Format.Rgba8);
        for (int y = 0; y < FrameMiddle; y++)
        for (int x = 0; x < FrameMiddle; x++)
            image.SetPixel(x, y, Grain(fill, x, y));
        return Sliced(Enlarged(image, FrameScale), 0, 0, 0);
    }

    private static Color Grain(Color fill, int x, int y) => Grain(fill, x, y, PanelTextureStrength);

    private static Color Grain(Color fill, int x, int y, float strength)
    {
        x = ((x % FrameMiddle) + FrameMiddle) % FrameMiddle;
        y = ((y % FrameMiddle) + FrameMiddle) % FrameMiddle;
        static float Hash(int a, int b)
        {
            uint h = (uint)(a * 374761393 + b * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
        // un bruit doux sur des carrés de <cell> pixels, de 0 à 1, qui se répète tous les FrameMiddle pixels
        float Smooth(int cell, int seed)
        {
            int cells = FrameMiddle / cell;
            float fx = (float)x / cell, fy = (float)y / cell;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            float Corner(int cx, int cy) => Hash(cx % cells + seed, cy % cells - seed);
            return Mathf.Lerp(Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), tx),
                              Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), tx), ty);
        }
        float speck = Hash(x + 1000, y + 1000);
        float factor = PanelTexture switch
        {
            "uni" => 1,
            "pierre" => 1 + 0.06f * (Smooth(8, 7) - 0.5f) + 0.07f * (Hash(x, y) - 0.5f)
                        - (speck < 0.10f ? 0.07f : 0) + (speck > 0.96f ? 0.06f : 0),
            "cuir" => 1 + 0.14f * (Smooth(16, 3) - 0.5f) + 0.06f * (Smooth(4, 11) - 0.5f),
            "toile" => 1 + ((x / 2 + y / 2) % 2 == 0 ? 0.025f : -0.025f) + 0.05f * (Smooth(16, 5) - 0.5f),
            _ => 1 + 0.10f * (Smooth(8, 0) - 0.5f) - (speck < 0.06f ? 0.08f : 0) + (speck > 0.97f ? 0.07f : 0),
        };
        factor = 1 + (factor - 1) * strength;
        return new Color(fill.R * factor, fill.G * factor, fill.B * factor, fill.A);
    }

    /// <summary>
    /// Une copie de l'image où les pixels transparents entourés par le cadre prennent la couleur <paramref name="fill"/>
    /// (ceux reliés au bord de l'image, à l'extérieur du cadre, restent transparents).
    /// </summary>
    private static Image FillInside(Image source, Color fill, int corner)
    {
        var image = (Image)source.Duplicate();
        image.Convert(Image.Format.Rgba8);
        int w = image.GetWidth(), h = image.GetHeight();
        var outside = new bool[w, h];
        var todo = new Stack<(int X, int Y)>();
        for (int x = 0; x < w; x++) { todo.Push((x, 0)); todo.Push((x, h - 1)); }
        for (int y = 0; y < h; y++) { todo.Push((0, y)); todo.Push((w - 1, y)); }
        while (todo.Count > 0)
        {
            var (x, y) = todo.Pop();
            if (x < 0 || y < 0 || x >= w || y >= h || outside[x, y] || image.GetPixel(x, y).A > 0.5f)
                continue;
            outside[x, y] = true;
            todo.Push((x + 1, y)); todo.Push((x - 1, y)); todo.Push((x, y + 1)); todo.Push((x, y - 1));
        }
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            if (!outside[x, y] && image.GetPixel(x, y).A <= 0.5f)
                image.SetPixel(x, y, Grain(fill, x / FrameScale - corner, y / FrameScale - corner));
        return image;
    }

    /// <summary>Hauteur de la barre de titre des fenêtres (choix des conditions, niveau, fin de partie…).</summary>
    private static int WindowTitleHeight => (int)Px(34);

    /// <summary>
    /// Habille les contrôles que l'interface ne construit pas elle-même, surtout les fenêtres (AcceptDialog) :
    /// cadre de bois et d'or autour de la fenêtre et de son titre, fond brun, boutons et textes du jeu.
    /// </summary>
    public static void ApplyTheme()
    {
        var theme = ThemeDB.GetDefaultTheme();

        // le cadre de la fenêtre déborde vers le haut pour entourer aussi la barre de titre
        var border = (StyleBox)Frame(PanelColor).Duplicate();
        switch (border)
        {
            case StyleBoxTexture texture:
                texture.SetExpandMarginAll(Px(6));
                texture.ExpandMarginTop = WindowTitleHeight + Px(6);
                break;
            case StyleBoxFlat flat:
                flat.SetExpandMarginAll(Px(6));
                flat.ExpandMarginTop = WindowTitleHeight + Px(6);
                break;
        }
        theme.SetStylebox("embedded_border", "Window", border);
        theme.SetStylebox("embedded_unfocused_border", "Window", border);
        theme.SetConstant("title_height", "Window", WindowTitleHeight);
        theme.SetColor("title_color", "Window", BorderColor);
        theme.SetFontSize("title_font_size", "Window", TextSize(16));
        // les petites fenêtres (GameDialog) n'ont pas de bordure de Godot : leur fond est le cadre du jeu
        var inside = (StyleBox)Frame(PanelColor).Duplicate();
        inside.SetContentMarginAll(Px(24));
        theme.SetStylebox("panel", "AcceptDialog", inside);
        theme.SetConstant("buttons_separation", "AcceptDialog", (int)Px(12));
        // les boutons du bas des petites fenêtres : la hauteur commune, et assez larges (un « OK » seul était minuscule)
        theme.SetConstant("buttons_min_width", "AcceptDialog", (int)ArtPx(64));
        theme.SetConstant("buttons_min_height", "AcceptDialog", (int)ButtonHeight);
        theme.SetColor("font_color", "Label", TextColor);

        theme.SetStylebox("normal", "Button", ButtonBox(Colors.White, MapRenderer.Rgb(90, 66, 30), BorderColor));
        theme.SetStylebox("hover", "Button", ButtonBox(new Color(1.3f, 1.25f, 1.15f), MapRenderer.Rgb(120, 90, 40), BorderColor));
        theme.SetStylebox("pressed", "Button", ButtonBox(new Color(0.75f, 0.7f, 0.65f), MapRenderer.Rgb(70, 50, 20), BorderColor));
        theme.SetStylebox("disabled", "Button", ButtonBox(new Color(0.5f, 0.47f, 0.43f), MapRenderer.Rgb(50, 42, 32), DimColor));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", TextColor);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", TextColor);
        theme.SetColor("font_disabled_color", "Button", DimColor);

        // le curseur (nombre à recruter, partage d'une troupe) : une rainure creusée et un bouton de pierre
        var track = (StyleBox)Inset(DarkPanelColor).Duplicate();
        track.ContentMarginTop = track.ContentMarginBottom = Px(4);
        theme.SetStylebox("slider", "HSlider", track);
        theme.SetStylebox("grabber_area", "HSlider", new StyleBoxEmpty());
        theme.SetStylebox("grabber_area_highlight", "HSlider", new StyleBoxEmpty());
        theme.SetIcon("grabber", "HSlider", Knob(1f));
        theme.SetIcon("grabber_highlight", "HSlider", Knob(1.25f));
        theme.SetIcon("grabber_disabled", "HSlider", Knob(0.6f));
        // les barres de défilement (listes, fenêtre Construire…) : une rainure creusée et une plaque de pierre bien visible
        var groove = (StyleBox)Inset(DarkPanelColor).Duplicate();
        groove.ContentMarginLeft = groove.ContentMarginRight = Px(6);
        groove.ContentMarginTop = groove.ContentMarginBottom = Px(3);
        theme.SetStylebox("scroll", "VScrollBar", groove);
        theme.SetStylebox("scroll_focus", "VScrollBar", groove);
        foreach (var (state, light) in new[] { ("grabber", 1f), ("grabber_highlight", 1.25f), ("grabber_pressed", 0.85f) })
        {
            var grabber = new StyleBoxFlat
            {
                BgColor = MapRenderer.Rgb(92, 94, 98) * light, BorderColor = SilverColor * light,
                CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2,
            };
            grabber.SetBorderWidthAll((int)Px(2));
            grabber.ContentMarginLeft = grabber.ContentMarginRight = Px(5);
            grabber.ContentMarginTop = grabber.ContentMarginBottom = Px(10);
            theme.SetStylebox(state, "VScrollBar", grabber);
        }
        // les listes déroulantes (proposition A, choisie par l'utilisateur) : le bouton de pierre, une flèche d'argent ;
        // la liste dans un creux, la ligne survolée en plaque bordée d'argent, le choix marqué d'une coche
        theme.SetStylebox("normal", "OptionButton", ButtonBox(Colors.White, MapRenderer.Rgb(90, 66, 30), BorderColor));
        theme.SetStylebox("hover", "OptionButton", ButtonBox(new Color(1.3f, 1.25f, 1.15f), MapRenderer.Rgb(120, 90, 40), BorderColor));
        theme.SetStylebox("pressed", "OptionButton", ButtonBox(new Color(0.75f, 0.7f, 0.65f), MapRenderer.Rgb(70, 50, 20), BorderColor));
        theme.SetStylebox("disabled", "OptionButton", ButtonBox(new Color(0.5f, 0.47f, 0.43f), MapRenderer.Rgb(50, 42, 32), DimColor));
        theme.SetStylebox("focus", "OptionButton", new StyleBoxEmpty());
        theme.SetIcon("arrow", "OptionButton", Arrow('v', SilverColor));
        theme.SetConstant("arrow_margin", "OptionButton", (int)Px(12));
        theme.SetColor("font_color", "OptionButton", TextColor);
        theme.SetColor("font_hover_color", "OptionButton", Colors.White);
        theme.SetColor("font_pressed_color", "OptionButton", TextColor);
        theme.SetColor("font_disabled_color", "OptionButton", DimColor);
        var list = (StyleBox)Inset(DarkPanelColor).Duplicate();
        list.SetContentMarginAll(Px(5));
        theme.SetStylebox("panel", "PopupMenu", list);
        var hovered = (StyleBox)Plate(PanelColor, SilverColor).Duplicate();
        theme.SetStylebox("hover", "PopupMenu", hovered);
        theme.SetColor("font_color", "PopupMenu", TextColor);
        theme.SetColor("font_hover_color", "PopupMenu", Colors.White);
        theme.SetColor("font_disabled_color", "PopupMenu", DimColor);
        theme.SetFontSize("font_size", "PopupMenu", TextSize(14));
        theme.SetConstant("v_separation", "PopupMenu", (int)Px(8));
        theme.SetConstant("item_start_padding", "PopupMenu", (int)Px(8));
        theme.SetIcon("radio_checked", "PopupMenu", CheckMark(SilverColor));
        theme.SetIcon("radio_unchecked", "PopupMenu", Blank(CheckMark(SilverColor)));
        theme.SetIcon("checked", "PopupMenu", CheckMark(SilverColor));
        theme.SetIcon("unchecked", "PopupMenu", Blank(CheckMark(SilverColor)));
        // les champs de texte (graine, nom d'une sauvegarde) : creusés, un trait d'argent quand on y écrit
        var field = (StyleBox)Inset(DarkPanelColor).Duplicate();
        field.ContentMarginLeft = field.ContentMarginRight = Px(8);
        field.ContentMarginTop = field.ContentMarginBottom = Px(4);
        var focused = (StyleBox)Inset(DarkPanelColor, SilverColor).Duplicate();
        focused.ContentMarginLeft = focused.ContentMarginRight = Px(8);
        focused.ContentMarginTop = focused.ContentMarginBottom = Px(4);
        theme.SetStylebox("normal", "LineEdit", field);
        theme.SetStylebox("focus", "LineEdit", focused);
        theme.SetColor("font_color", "LineEdit", TextColor);
        theme.SetColor("font_placeholder_color", "LineEdit", DimColor);
        theme.SetColor("caret_color", "LineEdit", TextColor);
    }

    /// <summary>Une coche en pixels d'art (le choix d'une liste déroulante).</summary>
    public static Texture2D CheckMark(Color color)
    {
        LoadImages();
        // 7 × 6 pixels : la petite branche puis la grande
        int[,] points = { { 0, 3 }, { 1, 4 }, { 2, 5 }, { 3, 4 }, { 4, 3 }, { 5, 2 }, { 6, 1 }, { 6, 0 }, { 1, 3 }, { 2, 4 } };
        var image = Image.CreateEmpty(7, 6, false, Image.Format.Rgba8);
        for (int i = 0; i < points.GetLength(0); i++)
            image.SetPixel(points[i, 0], points[i, 1], color);
        return ImageTexture.CreateFromImage(Enlarged(image, FrameScale));
    }

    /// <summary>Une image vide de la même taille (pour que les lignes sans coche gardent leur alignement).</summary>
    private static Texture2D Blank(Texture2D like) =>
        ImageTexture.CreateFromImage(Image.CreateEmpty(like.GetWidth(), like.GetHeight(), false, Image.Format.Rgba8));

    /// <summary>Une liste déroulante (proposition A) : ces choix, celui-ci choisi ; <paramref name="chosen"/> à chaque choix.</summary>
    public static OptionButton MakeDropdown(System.Collections.Generic.IEnumerable<string> items, int selected, Vector2 size,
        Action<int> chosen, int fontSize = 15)
    {
        var dropdown = new OptionButton { Size = size, CustomMinimumSize = size, FitToLongestItem = false, FocusMode = Control.FocusModeEnum.None };
        SetFontSize(dropdown, fontSize);
        foreach (var item in items)
            dropdown.AddItem(item);
        dropdown.Selected = selected;
        dropdown.ItemSelected += index => chosen((int)index);
        return dropdown;
    }

    /// <summary>
    /// Une petite flèche en pixels d'art (triangle plein, ombré d'un pixel sombre), agrandie comme le cadre :
    /// vers le bas (liste déroulante), la gauche ou la droite (sélecteur).
    /// </summary>
    public static Texture2D Arrow(char direction, Color color)
    {
        LoadImages();
        const int size = 7, depth = 4;
        bool down = direction == 'v';
        int w = down ? size : depth + 1, h = down ? depth + 1 : size;
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var shadow = MapRenderer.Rgb(20, 18, 16);
        for (int i = 0; i < depth; i++)
        for (int j = i; j < size - i; j++)
        {
            // i : la rangée depuis la base du triangle, j : la position le long de la base
            var (x, y) = direction switch
            {
                'v' => (j, i),
                '<' => (depth - 1 - i, j),
                _ => (i, j),
            };
            image.SetPixel(x, y, color);
            // l'ombre, un pixel plus bas (ou à droite pour une flèche couchée), là où il n'y a rien
            var (sx, sy) = down ? (x, y + 1) : (x + 1, y);
            if (sx < w && sy < h && image.GetPixel(sx, sy).A == 0)
                image.SetPixel(sx, sy, shadow);
        }
        return ImageTexture.CreateFromImage(Enlarged(image, FrameScale));
    }

    /// <summary>
    /// Le bouton du curseur : une petite plaque de pierre grise bordée d'argent, en pixels d'art agrandis comme le cadre ;
    /// <paramref name="light"/> l'éclaircit (survol) ou l'assombrit (désactivé).
    /// </summary>
    private static Texture2D Knob(float light)
    {
        LoadImages();
        const int w = 6, h = 10;
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        Color stone = MapRenderer.Rgb(92, 94, 98) * light, rim = SilverColor * light, dark = MapRenderer.Rgb(30, 30, 34);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1;
            bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
            Color c = corner ? new Color(0, 0, 0, 0) : edge ? dark : x == 1 || y == 1 ? rim : stone;
            image.SetPixel(x, y, new Color(c.R, c.G, c.B, corner ? 0 : 1));
        }
        return ImageTexture.CreateFromImage(Enlarged(image, FrameScale));
    }

    /// <summary>Une barre (mouvement, expérience) : une rainure creusée, et un remplissage à plat de cette couleur.</summary>
    public static void StyleBar(ProgressBar bar, Color fill)
    {
        // sans marges intérieures ni texte : sinon la barre grandit au-delà de la taille voulue
        StyleBox Tight(StyleBox box)
        {
            var copy = (StyleBox)box.Duplicate();
            copy.SetContentMarginAll(0);
            return copy;
        }
        bar.AddThemeStyleboxOverride("background", Tight(Inset(MapRenderer.Rgb(20, 16, 12))));
        bar.AddThemeStyleboxOverride("fill", Tight(Plate(fill, fill.Darkened(0.4f))));
        bar.AddThemeFontSizeOverride("font_size", 1);
    }

    // Les images des créatures dans l'interface (choix de l'utilisateur, 2026-11-07, comme Heroes 1 et 2) :
    // - les places de taille ordinaire montrent l'image de combat, toujours ×2 (CreaturePicture) : les pixels ont la même
    //   taille partout ; une grande créature qui ne tient pas est coupée, en gardant sa tête et son avant ;
    // - les places étroites (panneau de la carte, ordre de jeu du combat) montrent la petite image de la carte
    //   (SmallCreatureArt).

    /// <summary>Agrandissement des images de combat dans l'interface : le même partout.</summary>
    public const int CreatureArtScale = 2;

    /// <summary>
    /// La petite image d'une créature (celle de la carte) agrandie du plus grand nombre entier de fois qui tient dans une
    /// place de cette taille (pixels de l'écran) ; sans petite image, l'image de combat. Null si rien ne tient.
    /// </summary>
    public static (Texture2D Texture, Vector2 Size)? SmallCreatureArt(CreatureType type, Vector2 box)
    {
        var texture = Art.Get($"creatures/{type.Id}") ?? Art.Get($"creatures/battle/{type.Id}");
        if (texture == null)
            return null;
        var size = texture.GetSize();
        int scale = (int)Math.Floor(Math.Min(box.X / size.X, box.Y / size.Y));
        return scale >= 1 ? (texture, size * scale) : null;
    }

    /// <summary>
    /// L'image de combat d'une créature dans une place (x, y, largeur, hauteur en pixels de l'écran) : la partie dessinée
    /// de l'image (sans ses bords vides), ×<see cref="CreatureArtScale"/>, centrée si elle tient ; sinon coupée par la
    /// place, en gardant le haut (la tête) et la droite (l'avant : les créatures regardent à droite). Sans image de combat,
    /// la petite image, la plus grande qui tient. Null s'il n'y a aucune image.
    /// </summary>
    public static Control? CreaturePicture(CreatureType type, Rect2 place)
    {
        var holder = new Control
        {
            Position = place.Position, Size = place.Size, ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (Art.Get($"creatures/battle/{type.Id}") is { } battle)
        {
            var used = UsedRect(battle);
            var size = used.Size * CreatureArtScale;
            float x = size.X <= place.Size.X ? (place.Size.X - size.X) / 2 : place.Size.X - size.X;
            float y = size.Y <= place.Size.Y ? (place.Size.Y - size.Y) / 2 : 0;
            holder.AddChild(Picture(new AtlasTexture { Atlas = battle, Region = used },
                new Vector2(MathF.Floor(x), MathF.Floor(y)), size));
            return holder;
        }
        if (SmallCreatureArt(type, place.Size) is not { } small)
            return null;
        holder.AddChild(Picture(small.Texture, ((place.Size - small.Size) / 2).Floor(), small.Size));
        return holder;
    }

    private static readonly Dictionary<Texture2D, Rect2> UsedRects = new();

    /// <summary>La partie dessinée d'une image (sans ses bords transparents), en pixels de l'image.</summary>
    public static Rect2 UsedRect(Texture2D texture)
    {
        if (!UsedRects.TryGetValue(texture, out var rect))
        {
            var used = texture.GetImage()?.GetUsedRect() ?? new Rect2I();
            rect = used.Size.X > 0 && used.Size.Y > 0 ? new Rect2(used.Position, used.Size) : new Rect2(Vector2.Zero, texture.GetSize());
            UsedRects[texture] = rect;
        }
        return rect;
    }

    /// <summary>Un bouton aux couleurs du jeu. Il ne prend pas le focus, sinon Espace/Entrée le « cliqueraient ».</summary>
    public static Button MakeButton(string text, int fontSize = 16)
    {
        // au moins la hauteur commune : un bouton posé dans une rangée ne se règle pas sur son texte
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, ButtonHeight) };
        SetFontSize(button, fontSize);
        button.AddThemeColorOverride("font_color", TextColor);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", DimColor);
        button.AddThemeStyleboxOverride("normal", ButtonBox(Colors.White, MapRenderer.Rgb(90, 66, 30), BorderColor));
        button.AddThemeStyleboxOverride("hover", ButtonBox(new Color(1.3f, 1.25f, 1.15f), MapRenderer.Rgb(120, 90, 40), BorderColor));
        button.AddThemeStyleboxOverride("pressed", ButtonBox(new Color(0.75f, 0.7f, 0.65f), MapRenderer.Rgb(70, 50, 20), BorderColor));
        button.AddThemeStyleboxOverride("disabled", ButtonBox(new Color(0.5f, 0.47f, 0.43f), MapRenderer.Rgb(50, 42, 32), DimColor));
        return button;
    }

    /// <summary>
    /// Donne à un contrôle (souvent un bouton) sa taille : la largeur arrondie à la grille, et une hauteur commune
    /// (<see cref="ButtonHeight"/>, <see cref="SmallButtonHeight"/>, <see cref="LargeButtonHeight"/>…).
    /// </summary>
    public static T Sized<T>(T control, float width, float height) where T : Control
    {
        control.CustomMinimumSize = control.Size = new Vector2(Snap(width), height);
        return control;
    }

    /// <summary>
    /// Un bouton de taille fixe dont le texte (sur une ou plusieurs lignes) est dans une étiquette posée dessus : un
    /// bouton s'élargit pour contenir son propre texte, une étiquette de taille fixe non.
    /// </summary>
    public static Button FixedButton(string text, int fontSize, Vector2 size, bool disabled = false)
    {
        var button = MakeButton("", fontSize);
        size = new Vector2(Snap(size.X), Snap(size.Y));
        button.CustomMinimumSize = button.Size = size;
        button.Disabled = disabled;
        var label = MakeLabel(text, fontSize, disabled ? DimColor : TextColor);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true; // avant la taille : sinon elle se règle sur le texte entier
        label.Size = size;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        button.AddChild(label);
        return button;
    }

    /// <summary>Une image à cette taille (le mode « taille libre » d'abord : sinon l'image garderait sa propre taille).</summary>
    public static TextureRect Picture(Texture2D texture, Vector2 position, Vector2 size) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
        Texture = texture, Position = position, Size = size,
    };

    /// <summary>
    /// Demande un nombre entre 1 et <paramref name="max"/> avec un curseur (partager une troupe) ; <paramref name="chosen"/>
    /// est appelé avec ce nombre si le joueur valide.
    /// </summary>
    public static void AskCount(Node parent, string title, Func<int, string> describe, int max, Action<int> chosen)
    {
        var dialog = new GameDialog { Title = title, OkButtonText = "OK" };
        dialog.AddCancelButton(Texts.CancelButton);
        var content = new VBoxContainer { CustomMinimumSize = V(380, 0) };
        content.AddThemeConstantOverride("separation", (int)Px(10));
        var text = MakeLabel("", 15, TextColor);
        content.AddChild(text);
        var slider = new HSlider { MinValue = 1, MaxValue = Math.Max(1, max), Step = 1, Value = Math.Max(1, max / 2), FocusMode = Control.FocusModeEnum.None };
        slider.CustomMinimumSize = V(360, 24);
        content.AddChild(slider);
        void Update() => text.Text = describe((int)slider.Value);
        slider.ValueChanged += _ => Update();
        Update();
        dialog.AddChild(content);
        dialog.Confirmed += () =>
        {
            chosen((int)slider.Value);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        parent.AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(420), 0));
    }

    /// <summary>Une case à cocher aux couleurs du jeu.</summary>
    public static CheckBox MakeCheckBox(string text, bool pressed)
    {
        var box = new CheckBox { Text = text, ButtonPressed = pressed, FocusMode = Control.FocusModeEnum.None };
        box.AddThemeConstantOverride("h_separation", (int)Px(8));
        SetFontSize(box, 15);
        box.AddThemeColorOverride("font_color", TextColor);
        box.AddThemeColorOverride("font_pressed_color", TextColor);
        box.AddThemeColorOverride("font_hover_color", Colors.White);
        box.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
        // la case elle-même, dessinée claire sur le fond sombre (cochée ou non)
        box.AddThemeIconOverride("checked", CheckIcon(true));
        box.AddThemeIconOverride("unchecked", CheckIcon(false));
        return box;
    }

    private static Texture2D? _checked, _unchecked;

    /// <summary>Une case à cocher en pixel art : un cadre clair, et un carré doré dedans quand elle est cochée.</summary>
    private static Texture2D CheckIcon(bool on)
    {
        if ((on ? _checked : _unchecked) is { } cached)
            return cached;
        int size = (int)Px(18), line = (int)Px(2), inset = (int)Px(5);
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(DarkPanelColor);
        for (int i = 0; i < size; i++)
            for (int w = 0; w < line; w++)
            {
                image.SetPixel(i, w, TextColor);
                image.SetPixel(i, size - 1 - w, TextColor);
                image.SetPixel(w, i, TextColor);
                image.SetPixel(size - 1 - w, i, TextColor);
            }
        if (on)
            image.FillRect(new Rect2I(inset, inset, size - 2 * inset, size - 2 * inset), BorderColor);
        var texture = ImageTexture.CreateFromImage(image);
        if (on)
            _checked = texture;
        else
            _unchecked = texture;
        return texture;
    }

    /// <summary>Retire et libère tous les enfants d'un nœud (pour reconstruire une liste).</summary>
    public static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Marque un bouton comme choisi (taille de carte, sauvegarde) : plus sombre, comme enfoncé.</summary>
    public static void MarkSelected(Button button) =>
        button.AddThemeStyleboxOverride("normal", ButtonBox(new Color(0.58f, 0.58f, 0.6f), MapRenderer.Rgb(70, 50, 20), SilverColor));
}
