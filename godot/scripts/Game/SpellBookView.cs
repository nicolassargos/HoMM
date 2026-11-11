using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le livre de sorts ouvert (décidé avec l'utilisateur, 2026-11-11) : une page par niveau de sort, deux pages à la fois ;
/// sur chaque page, jusqu'à six sorts (leur icône, leur nom, leur coût ; ce qu'ils font au survol) ; des onglets sur le
/// bord droit pour ne montrer qu'une école ; des coins de page pour tourner, avec une page qui tourne.
/// <para>
/// Maquette : le cuir, le parchemin, les onglets et les icônes sont dessinés par le code, pour régler les tailles avant
/// de faire les images (le livre ouvert, une icône de 32 × 32 pixels d'art par sort). Toutes les tailles sont en pixels
/// d'art (<see cref="Ui.ArtPx"/>).
/// </para>
/// </summary>
public partial class SpellBookView : Control
{
    // ---- les tailles, en pixels d'art
    /// <summary>Le livre ouvert (couverture comprise).</summary>
    public const int BookWidth = 480, BookHeight = 288;
    private const int Cover = 8, Spine = 8;
    private const int PageWidth = (BookWidth - 2 * Cover - Spine) / 2, PageHeight = BookHeight - 2 * Cover;
    private const int PageMargin = 10, HeaderHeight = 28, FooterHeight = 28;
    private const int Columns = 2, Rows = 3, SpellsPerPage = Columns * Rows;
    private const int CellWidth = (PageWidth - 2 * PageMargin) / Columns, CellHeight = (PageHeight - HeaderHeight - FooterHeight) / Rows;

    /// <summary>Le côté de l'icône d'un sort (l'image à faire).</summary>
    public const int IconSize = 32;

    /// <summary>Les onglets des écoles, à droite du livre.</summary>
    private const int TabWidth = 40, TabHeight = 30, TabGap = 4, TabOut = 34;

    /// <summary>La place totale : le livre et ses onglets.</summary>
    public static Vector2 TotalSize => new(ArtPx(BookWidth + TabOut), ArtPx(BookHeight));

    // ---- les couleurs du cuir, du parchemin et de l'encre
    private static readonly Color Leather = MapRenderer.Rgb(86, 40, 28), LeatherDark = MapRenderer.Rgb(52, 22, 16),
        LeatherLight = MapRenderer.Rgb(128, 66, 44), Parchment = MapRenderer.Rgb(226, 208, 164),
        ParchmentEdge = MapRenderer.Rgb(196, 172, 122), Ink = MapRenderer.Rgb(58, 38, 22), SoftInk = MapRenderer.Rgb(112, 86, 58),
        Gold = MapRenderer.Rgb(150, 104, 30);

    /// <summary>La couleur de chaque école (onglets, icônes d'attente) ; null : tous les sorts.</summary>
    private static readonly (string? School, Color Color)[] Schools =
    {
        (null, MapRenderer.Rgb(170, 132, 60)), ("eau", MapRenderer.Rgb(60, 110, 170)), ("feu", MapRenderer.Rgb(176, 64, 40)),
        ("terre", MapRenderer.Rgb(104, 120, 56)), ("air", MapRenderer.Rgb(150, 160, 176)),
    };

    private readonly Hero _hero;
    private readonly int _points;
    private readonly Func<SpellType, bool>? _canCast;
    private readonly Action<SpellType>? _chosen;

    /// <summary>L'école montrée (null : toutes) et la double page ouverte (0 : les deux premières pages).</summary>
    private string? _school;
    private int _spread;

    /// <summary>Les pages, d'après l'école : un niveau par page (deux pages pour un niveau de plus de six sorts).</summary>
    private List<(int Level, List<SpellType> Spells)> _pages = new();

    private readonly Control _content = new() { MouseFilter = MouseFilterEnum.Pass };
    private readonly Control _turnLayer = new() { MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>La page qui tourne : le temps écoulé (0 à <see cref="TurnTime"/>) et le sens (+1 : vers la droite).</summary>
    private float _turn = TurnTime;
    private int _turnDirection;
    private const float TurnTime = 0.3f;

    public SpellBookView() : this(null!, 0, null, null) { } // pour Godot

    /// <param name="points">Les points de sort qui restent.</param>
    /// <param name="canCast">Ce sort peut-il être lancé maintenant ? (null : le livre se lit seulement)</param>
    /// <param name="chosen">Appelé avec le sort choisi.</param>
    public SpellBookView(Hero hero, int points, Func<SpellType, bool>? canCast, Action<SpellType>? chosen)
    {
        _hero = hero;
        _points = points;
        _canCast = canCast;
        _chosen = chosen;
        CustomMinimumSize = Size = TotalSize;
        MouseFilter = MouseFilterEnum.Stop;
        _content.Size = TotalSize;
        _turnLayer.Size = TotalSize;
        _turnLayer.Draw += DrawTurningPage;
        AddChild(_content);
        AddChild(_turnLayer);
        if (hero == null)
            return;
        // développement : « --book-school=feu » (un onglet), « --book-spread=1 » (une double page), « --book-turn=0.3 »
        // (la page qui tourne, figée à cet instant, de 0 à 1)
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--book-school="))
                _school = arg["--book-school=".Length..];
            else if (arg.StartsWith("--book-spread=") && int.TryParse(arg["--book-spread=".Length..], out int spread))
                _spread = spread;
            else if (arg.StartsWith("--book-turn=") && float.TryParse(arg["--book-turn=".Length..],
                         System.Globalization.CultureInfo.InvariantCulture, out float turn))
                _frozenTurn = turn;
        }
        Rebuild();
        if (_frozenTurn is { } frozen)
        {
            _turnDirection = 1;
            _turn = frozen * TurnTime;
        }
    }

    /// <summary>Développement : la page qui tourne figée à cet instant (« --book-turn »).</summary>
    private float? _frozenTurn;

    private static float A(float artPixels) => ArtPx(artPixels);

    private static Vector2 A(float x, float y) => new(ArtPx(x), ArtPx(y));

    /// <summary>La place d'une page dans le livre (0 : à gauche, 1 : à droite).</summary>
    private static Rect2 PageRect(int side) =>
        new(A(Cover + side * (PageWidth + Spine), Cover), A(PageWidth, PageHeight));

    private int SpreadCount => Math.Max(1, (_pages.Count + 1) / 2);

    /// <summary>Refait les pages d'après l'école choisie, puis la double page ouverte.</summary>
    private void Rebuild()
    {
        _pages = _hero.Spells
            .Where(s => _school == null || s.School == _school)
            .GroupBy(s => s.Level).OrderBy(g => g.Key)
            .SelectMany(g => g.OrderBy(s => s.Name).Chunk(SpellsPerPage).Select(chunk => (g.Key, chunk.ToList())))
            .ToList();
        _spread = Math.Clamp(_spread, 0, SpreadCount - 1);
        Fill();
    }

    /// <summary>Remplit la double page ouverte : les deux pages, leurs coins, les onglets, les points de sort.</summary>
    private void Fill()
    {
        Clear(_content);
        for (int side = 0; side < 2; side++)
        {
            int index = 2 * _spread + side;
            var page = PageRect(side);
            if (index < _pages.Count)
                FillPage(page, _pages[index]);
            else if (index == 0)
                _content.AddChild(InkLabel(_hero.Spells.Count == 0 ? Texts.NoSpellsKnown : Texts.NoSpellOfSchool, 12, SoftInk,
                    page.Position + A(PageMargin, HeaderHeight), PageWidth - 2 * PageMargin, wrap: true));
        }
        // les points de sort, en bas de la page de gauche ; les coins pour tourner, en bas de chaque page
        var left = PageRect(0);
        _content.AddChild(InkLabel(Texts.SpellPointsLine(_points, _hero.MaxSpellPoints), 12, Ink,
            left.Position + A(PageMargin, PageHeight - FooterHeight + 8), PageWidth - 2 * PageMargin, center: true));
        if (_spread > 0)
            _content.AddChild(Corner(left.Position + A(0, PageHeight - 20), -1));
        if (_spread < SpreadCount - 1)
            _content.AddChild(Corner(PageRect(1).End - A(20, 20), +1));
        AddTabs();
        QueueRedraw();
    }

    /// <summary>Une page : le niveau en titre, puis ses sorts en deux colonnes.</summary>
    private void FillPage(Rect2 page, (int Level, List<SpellType> Spells) content)
    {
        _content.AddChild(InkLabel(Texts.GuildLevel(content.Level), 20, Gold, page.Position + A(0, 6), PageWidth, center: true));
        for (int i = 0; i < content.Spells.Count; i++)
        {
            var at = page.Position + A(PageMargin + i % Columns * CellWidth, HeaderHeight + i / Columns * CellHeight);
            _content.AddChild(SpellCell(content.Spells[i], at));
        }
    }

    /// <summary>
    /// Un sort : son icône (en attendant l'image : un carré de la couleur de son école, ses initiales), son nom, son coût.
    /// Au combat, un clic le choisit ; un sort qu'on ne peut pas lancer est pâli. Ce qu'il fait au survol.
    /// </summary>
    private Control SpellCell(SpellType spell, Vector2 at)
    {
        bool castable = _canCast?.Invoke(spell) == true;
        bool dim = _canCast != null && !castable;
        var cell = new Control
        {
            Position = at, Size = A(CellWidth, CellHeight), TooltipText = Texts.SpellTooltip(spell, _hero, _points),
            MouseFilter = MouseFilterEnum.Stop, Modulate = dim ? new Color(1, 1, 1, 0.45f) : Colors.White,
            MouseDefaultCursorShape = castable ? CursorShape.PointingHand : CursorShape.Arrow,
        };
        var color = Schools.FirstOrDefault(s => s.School == spell.School).Color;
        var icon = new SpellIconPlaceholder { Color = color, Initials = Initials(spell.Name), Position = A((CellWidth - IconSize) / 2f, 0), Size = A(IconSize, IconSize) };
        cell.AddChild(icon);
        // le nom (deux lignes au plus) puis le coût, juste dessous
        var lines = new VBoxContainer { Position = A(0, IconSize + 2), Size = A(CellWidth, 0), MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", 0);
        var name = InkLabel(spell.Name, 12, Ink, Vector2.Zero, CellWidth, center: true, wrap: true);
        name.MaxLinesVisible = 2;
        lines.AddChild(name);
        lines.AddChild(InkLabel(Texts.SpellCost(spell, _hero), 11,
            _hero.SpellCost(spell) > _points ? MapRenderer.Rgb(150, 40, 30) : SoftInk, Vector2.Zero, CellWidth, center: true));
        cell.AddChild(lines);
        cell.MouseEntered += () => icon.Highlight = true;
        cell.MouseExited += () => icon.Highlight = false;
        cell.GuiInput += e =>
        {
            if (castable && e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                _chosen?.Invoke(spell);
        };
        return cell;
    }

    private static string Initials(string name) =>
        string.Concat(name.Split(' ', '\'').Where(w => w.Length > 2).Take(2).Select(w => char.ToUpperInvariant(w[0])));

    /// <summary>Un texte à l'encre sur le parchemin.</summary>
    private static Label InkLabel(string text, int size, Color color, Vector2 at, float widthArt, bool center = false, bool wrap = false)
    {
        var label = MakeLabel("", size, color);
        label.HorizontalAlignment = center ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        if (wrap)
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.Position = at;
        label.CustomMinimumSize = new Vector2(A(widthArt), 0);
        label.Size = new Vector2(A(widthArt), 0);
        label.Text = text;
        return label;
    }

    /// <summary>Un coin de page pour tourner (<paramref name="direction"/> : −1 en arrière, +1 en avant).</summary>
    private Control Corner(Vector2 at, int direction)
    {
        var corner = new Button
        {
            Position = at, Size = A(20, 20), CustomMinimumSize = A(20, 20), Flat = true, FocusMode = FocusModeEnum.None,
            TooltipText = direction < 0 ? Texts.PreviousPage : Texts.NextPage,
        };
        corner.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        corner.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        corner.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        // le coin replié : un triangle de parchemin plus sombre
        corner.Draw += () =>
        {
            var s = A(20, 20);
            var points = direction > 0
                ? new[] { new Vector2(s.X, 0), new Vector2(s.X, s.Y), new Vector2(0, s.Y) }
                : new[] { new Vector2(0, 0), new Vector2(s.X, s.Y), new Vector2(0, s.Y) };
            corner.DrawColoredPolygon(points, corner.IsHovered() ? ParchmentEdge.Darkened(0.2f) : ParchmentEdge);
            corner.DrawPolyline(points.Append(points[0]).ToArray(), SoftInk, Grid);
        };
        corner.MouseEntered += corner.QueueRedraw;
        corner.MouseExited += corner.QueueRedraw;
        corner.Pressed += () => TurnPage(direction);
        return corner;
    }

    /// <summary>Tourne la page : la double page suivante (ou précédente), et la page qui tourne par-dessus.</summary>
    public void TurnPage(int direction)
    {
        int next = Math.Clamp(_spread + direction, 0, SpreadCount - 1);
        if (next == _spread)
            return;
        _spread = next;
        _turn = 0;
        _turnDirection = direction;
        Fill();
    }

    /// <summary>Les onglets des écoles, sur le bord droit : celui qui est choisi sort plus et s'éclaire.</summary>
    private void AddTabs()
    {
        for (int i = 0; i < Schools.Length; i++)
        {
            var (school, color) = Schools[i];
            bool chosen = school == _school;
            var tab = new Control
            {
                Position = A(BookWidth - Cover + (chosen ? 0 : -4), Cover + 8 + i * (TabHeight + TabGap)),
                Size = A(TabWidth, TabHeight), MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand,
                TooltipText = Texts.SchoolTabTooltip(school),
            };
            tab.Draw += () =>
            {
                var c = chosen ? color.Lightened(0.15f) : color.Darkened(0.2f);
                tab.DrawRect(new Rect2(Vector2.Zero, tab.Size), c);
                tab.DrawRect(new Rect2(Vector2.Zero, tab.Size), c.Darkened(0.45f), false, Grid);
                tab.DrawRect(new Rect2(new Vector2(Grid, Grid), new Vector2(tab.Size.X - 2 * Grid, Grid)), c.Lightened(0.3f));
            };
            var label = MakeLabel(Texts.SchoolTab(school), 11, chosen ? Colors.White : TextColor);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Position = A(4, 0);
            label.Size = A(TabWidth - 4, TabHeight);
            tab.AddChild(label);
            tab.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _school != school)
                {
                    _school = school;
                    _spread = 0;
                    Rebuild();
                }
            };
            // derrière le livre : ajouté avant le contenu
            _content.AddChild(tab);
            _content.MoveChild(tab, 0);
        }
    }

    public override void _Process(double delta)
    {
        if (_turn >= TurnTime || _frozenTurn != null)
        {
            if (_frozenTurn != null)
                _turnLayer.QueueRedraw();
            return;
        }
        _turn = Math.Min(TurnTime, _turn + (float)delta);
        _turnLayer.QueueRedraw();
    }

    public override void _Draw()
    {
        // la couverture de cuir, un liseré clair, le dos plus sombre
        var book = new Rect2(Vector2.Zero, A(BookWidth, BookHeight));
        DrawRect(book, Leather);
        DrawRect(book, LeatherDark, false, A(2));
        DrawRect(new Rect2(A(3, 3), A(BookWidth - 6, BookHeight - 6)), LeatherLight, false, Grid);
        DrawRect(new Rect2(A(Cover + PageWidth, 0), A(Spine, BookHeight)), LeatherDark);
        // les deux pages, plus sombres vers le dos (la courbure du papier) et sur leur bord
        for (int side = 0; side < 2; side++)
            DrawPage(this, PageRect(side), side);
    }

    /// <summary>Une page de parchemin : le papier, un dégradé en bandes vers le dos, un bord plus sombre.</summary>
    private static void DrawPage(CanvasItem canvas, Rect2 page, int side)
    {
        canvas.DrawRect(page, Parchment);
        const int bands = 6;
        for (int i = 0; i < bands; i++)
        {
            float w = A(2);
            float x = side == 0 ? page.End.X - (i + 1) * w : page.Position.X + i * w;
            canvas.DrawRect(new Rect2(x, page.Position.Y, w, page.Size.Y), new Color(ParchmentEdge, 0.9f - i * 0.15f));
        }
        canvas.DrawRect(page, ParchmentEdge.Darkened(0.25f), false, Grid);
    }

    /// <summary>
    /// La page qui tourne : une feuille qui part d'un côté, se rétrécit jusqu'au dos puis s'ouvre de l'autre côté (son
    /// envers plus sombre), en pixels d'art entiers.
    /// </summary>
    private void DrawTurningPage()
    {
        if (_turn >= TurnTime || _turnDirection == 0)
            return;
        float t = _turn / TurnTime;
        float spineX = A(Cover + PageWidth + Spine / 2f);
        float width = Snap(A(PageWidth) * MathF.Abs(MathF.Cos(MathF.PI * t)));
        // en avant : la feuille quitte la page de droite (t < 0,5) puis se couche à gauche ; en arrière, l'inverse
        bool onRight = (_turnDirection > 0) == (t < 0.5f);
        float x = onRight ? spineX : spineX - width;
        var sheet = new Rect2(x, A(Cover), width, A(PageHeight));
        _turnLayer.DrawRect(sheet, t < 0.5f ? Parchment : Parchment.Darkened(0.12f));
        _turnLayer.DrawRect(sheet, ParchmentEdge.Darkened(0.3f), false, Grid);
    }
}
