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
/// bord droit pour ne montrer qu'une école ; des coins de page pour tourner. Il sert au héros (son livre) et à la guilde
/// des mages (son grimoire).
/// <para>
/// Magique : il s'ouvre (la couverture tourne, puis une gerbe de lumière et d'étincelles), des étincelles suivent la page
/// qui tourne, le sort survolé luit de la couleur de son école. Tout est dessiné par le jeu, sur la grille des pixels
/// d'art (<see cref="Ui.ArtPx"/>).
/// </para>
/// <para>
/// Maquette : le cuir, le parchemin, les onglets et les icônes sont dessinés par le code, pour régler les tailles avant
/// de faire les images (le livre ouvert vu un peu d'en haut, une icône de 32 × 32 pixels d'art par sort).
/// </para>
/// </summary>
public partial class SpellBookView : Control
{
    // ---- les tailles, en pixels d'art
    /// <summary>Le livre ouvert (couverture comprise).</summary>
    public const int BookWidth = 480, BookHeight = 288;
    private const int Cover = 8, Spine = 8;
    /// <summary>La tranche des pages, sous elles (l'image du livre la dessine).</summary>
    private const int PageEdges = 16;
    private const int PageWidth = (BookWidth - 2 * Cover - Spine) / 2, PageHeight = BookHeight - 2 * Cover - PageEdges;
    /// <summary>La marge du texte dans une page : le papier se courbe vers le dos, et l'image a un bord abîmé.</summary>
    private const int PageMargin = 16, HeaderHeight = 24, FooterHeight = 24;
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
        Gold = MapRenderer.Rgb(150, 104, 30), Spark = MapRenderer.Rgb(255, 226, 140);

    /// <summary>La couleur de chaque école (onglets, lueur, icônes d'attente) ; null : tous les sorts.</summary>
    private static readonly (string? School, Color Color)[] Schools =
    {
        (null, MapRenderer.Rgb(170, 132, 60)), ("eau", MapRenderer.Rgb(60, 110, 170)), ("feu", MapRenderer.Rgb(176, 64, 40)),
        ("terre", MapRenderer.Rgb(104, 120, 56)), ("air", MapRenderer.Rgb(150, 160, 176)),
    };

    private static Color SchoolColor(string? school) => Schools.FirstOrDefault(s => s.School == school).Color;

    /// <summary>
    /// Ce que montre le livre : ses sorts, le héros qui le lit (coûts, ce que font les sorts ; null : les coûts de base),
    /// ses points de sort (null : pas de ligne de points), et pour un grimoire de guilde, tous les niveaux et ceux qui sont
    /// construits (un niveau pas encore construit le dit sur sa page), les sorts déjà connus du héros étant marqués.
    /// </summary>
    public sealed record Content(IReadOnlyList<SpellType> Spells, Hero? Reader, int? Points,
        IReadOnlyList<int>? Levels = null, Func<int, bool>? LevelBuilt = null, bool MarkKnown = false);

    private readonly Content _book;
    private readonly Func<SpellType, bool>? _canCast;
    private readonly Action<SpellType>? _chosen;

    /// <summary>L'école montrée (null : toutes) et la double page ouverte (0 : les deux premières pages).</summary>
    private string? _school;
    private int _spread;

    /// <summary>Les pages, d'après l'école : un niveau par page (deux pages pour un niveau de plus de six sorts).</summary>
    private List<(int Level, List<SpellType> Spells, bool Built)> _pages = new();

