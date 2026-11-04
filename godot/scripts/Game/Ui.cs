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

    public const string FontPath = "res://fonts/VT323-Regular.ttf";

    /// <summary>
    /// VT323 est plus petite et plus étroite que la police de Godot : les tailles demandées par l'interface
    /// (pensées pour la police de Godot) sont multipliées d'autant.
    /// </summary>
    public const float FontScale = 1.3f;

    /// <summary>Taille à partir de laquelle la police reste nette sans lissage (en dessous, elle est lissée).</summary>
    public const int SharpFontSize = 25;

    private static Font? _sharpFont;
    private static Font? _smoothFont;

    /// <summary>La taille réelle d'un texte prévu à la taille <paramref name="size"/>.</summary>
    public static int TextSize(int size) => _smoothFont == null ? size : (int)MathF.Round(size * FontScale);

    /// <summary>
    /// La police pixel (VT323, licence OFL) devient la police de toute l'interface et des textes de la carte.
    /// Lue directement depuis le fichier, sans import par l'éditeur. Sans le fichier, la police de Godot reste.
    /// </summary>
    public static void UsePixelFont()
    {
        if (!Godot.FileAccess.FileExists(FontPath))
            return;
        var data = Godot.FileAccess.GetFileAsBytes(FontPath);
        _sharpFont = MakeFont(data, TextServer.FontAntialiasing.None);
        _smoothFont = MakeFont(data, TextServer.FontAntialiasing.Gray);
        // le thème par défaut sert à tous les contrôles, même ceux des CanvasLayer (qui n'héritent pas du thème de la fenêtre)
        var theme = ThemeDB.GetDefaultTheme();
        theme.DefaultFont = SharpFontSize <= 16 ? _sharpFont : _smoothFont;
        theme.DefaultFontSize = TextSize(16);
        ThemeDB.FallbackFont = theme.DefaultFont; // pour les textes dessinés sur la carte
    }

    private static FontFile MakeFont(byte[] data, TextServer.FontAntialiasing antialiasing) => new()
    {
        Data = data,
        Antialiasing = antialiasing,
        // net : calé sur la grille de pixels ; lissé : sans calage, sinon les traits deviennent inégaux
        Hinting = antialiasing == TextServer.FontAntialiasing.None ? TextServer.Hinting.Normal : TextServer.Hinting.None,
        SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
    };

    /// <summary>Donne à un contrôle la taille réelle de son texte, et la police nette si cette taille le permet.</summary>
    public static void SetFontSize(Control control, int fontSize)
    {
        int size = TextSize(fontSize);
        control.AddThemeFontSizeOverride("font_size", size);
        if (_sharpFont != null && _smoothFont != null)
            control.AddThemeFontOverride("font", size >= SharpFontSize ? _sharpFont : _smoothFont);
    }

    public static T At<T>(T control, float x, float y) where T : Control
    {
        control.Position = new Vector2(x, y);
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
        if (border is { } b)
        {
            box.BorderColor = b;
            box.BorderWidthLeft = left;
            box.BorderWidthTop = top;
            box.BorderWidthRight = right;
            box.BorderWidthBottom = bottom;
        }
        box.SetCornerRadiusAll(radius);
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
        _frameImage = Art.GetImage("ui/panel") is { } frame ? WithoutRepeatedDetails(frame, FrameCorner) : null;
        _buttonImage = Art.GetImage("ui/button") is { } button ? WithoutRepeatedDetails(button, ButtonCorner) : null;
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
            : Sliced(FillInside(_frameImage, fill), FrameCorner, FrameContentX, FrameContentY);
        Frames[fill] = box;
        return box;
    }

    /// <summary>Le fond d'un bouton, teinté par <paramref name="tint"/> (survol, appui, désactivé).</summary>
    private static StyleBox ButtonBox(Color tint, Color flatColor, Color border)
    {
        LoadImages();
        if (_buttonImage == null)
            return Box(flatColor, border, 2, 2, 2, 2, radius: 6);
        var box = Sliced(_buttonImage, ButtonCorner, ButtonCorner + 8, 5);
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
    /// Une copie de l'image où les pixels transparents entourés par le cadre prennent la couleur <paramref name="fill"/>
    /// (ceux reliés au bord de l'image, à l'extérieur du cadre, restent transparents).
    /// </summary>
    private static Image FillInside(Image source, Color fill)
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
                image.SetPixel(x, y, fill);
        return image;
    }

    /// <summary>Hauteur de la barre de titre des fenêtres (choix des conditions, niveau, fin de partie…).</summary>
    private const int WindowTitleHeight = 34;

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
                texture.SetExpandMarginAll(6);
                texture.ExpandMarginTop = WindowTitleHeight + 6;
                break;
            case StyleBoxFlat flat:
                flat.SetExpandMarginAll(6);
                flat.ExpandMarginTop = WindowTitleHeight + 6;
                break;
        }
        theme.SetStylebox("embedded_border", "Window", border);
        theme.SetStylebox("embedded_unfocused_border", "Window", border);
        theme.SetConstant("title_height", "Window", WindowTitleHeight);
        theme.SetColor("title_color", "Window", BorderColor);
        theme.SetFontSize("title_font_size", "Window", TextSize(16));
        var inside = Box(PanelColor, null);
        inside.SetContentMarginAll(14);
        theme.SetStylebox("panel", "AcceptDialog", inside);
        theme.SetConstant("buttons_separation", "AcceptDialog", 12);
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
    }

    /// <summary>
    /// L'image d'une créature (art/creatures/{id}.png), agrandie <paramref name="scale"/> fois (pixels nets),
    /// ou null si elle n'a pas d'image.
    /// </summary>
    public static TextureRect? CreatureImage(CreatureType type, int scale = 2)
    {
        if (Art.Get($"creatures/{type.Id}") is not { } texture)
            return null;
        return new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            CustomMinimumSize = texture.GetSize() * scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
    }

    /// <summary>Un bouton aux couleurs du jeu. Il ne prend pas le focus, sinon Espace/Entrée le « cliqueraient ».</summary>
    public static Button MakeButton(string text, int fontSize = 16)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
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

    /// <summary>Marque un bouton comme choisi (le héros sélectionné) : plus clair, et bordé de blanc sans image.</summary>
    public static void MarkSelected(Button button) =>
        button.AddThemeStyleboxOverride("normal", ButtonBox(new Color(1.45f, 1.4f, 1.25f), MapRenderer.Rgb(120, 90, 40), Colors.White));
}