    // Les couches : le livre (coupé pendant l'ouverture), puis par-dessus la couverture qui tourne, la page qui tourne et
    // les étincelles.
    private readonly Control _clip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
    private readonly Control _bookLayer = new() { MouseFilter = MouseFilterEnum.Pass };
    private readonly Control _content = new() { MouseFilter = MouseFilterEnum.Pass };
    private readonly Control _effects = new() { MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>L'ouverture du livre : le temps écoulé (0 à <see cref="OpenTime"/>).</summary>
    private float _open;
    private const float OpenTime = 0.5f;

    /// <summary>La page qui tourne : le temps écoulé (0 à <see cref="TurnTime"/>) et le sens (+1 : en avant).</summary>
    private float _turn = TurnTime;
    private int _turnDirection;
    private const float TurnTime = 0.35f;

    /// <summary>Développement : la page qui tourne figée à cet instant (« --book-turn »), l'ouverture figée (« --book-open »).</summary>
    private float? _frozenTurn, _frozenOpen;

    /// <summary>Les étincelles : place, vitesse (pixels de l'écran par seconde), âge et durée, couleur, taille (pixels d'art).</summary>
    private readonly List<(Vector2 At, Vector2 Speed, float Age, float Life, Color Color, int Size)> _sparks = new();
    private readonly Random _random = new(7);

    public SpellBookView() : this(null!, null, null) { } // pour Godot

    /// <param name="canCast">Ce sort peut-il être lancé maintenant ? (null : le livre se lit seulement)</param>
    /// <param name="chosen">Appelé avec le sort choisi.</param>
    public SpellBookView(Content book, Func<SpellType, bool>? canCast, Action<SpellType>? chosen)
    {
        _book = book;
        _canCast = canCast;
        _chosen = chosen;
        CustomMinimumSize = Size = TotalSize;
        MouseFilter = MouseFilterEnum.Stop;
        foreach (var layer in new[] { _clip, _bookLayer, _content, _effects })
            layer.Size = TotalSize;
        _bookLayer.Draw += DrawBook;
        _effects.Draw += DrawEffects;
        _bookLayer.AddChild(_content);
        _clip.AddChild(_bookLayer);
        AddChild(_clip);
        AddChild(_effects);
        if (book == null)
            return;
        // développement : « --book-school=feu » (un onglet), « --book-spread=1 » (une double page), « --book-turn=0.3 »
        // (la page qui tourne, figée de 0 à 1), « --book-open=0.3 » (l'ouverture, figée de 0 à 1 ; 1 : la gerbe)
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--book-school="))
                _school = arg["--book-school=".Length..];
            else if (arg.StartsWith("--book-spread=") && int.TryParse(arg["--book-spread=".Length..], out int spread))
                _spread = spread;
            else if (arg.StartsWith("--book-turn=") && TryFraction(arg["--book-turn=".Length..], out float turn))
                _frozenTurn = turn;
            else if (arg.StartsWith("--book-open=") && TryFraction(arg["--book-open=".Length..], out float open))
                _frozenOpen = open;
        }
        Rebuild();
        if (_frozenTurn is { } frozenTurn)
        {
            _open = OpenTime;
            _turnDirection = 1;
            _turn = frozenTurn * TurnTime;
            for (int i = 0; i < 30; i++)
                EmitTurnSparks();
        }
        if (_frozenOpen is { } frozenOpen)
        {
            _open = frozenOpen * OpenTime;
            if (frozenOpen >= 1)
            {
                Burst();
                AgeSparks(0.25f);
            }
        }
        LayoutOpening();
    }

    private static bool TryFraction(string text, out float value) =>
        float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);

    private static float A(float artPixels) => ArtPx(artPixels);

    private static Vector2 A(float x, float y) => new(ArtPx(x), ArtPx(y));

    /// <summary>La place d'une page dans le livre (0 : à gauche, 1 : à droite).</summary>
    private static Rect2 PageRect(int side) =>
        new(A(Cover + side * (PageWidth + Spine), Cover), A(PageWidth, PageHeight));

    /// <summary>Le milieu du dos du livre (en pixels de l'écran).</summary>
    private static float SpineX => A(Cover + PageWidth + Spine / 2f);

    private int SpreadCount => Math.Max(1, (_pages.Count + 1) / 2);

    /// <summary>Refait les pages d'après l'école choisie, puis la double page ouverte.</summary>
    private void Rebuild()
    {
        var levels = _book.Levels ?? _book.Spells.Select(s => s.Level).Distinct().OrderBy(l => l).ToList();
        _pages = new();
        foreach (int level in levels)
        {
            bool built = _book.LevelBuilt?.Invoke(level) ?? true;
            var spells = _book.Spells.Where(s => s.Level == level && (_school == null || s.School == _school)).OrderBy(s => s.Name).ToList();
            if (!built)
                _pages.Add((level, new List<SpellType>(), false));
            else if (spells.Count > 0)
                _pages.AddRange(spells.Chunk(SpellsPerPage).Select(chunk => (level, chunk.ToList(), true)));
        }
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
                _content.AddChild(InkLabel(_book.Spells.Count == 0 ? Texts.NoSpellsKnown : Texts.NoSpellOfSchool, 12, SoftInk,
                    page.Position + A(PageMargin, HeaderHeight), PageWidth - 2 * PageMargin, wrap: true));
        }
        // les points de sort, en bas de la page de gauche ; les coins pour tourner, en bas de chaque page
        var left = PageRect(0);
        if (_book.Points is { } points && _book.Reader is { } reader)
            _content.AddChild(InkLabel(Texts.SpellPointsLine(points, reader.MaxSpellPoints), 12, Ink,
                left.Position + A(PageMargin, PageHeight - FooterHeight + 8), PageWidth - 2 * PageMargin, center: true));
        if (_spread > 0)
            _content.AddChild(Corner(left.Position + A(0, PageHeight - 20), -1));
        if (_spread < SpreadCount - 1)
            _content.AddChild(Corner(PageRect(1).End - A(20, 20), +1));
        AddTabs();
        _bookLayer.QueueRedraw();
    }

    /// <summary>Une page : le niveau en titre, puis ses sorts en deux colonnes (ou « pas encore construit »).</summary>
    private void FillPage(Rect2 page, (int Level, List<SpellType> Spells, bool Built) content)
    {
        _content.AddChild(InkLabel(Texts.GuildLevel(content.Level), 20, Gold, page.Position + A(0, 6), PageWidth, center: true));
        if (!content.Built)
        {
            _content.AddChild(InkLabel(Texts.GuildLevelMissing, 12, SoftInk, page.Position + A(PageMargin, HeaderHeight + 8),
                PageWidth - 2 * PageMargin, center: true));
            return;
        }
        for (int i = 0; i < content.Spells.Count; i++)
        {
            var at = page.Position + A(PageMargin + i % Columns * CellWidth, HeaderHeight + i / Columns * CellHeight);
            _content.AddChild(SpellCell(content.Spells[i], at));
        }
    }

    /// <summary>
    /// Un sort : son icône (en attendant l'image : un carré de la couleur de son école, ses initiales), son nom, son coût.
    /// Au combat, un clic le choisit ; un sort qu'on ne peut pas lancer est pâli. Ce qu'il fait au survol, où il luit.
    /// </summary>
    private Control SpellCell(SpellType spell, Vector2 at)
    {
        var reader = _book.Reader;
        int points = _book.Points ?? int.MaxValue;
        bool castable = _canCast?.Invoke(spell) == true;
        bool dim = _canCast != null && !castable;
        var cell = new Control
        {
            Position = at, Size = A(CellWidth, CellHeight),
            TooltipText = reader != null ? Texts.SpellTooltip(spell, reader, points) : Texts.SpellLine(spell),
            MouseFilter = MouseFilterEnum.Stop, Modulate = dim ? new Color(1, 1, 1, 0.45f) : Colors.White,
            MouseDefaultCursorShape = castable ? CursorShape.PointingHand : CursorShape.Arrow,
        };
        var icon = new SpellIcon
        {
            Picture = Art.Get($"spells/{spell.Id}"), Color = SchoolColor(spell.School), Initials = Initials(spell.Name),
            Known = _book.MarkKnown && reader?.Knows(spell) == true,
            Position = A((CellWidth - IconSize) / 2f, 0), Size = A(IconSize, IconSize),
        };
        cell.AddChild(icon);
        // le nom (deux lignes au plus) puis le coût, juste dessous (dans un grimoire de guilde, une coche sur l'icône
        // d'un sort que le héros connaît : « connu » en texte débordait sur la colonne voisine)
        var lines = new VBoxContainer { Position = A(0, IconSize + 2), Size = A(CellWidth, 0), MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", 0);
        var name = InkLabel(spell.Name, 12, Ink, Vector2.Zero, CellWidth, center: true, wrap: true);
        name.MaxLinesVisible = 2;
        lines.AddChild(name);
        bool tooDear = reader != null && reader.SpellCost(spell) > points;
        lines.AddChild(InkLabel(Texts.SpellCost(spell, reader), 11, tooDear ? MapRenderer.Rgb(150, 40, 30) : SoftInk, Vector2.Zero, CellWidth, center: true));
        cell.AddChild(lines);
        // développement : « --book-hover=id » montre la lueur de ce sort
        if (OS.GetCmdlineUserArgs().Contains($"--book-hover={spell.Id}"))
            icon.Highlight = true;
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

    /// <summary>Un texte à l'encre sur le parchemin (le texte après la taille : la largeur reste celle voulue).</summary>
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
        foreach (var state in new[] { "normal", "hover", "pressed" })
            corner.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
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
        if (next == _spread || _open < OpenTime)
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

    // ------------------------------------------------------------------ l'ouverture, la page qui tourne, les étincelles

    /// <summary>
    /// L'ouverture : d'abord le livre fermé (sa couverture sur la moitié droite) dont la couverture se soulève vers le dos
    /// et découvre la page de droite ; puis elle se couche à gauche, et la page de gauche apparaît à sa place. Le livre est
    /// coupé (<see cref="_clip"/>) à ce qui est découvert.
    /// </summary>
    private void LayoutOpening()
    {
        float t = Math.Clamp(_open / OpenTime, 0, 1);
        float left = t < 0.5f ? SpineX : Snap(SpineX * MathF.Abs(MathF.Cos(MathF.PI * t)));
        if (t >= 1)
            left = 0;
        _clip.Position = new Vector2(left, 0);
        _clip.Size = new Vector2(TotalSize.X - left, TotalSize.Y);
        _bookLayer.Position = new Vector2(-left, 0);
        _effects.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool frozen = _frozenTurn != null || _frozenOpen != null;
        if (_open < OpenTime && !frozen)
        {
            _open = Math.Min(OpenTime, _open + dt);
            LayoutOpening();
            if (_open >= OpenTime)
                Burst();
        }
        if (_turn < TurnTime && !frozen)
        {
            _turn = Math.Min(TurnTime, _turn + dt);
            EmitTurnSparks();
        }
        if (!frozen)
            AgeSparks(dt);
        _effects.QueueRedraw();
    }

    /// <summary>La gerbe de l'ouverture : des étincelles d'or et de blanc qui montent du dos du livre.</summary>
    private void Burst()
    {
        for (int i = 0; i < 90; i++)
        {
            var at = new Vector2(SpineX + (float)(_random.NextDouble() - 0.5) * A(200), A(BookHeight / 2f) + (float)(_random.NextDouble() - 0.5) * A(200));
            var speed = new Vector2((float)(_random.NextDouble() - 0.5) * A(90), -A(40) - (float)_random.NextDouble() * A(90));
            // de l'or et du blanc, bordés d'un or plus sombre pour se voir sur le parchemin (DrawEffects)
            var color = _random.Next(3) == 0 ? Colors.White : Spark;
            _sparks.Add((at, speed, 0, 0.6f + (float)_random.NextDouble() * 0.7f, color, 1 + _random.Next(3)));
        }
    }

    /// <summary>Des étincelles le long du bord de la page qui tourne, de la couleur de l'école montrée (l'or pour tous).</summary>
    private void EmitTurnSparks()
    {
        float t = _turn / TurnTime;
        float width = A(PageWidth) * MathF.Abs(MathF.Cos(MathF.PI * t));
        bool onRight = (_turnDirection > 0) == (t < 0.5f);
        float edge = onRight ? SpineX + width : SpineX - width;
        var color = _school == null ? Spark : SchoolColor(_school).Lightened(0.4f);
        for (int i = 0; i < 2; i++)
        {
            var at = new Vector2(edge, A(Cover) + (float)_random.NextDouble() * A(PageHeight));
            var speed = new Vector2((float)(_random.NextDouble() - 0.5) * A(30), -A(10) - (float)_random.NextDouble() * A(30));
            _sparks.Add((at, speed, 0, 0.3f + (float)_random.NextDouble() * 0.4f, color, 1));
        }
    }

    private void AgeSparks(float dt)
    {
        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += dt;
            s.At += s.Speed * dt;
            if (s.Age >= s.Life)
                _sparks.RemoveAt(i);
            else
                _sparks[i] = s;
        }
    }

    /// <summary>Par-dessus le livre : la couverture qui se soulève, la lueur de l'ouverture, la page qui tourne, les étincelles.</summary>
    private void DrawEffects()
    {
        float t = Math.Clamp(_open / OpenTime, 0, 1);
        // la couverture : sur la page de droite, elle se rétrécit vers le dos
        if (t < 0.5f)
        {
            float width = Snap(A(PageWidth + Cover + Spine / 2f) * MathF.Cos(MathF.PI * t));
            var cover = new Rect2(SpineX, 0, width, A(BookHeight));
            _effects.DrawRect(cover, Leather);
            _effects.DrawRect(cover, LeatherDark, false, A(2));
            // le fer doré au milieu de la couverture
            if (width > A(40))
            {
                var mark = new Rect2(SnapDown(cover.Position.X + cover.Size.X / 2 - A(12)), A(BookHeight / 2f - 16), A(24), A(32));
                _effects.DrawRect(mark, Gold, false, A(2));
                _effects.DrawRect(mark.Grow(-A(6)), Gold.Darkened(0.2f));
            }
        }
        // la lueur de l'ouverture : des rectangles de lumière autour du dos, qui s'effacent avec les étincelles
        if (_open >= OpenTime && _sparks.Count > 0 && _turn >= TurnTime)
        {
            // des rectangles de lumière posés l'un sur l'autre : plus vive au dos, qui s'éteint avec les étincelles
            float glow = Math.Clamp(1 - _sparks.Average(s => s.Age / s.Life), 0, 1);
            for (int ring = 5; ring >= 0; ring--)
            {
                float w = A(24 + ring * 48), h = A(BookHeight - 60 + ring * 12);
                _effects.DrawRect(new Rect2(SnapDown(SpineX - w / 2), SnapDown(A(BookHeight / 2f) - h / 2), w, h),
                    new Color(Spark, glow * 0.14f));
            }
        }
        // la page qui tourne : une feuille qui se rétrécit jusqu'au dos puis s'ouvre de l'autre côté (son envers plus sombre)
        if (_turn < TurnTime && _turnDirection != 0)
        {
            float u = _turn / TurnTime;
            float width = Snap(A(PageWidth) * MathF.Abs(MathF.Cos(MathF.PI * u)));
            bool onRight = (_turnDirection > 0) == (u < 0.5f);
            var sheet = new Rect2(onRight ? SpineX : SpineX - width, A(Cover), width, A(PageHeight));
            _effects.DrawRect(sheet, u < 0.5f ? Parchment : Parchment.Darkened(0.12f));
            _effects.DrawRect(sheet, ParchmentEdge.Darkened(0.3f), false, Grid);
        }
        // les étincelles : des carrés d'un ou deux pixels d'art, sur la grille, qui pâlissent
        foreach (var s in _sparks)
        {
            float alpha = 1 - s.Age / s.Life;
            var at = new Vector2(SnapDown(s.At.X), SnapDown(s.At.Y));
            var size = new Vector2(Grid * s.Size, Grid * s.Size);
            // un liseré d'or sombre autour des plus grosses : elles se voient sur le parchemin clair
            if (s.Size > 1)
                _effects.DrawRect(new Rect2(at - new Vector2(Grid, Grid), size + new Vector2(2 * Grid, 2 * Grid)), new Color(Gold, alpha * 0.6f));
            _effects.DrawRect(new Rect2(at, size), new Color(s.Color, alpha));
        }
    }

    // ------------------------------------------------------------------ le livre

    /// <summary>L'image du livre ouvert (<c>art/ui/livre-ouvert.png</c>, 480 × 288 : la couverture, les pages blanches,
    /// leur tranche), à la place du livre dessiné.</summary>
    private static readonly Texture2D? Image = Art.Get("ui/livre-ouvert");

    private void DrawBook()
    {
        if (Image != null)
        {
            _bookLayer.DrawTextureRect(Image, new Rect2(Vector2.Zero, A(BookWidth, BookHeight)), false);
            return;
        }
        // la couverture de cuir, un liseré clair, le dos plus sombre
        var book = new Rect2(Vector2.Zero, A(BookWidth, BookHeight));
        _bookLayer.DrawRect(book, Leather);
        _bookLayer.DrawRect(book, LeatherDark, false, A(2));
        _bookLayer.DrawRect(new Rect2(A(3, 3), A(BookWidth - 6, BookHeight - 6)), LeatherLight, false, Grid);
        _bookLayer.DrawRect(new Rect2(A(Cover + PageWidth, 0), A(Spine, BookHeight)), LeatherDark);
        // les deux pages, plus sombres vers le dos (la courbure du papier) et sur leur bord
        for (int side = 0; side < 2; side++)
            DrawPage(_bookLayer, PageRect(side), side);
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
}
