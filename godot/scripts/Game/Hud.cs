using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'interface de la carte (étape 7), entièrement construite par le code : la barre des ressources en haut, la bande
/// sous la carte (ce qui est survolé, le dernier message, le journal), et la colonne de droite (carte réduite, listes
/// des héros et des villes, héros choisi, son armée, boutons d'action, fin du tour). Aussi les fenêtres de la carte.
/// </summary>
public partial class Hud : CanvasLayer
{
    /// <summary>Largeur de la colonne de droite et hauteurs des bandes, en pixels de l'écran (1920 × 1080).</summary>
    public const int PanelWidth = 384;
    public const int TopBarHeight = 66;

    /// <summary>Hauteur de la bande sous la carte (survol et dernier message, sur deux lignes).</summary>
    public const int BottomBarHeight = 78;

    private GameState _state = null!;

    /// <summary>Le joueur qui regarde l'écran (le joueur humain) : le panneau montre toujours ses héros et ses villes.</summary>
    private Player Viewer => _state.Players[0];

    private Hero? _lastHero;

    /// <summary>
    /// Le héros montré par le panneau : le héros choisi du joueur, et pendant le tour d'un adversaire (qui attend une
    /// défense), le dernier qu'il avait choisi.
    /// </summary>
    public Hero? ShownHero
    {
        get
        {
            if (_state.CurrentPlayer == Viewer)
                return _lastHero = _state.CurrentHero;
            return _lastHero is { } hero && Viewer.Heroes.Contains(hero) ? hero : Viewer.Heroes.FirstOrDefault();
        }
    }
    private ResourceBar _resourceBar = null!;

    // le héros choisi
    private TextureRect _portrait = null!;
    private Label _heroName = null!;
    private Label _heroLevel = null!;
    private readonly Dictionary<HeroStat, Label> _statLabels = new();
    private Label _charisma = null!;
    private ProgressBar _movementBar = null!;
    private Label _movementText = null!;
    private HBoxContainer _artifacts = null!;
    private string? _artifactsKey;
    private ArmySlots _army = null!;

    // les listes
    private GridContainer _heroRows = null!;
    private GridContainer _townRows = null!;
    private Button _heroesUp = null!, _heroesDown = null!, _townsUp = null!, _townsDown = null!;
    private int _heroesOffset, _townsOffset;
    private string? _heroesKey, _townsKey;

    /// <summary>
    /// Les listes, comme Heroes 2 : des portraits sans nom (le détail au survol), deux par rangée, trois rangées visibles
    /// (les autres avec les flèches). Une case : le portrait de 64 pixels et, à côté, deux barres (mouvement, points de
    /// sort) ou la marque « on peut construire aujourd'hui ».
    /// </summary>
    private const int ListRows = 3, ListColumns = 2;
    private const int RowHeight = 68, CellWidth = 80;

    /// <summary>Le nombre de héros ou de villes visibles à la fois dans une liste.</summary>
    private const int ListCells = ListRows * ListColumns;

    // la bande du bas
    private Label _hover = null!;
    private Label _lastMessage = null!;
    private readonly List<string> _messages = new();
    private const int MaxMessages = 60;
    private (Cell? Cell, int HeroId, Cell HeroAt, int Movement, int Objects)? _hoverKey;
    private string _hoverText = "";

    // Prévision de combat mise en cache : la simulation n'est refaite que si quelque chose change
    private (Army Enemy, int EnemyCount, int ArmyCount)? _predictionKey;
    private string _predictionText = "";

    public Minimap Minimap { get; private set; } = null!;

    /// <summary>La hauteur de la carte réduite au plus (en unités de l'interface : 240 pixels de l'écran).</summary>
    private const float MinimapMaxHeight = 160;
    public event Action? EndTurnPressed;
    public event Action? SavePressed;
    public event Action? DigPressed;
    public event Action? TreasureMapPressed;
    public event Action? LoadPressed;

    /// <summary>Le joueur quitte le jeu (après confirmation).</summary>
    public event Action? QuitPressed;

    /// <summary>Le joueur revient au menu principal (après confirmation).</summary>
    public event Action? MainMenuPressed;
    public event Action? NextHeroPressed;
    public event Action? WalkPressed;
    public event Action? CenterPressed;
    public event Action? KingdomPressed;
    public event Action? SpellBookPressed;

    /// <summary>Le joueur veut l'écran d'un héros (clic sur le portrait, double clic dans la liste).</summary>
    public event Action<Hero>? HeroDetailsPressed;

    /// <summary>Le joueur a cliqué sur une de ses villes dans la liste.</summary>
    public event Action<Town>? TownClicked;

    /// <summary>Le joueur a cliqué sur un de ses héros dans la liste.</summary>
    public event Action<Hero>? HeroClicked;

    /// <summary>
    /// Construit l'interface de la carte. Les positions sont en pixels de l'écran ; les écarts et les tailles fixes sont
    /// écrits dans les unités de l'interface (<see cref="Ui.Px"/>, <see cref="Ui.V"/>).
    /// </summary>
    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        float panelX = screenSize.X - PanelWidth;

        // ---- Barre des ressources, en haut de la carte
        _resourceBar = new ResourceBar();
        AddChild(_resourceBar);
        _resourceBar.Build(panelX, TopBarHeight);

        BuildBottomBar(screenSize, panelX);

        // ---- Colonne de droite
        var panel = new Panel { Position = new Vector2(panelX, 0), Size = new Vector2(PanelWidth, screenSize.Y) };
        panel.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        AddChild(panel);
        float margin = Px(8);
        float inner = PanelWidth - 2 * margin;

        // la carte réduite : des cases d'un nombre entier de pixels, pas plus haute que celle des cartes de 54 × 40 (les
        // cartes carrées de Heroes 3 poussaient le bas de la colonne hors de l'écran)
        int scale = (int)Math.Min(Math.Min((PanelWidth - Px(24)) / state.Map.Width, Px(MinimapMaxHeight) / state.Map.Height), 9);
        var minimapSize = new Vector2(state.Map.Width, state.Map.Height) * scale;
        var minimapFrame = new Panel
        {
            Position = new Vector2((PanelWidth - minimapSize.X) / 2 - Px(2), Px(10)),
            Size = minimapSize + V(4, 4),
        };
        minimapFrame.AddThemeStyleboxOverride("panel", Box(Colors.Black, SilverColor, 2, 2, 2, 2));
        panel.AddChild(minimapFrame);
        Minimap = new Minimap { State = state, CellSize = scale, Position = V(2, 2), Size = minimapSize, ClipContents = true };
        minimapFrame.AddChild(Minimap);
        float y = minimapFrame.Position.Y + minimapFrame.Size.Y + Px(8);

        // les listes des héros et des villes, côte à côte (comme Heroes 2)
        float listWidth = (inner - Px(6)) / 2;
        float listHeight = ListHeader + ListRows * (RowHeight + Grid) + Px(4);
        (_heroRows, _heroesUp, _heroesDown) = BuildList(panel, Texts.HeroesHeader, margin, y, listWidth, listHeight, step =>
        {
            _heroesOffset += step;
            _heroesKey = null;
        });
        (_townRows, _townsUp, _townsDown) = BuildList(panel, Texts.TownsHeader, margin + listWidth + Px(6), y, listWidth, listHeight, step =>
        {
            _townsOffset += step;
            _townsKey = null;
        });
        y += listHeight + Px(8);

        y = BuildHeroCard(panel, margin, y, inner) + Px(6);

        // son armée, en 7 emplacements (petites images ×1)
        panel.AddChild(At(MakeLabel(Texts.ArmyHeader, 12, SoftTextColor), margin / Ui.Scale, y / Ui.Scale));
        y += Px(16);
        // des emplacements de largeur entière (sinon les arrondis débordent du panneau)
        float gap = Px(2);
        float slot = MathF.Floor((inner - 6 * gap) / Army.MaxStacks);
        _army = new ArmySlots
        {
            Position = new Vector2(margin + (inner - 6 * gap - slot * Army.MaxStacks) / 2, y),
            SlotSize = new Vector2(slot, Px(52)), Compact = true,
        };
        panel.AddChild(_army);
        y += Px(52) + Px(8);

        // les boutons d'action : des boutons carrés de pierre à icône (comme au combat), deux rangées de quatre ; le nom
        // et la touche au survol
        var actions = new (string Icon, string Key, string Tooltip, Action Pressed)[]
        {
            ("suivant", "H", Texts.NextHeroButton, () => NextHeroPressed?.Invoke()),
            ("avancer", "Espace", Texts.WalkTooltip, () => WalkPressed?.Invoke()),
            ("centrer", "C", Texts.CenterTooltip, () => CenterPressed?.Invoke()),
            ("royaume", "K", Texts.KingdomTitle, () => KingdomPressed?.Invoke()),
            ("creuser", "F", Texts.DigTooltip, () => DigPressed?.Invoke()),
            ("tresor", "M", Texts.TreasureMapAction, () => TreasureMapPressed?.Invoke()),
            ("livre", "S", Texts.SpellBookTooltip, () => SpellBookPressed?.Invoke()),
            ("options", "Échap", Texts.OptionsTooltip, ShowOptions),
        };
        // les boutons à icône ont la taille commune, centrés dans la colonne
        const int columns = 4;
        float side = IconButtonSide;
        float buttonsX = margin + SnapDown((inner - columns * side - (columns - 1) * ButtonGap) / 2);
        for (int i = 0; i < actions.Length; i++)
        {
            var (icon, key, tooltip, pressed) = actions[i];
            var button = IconButtons.Make(icon, tooltip, key, pressed);
            button.Position = new Vector2(buttonsX + i % columns * (side + ButtonGap), y + i / columns * (side + ButtonGap));
            panel.AddChild(button);
        }
        y += (actions.Length + columns - 1) / columns * (side + ButtonGap);

        // la fin du tour : un grand bouton, toujours au même endroit, en bas de la colonne
        var endTurn = MakeButton(Texts.EndTurnButton, 18);
        endTurn.TooltipText = Texts.KeyTooltip(Texts.EndTurnButton, "Entrée");
        Sized(endTurn, inner, LargeButtonHeight);
        endTurn.Position = new Vector2(margin, Math.Max(y, Snap(screenSize.Y - Px(10) - LargeButtonHeight)));
        endTurn.Pressed += () => EndTurnPressed?.Invoke();
        panel.AddChild(endTurn);

        if (state.CurrentHero is { } hero)
            AddMessage(Texts.Welcome(hero));
    }

    /// <summary>Hauteur du haut d'une liste : le titre et les flèches.</summary>
    private static float ListHeader => SmallButtonHeight + Px(4);

    /// <summary>Une liste (héros ou villes) : un titre, des flèches si elle est trop longue, et ses cases (en pixels).</summary>
    private static (GridContainer Rows, Button Up, Button Down) BuildList(Control parent, string title, float x, float y,
        float width, float height, Action<int> scroll)
    {
        var box = new Panel { Position = new Vector2(x, y), Size = new Vector2(width, height) };
        box.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        parent.AddChild(box);
        var heading = MakeLabel(title, 12, SoftTextColor);
        heading.Position = new Vector2(Px(4), Px(2) + SnapDown((SmallButtonHeight - NormalText) / 2));
        box.AddChild(heading);
        Button Arrow(string text, float ax, int step)
        {
            var arrow = Sized(MakeButton(text, 10), SmallButtonHeight, SmallButtonHeight);
            arrow.Position = new Vector2(ax, Px(2));
            arrow.Pressed += () => scroll(step);
            box.AddChild(arrow);
            return arrow;
        }
        var down = Arrow("▼", width - Px(2) - SmallButtonHeight, +1);
        var up = Arrow("▲", down.Position.X - Px(2) - SmallButtonHeight, -1);
        // les cases centrées dans la largeur de la liste
        float cellsWidth = ListColumns * CellWidth + (ListColumns - 1) * Grid;
        var rows = new GridContainer
        {
            Columns = ListColumns, Position = new Vector2(SnapDown((width - cellsWidth) / 2), ListHeader),
            Size = new Vector2(cellsWidth, height - ListHeader),
        };
        rows.AddThemeConstantOverride("h_separation", Grid);
        rows.AddThemeConstantOverride("v_separation", Grid);
        box.AddChild(rows);
        return (rows, up, down);
    }

    /// <summary>
    /// La fiche du héros choisi (en pixels) : portrait ×2 à gauche ; à droite le nom, le niveau, les caractéristiques, le
    /// mouvement et les artefacts. Renvoie le bas.
    /// </summary>
    private float BuildHeroCard(Control panel, float x, float y, float width)
    {
        const int portraitSize = 128; // le portrait de 64 pixels, ×2
        var card = new Panel { Position = new Vector2(x, y), Size = new Vector2(width, portraitSize + Px(12)) };
        card.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        panel.AddChild(card);
        var portraitFrame = new Panel { Position = V(4, 4), Size = new Vector2(portraitSize, portraitSize) + V(4, 4) };
        portraitFrame.AddThemeStyleboxOverride("panel", Box(Colors.Black, SilverColor, 2, 2, 2, 2));
        card.AddChild(portraitFrame);
        _portrait = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = V(2, 2), Size = new Vector2(portraitSize, portraitSize),
        };
        portraitFrame.AddChild(_portrait);
        // un clic sur le portrait ouvre l'écran du héros
        portraitFrame.MouseFilter = Control.MouseFilterEnum.Stop;
        portraitFrame.TooltipText = Texts.HeroScreenHint;
        portraitFrame.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && ShownHero is { } shown)
                HeroDetailsPressed?.Invoke(shown);
        };

        float textX = portraitFrame.Size.X + Px(12), textWidth = width - textX - Px(4);
        _heroName = MakeLabel("", 16, TextColor);
        _heroName.Position = new Vector2(textX, Px(2));
        _heroName.ClipText = true;
        _heroName.Size = new Vector2(textWidth, Px(22));
        card.AddChild(_heroName);
        _heroLevel = MakeLabel("", 12, SoftTextColor);
        _heroLevel.Position = new Vector2(textX, Px(20));
        card.AddChild(_heroLevel);

        // les caractéristiques, chacune expliquée au survol : Att, Déf sur une ligne ; Pui, Sav ; puis Cha
        var stats = new (HeroStat? Stat, int Column, int Row)[]
        {
            (HeroStat.Attack, 0, 0), (HeroStat.Defense, 1, 0), (HeroStat.Power, 0, 1), (HeroStat.Knowledge, 1, 1), (null, 0, 2),
        };
        float column = textWidth / 2;
        foreach (var (stat, col, row) in stats)
        {
            var label = MakeLabel("", 12, TextColor);
            label.Position = new Vector2(textX + col * column, Px(34 + row * 14));
            label.MouseFilter = Control.MouseFilterEnum.Stop;
            card.AddChild(label);
            if (stat is { } s)
                _statLabels[s] = label;
            else
                _charisma = label;
        }

        _movementBar = new ProgressBar
        {
            Position = new Vector2(textX, Px(80)), Size = new Vector2(textWidth, Px(14)),
            ShowPercentage = false, MinValue = 0, MaxValue = Hero.DefaultMovement,
        };
        StyleBar(_movementBar, MovementColor);
        card.AddChild(_movementBar);
        _movementBar.Size = new Vector2(textWidth, Px(14)); // une fois stylée et posée : avant, elle gardait la hauteur minimale du thème
        _movementText = MakeLabel("1500 / 1500", 10, TextColor);
        _movementText.HorizontalAlignment = HorizontalAlignment.Center;
        // centré sur la barre : la ligne de texte est un peu plus haute qu'elle
        float textHeight = _movementText.GetCombinedMinimumSize().Y;
        _movementText.Position = _movementBar.Position + new Vector2(0, SnapDown((_movementBar.Size.Y - textHeight) / 2));
        _movementText.Size = new Vector2(_movementBar.Size.X, textHeight);
        card.AddChild(_movementText);

        // ses artefacts : de petites images à côté du charisme (le nom et l'effet au survol)
        _artifacts = new HBoxContainer { Position = new Vector2(textX + column, Px(61)), Size = new Vector2(column, Px(16)) };
        _artifacts.AddThemeConstantOverride("separation", (int)Px(2));
        card.AddChild(_artifacts);
        return y + card.Size.Y;
    }

    /// <summary>La bande sous la carte (en pixels) : à gauche ce qui est survolé, à droite le dernier message et le journal.</summary>
    private void BuildBottomBar(Vector2 screenSize, float width)
    {
        var bar = new Panel { Position = new Vector2(0, screenSize.Y - BottomBarHeight), Size = new Vector2(width, BottomBarHeight) };
        bar.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        AddChild(bar);
        float half = (width - Px(32 + 90)) / 2;
        _hover = MakeLabel("", 13, SoftTextColor);
        _hover.Position = V(14, 6);
        _hover.ClipText = true;
        _hover.Size = new Vector2(half, BottomBarHeight - Px(12));
        _hover.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_hover);
        _lastMessage = MakeLabel("", 13, TextColor);
        _lastMessage.Position = new Vector2(Px(14 + 8) + half, Px(6));
        // le retour à la ligne d'abord : sinon la largeur se règle sur le texte entier
        _lastMessage.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _lastMessage.CustomMinimumSize = _lastMessage.Size = new Vector2(half - Px(8), BottomBarHeight - Px(12));
        _lastMessage.HorizontalAlignment = HorizontalAlignment.Right;
        _lastMessage.VerticalAlignment = VerticalAlignment.Center;
        _lastMessage.ClipText = true;
        bar.AddChild(_lastMessage);
        var log = Sized(MakeButton(Texts.LogButton, 13), Px(84), ButtonHeight);
        log.Position = new Vector2(width - Px(14) - log.Size.X, SnapDown((BottomBarHeight - ButtonHeight) / 2f));
        // le message s'arrête avant le bouton
        _lastMessage.Size = _lastMessage.CustomMinimumSize = new Vector2(log.Position.X - Px(12) - _lastMessage.Position.X, _lastMessage.Size.Y);
        log.Pressed += ShowLog;
        bar.AddChild(log);
    }

    /// <summary>Met à jour l'interface. Appelé à chaque image : ce qui coûte (listes, survol) n'est refait qu'au changement.</summary>
    public void Refresh(Cell? hovered)
    {
        var player = Viewer;
        _resourceBar.Refresh(_state, player);

        if (ShownHero is { } hero)
        {
            _heroName.Text = hero.Name;
            _heroLevel.Text = Texts.HeroLevelLine(hero);
            _portrait.Texture = Art.Portrait(hero.Name);
            foreach (var (stat, label) in _statLabels)
            {
                label.Text = $"{Texts.StatShort(stat)} {StatValue(hero, stat)}";
                label.TooltipText = Texts.StatTooltip(stat, hero);
            }
            _charisma.Text = $"{Texts.StatShort(null)} {hero.Charisma}";
            _charisma.TooltipText = Texts.StatTooltip(null, hero);
            RefreshArtifacts(hero);
            _movementBar.MaxValue = hero.MaxMovement;
            _movementBar.Value = hero.Movement;
            _movementText.Text = $"{hero.Movement} / {hero.MaxMovement}";
            _army.Show(hero.Army, hero.CommandLimit, hero);
        }
        else
        {
            _heroName.Text = Texts.NoHero;
            _heroLevel.Text = "";
            _portrait.Texture = null;
            foreach (var label in _statLabels.Values.Append(_charisma))
                label.Text = label.TooltipText = "";
            RefreshArtifacts(null);
            _movementBar.Value = 0;
            _movementText.Text = "";
            _army.Show(null, null);
        }

        RefreshHeroes(player);
        RefreshTowns(player);
        _hover.Text = DescribeCell(hovered);
        string last = _messages.Count > 0 ? _messages[^1] : "";
        if (_lastMessage.Text != last)
            _lastMessage.Text = last;
    }

    private static int StatValue(Hero hero, HeroStat stat) => stat switch
    {
        HeroStat.Attack => hero.Attack,
        HeroStat.Defense => hero.Defense,
        HeroStat.Power => hero.Power,
        _ => hero.Knowledge,
    };

    /// <summary>
    /// La liste des héros : portrait, et à sa droite deux barres qui se vident vers le bas (comme Heroes 2) : le mouvement
    /// (vert) et les points de sort (bleu). Le héros choisi est bordé de clair ; le détail au survol.
    /// </summary>
    private void RefreshHeroes(Player player)
    {
        _heroesOffset = ClampOffset(_heroesOffset, player.Heroes.Count);
        var shown = player.Heroes.Skip(_heroesOffset * ListColumns).Take(ListCells).ToList();
        string key = string.Join("|", shown.Select(h => $"{h.Id}:{h.Movement}:{h.SpellPoints}")) + $"#{ShownHero?.Id}#{player.Heroes.Count}";
        if (key == _heroesKey)
            return;
        _heroesKey = key;
        Clear(_heroRows);
        foreach (var hero in shown)
        {
            var cell = Cell(hero == ShownHero, Texts.HeroTooltip(hero), () => HeroClicked?.Invoke(hero),
                () => HeroDetailsPressed?.Invoke(hero));
            if (Art.Portrait(hero.Name) is { } portrait)
                cell.AddChild(Picture(portrait, new Vector2(Grid, Grid), new Vector2(64, 64)));
            AddBar(cell, 0, hero.Movement, hero.MaxMovement, MovementColor);
            AddBar(cell, 1, hero.SpellPoints, hero.MaxSpellPoints, SpellPointsColor);
            _heroRows.AddChild(cell);
        }
        _heroesUp.Visible = _heroesDown.Visible = player.Heroes.Count > ListCells;
    }

    /// <summary>Le bleu des points de sort (la barre à côté du mouvement).</summary>
    private static readonly Color SpellPointsColor = MapRenderer.Rgb(70, 110, 190);

    /// <summary>Une barre verticale à droite du portrait (<paramref name="index"/> : la première ou la seconde), qui se vide vers le bas.</summary>
    private static void AddBar(Control cell, int index, int value, int max, Color color)
    {
        float x = Grid + 64 + Grid + index * (Grid * 3), height = 64;
        cell.AddChild(new ColorRect
        {
            Position = new Vector2(x, Grid), Size = new Vector2(Grid * 2, height), Color = MapRenderer.Rgb(20, 16, 12),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        float filled = Snap(height * Math.Clamp(value, 0, Math.Max(1, max)) / Math.Max(1, max));
        cell.AddChild(new ColorRect
        {
            Position = new Vector2(x, Grid + height - filled), Size = new Vector2(Grid * 2, filled), Color = color,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    /// <summary>
    /// La liste des villes : l'image de la ville et, à côté, une marque verte si l'on peut encore y construire aujourd'hui ;
    /// un clic ouvre la ville, le détail au survol.
    /// </summary>
    private void RefreshTowns(Player player)
    {
        _townsOffset = ClampOffset(_townsOffset, player.Towns.Count);
        var shown = player.Towns.Skip(_townsOffset * ListColumns).Take(ListCells).ToList();
        string key = string.Join("|", shown.Select(t => $"{t.Id}:{t.DailyGold}:{t.BuiltToday}")) + $"#{player.Towns.Count}";
        if (key == _townsKey)
            return;
        _townsKey = key;
        Clear(_townRows);
        if (player.Towns.Count == 0)
            _townRows.AddChild(MakeLabel(Texts.NoTown, 12, SoftTextColor));
        foreach (var town in shown)
        {
            var cell = Cell(false, Texts.TownDetails(town), () => TownClicked?.Invoke(town));
            if (Art.Town(town.Faction) is { } image)
                cell.AddChild(Picture(TownThumbnail(image), new Vector2(Grid, Grid), new Vector2(64, 64)));
            if (!town.BuiltToday)
                cell.AddChild(new ColorRect
                {
                    Position = new Vector2(Grid + 64 + Grid * 2, Grid * 2), Size = new Vector2(Grid * 3, Grid * 3), Color = GoodColor,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                });
            _townRows.AddChild(cell);
        }
        _townsUp.Visible = _townsDown.Visible = player.Towns.Count > ListCells;
    }

    /// <summary>La première rangée montrée (une rangée de deux à chaque flèche), sans dépasser la dernière.</summary>
    private static int ClampOffset(int offset, int count)
    {
        int rows = (count + ListColumns - 1) / ListColumns;
        return Math.Clamp(offset, 0, Math.Max(0, rows - ListRows));
    }

    private static readonly Dictionary<Texture2D, Texture2D> TownThumbnails = new();

    /// <summary>
    /// L'image d'une ville dans la liste : 64 × 64 pixels pris dans sa partie dessinée, ×1 (l'image entière, de 96, ne
    /// tient pas ; la réduire rendrait les pixels inégaux). Centrée et calée en bas : la porte et les remparts restent.
    /// </summary>
    private static Texture2D TownThumbnail(Texture2D image)
    {
        const float side = 64;
        if (image.GetWidth() <= side && image.GetHeight() <= side)
            return image;
        if (!TownThumbnails.TryGetValue(image, out var thumbnail))
        {
            var used = UsedRect(image);
            float x = Math.Clamp(MathF.Floor(used.GetCenter().X - side / 2), 0, image.GetWidth() - side);
            float y = Math.Clamp(used.End.Y - side, 0, image.GetHeight() - side);
            thumbnail = new AtlasTexture { Atlas = image, Region = new Rect2(x, y, side, side) };
            TownThumbnails[image] = thumbnail;
        }
        return thumbnail;
    }

    /// <summary>Une case cliquable d'une liste, avec son info-bulle ; <paramref name="selected"/> : bordée de clair.</summary>
    private static Panel Cell(bool selected, string tooltip, Action clicked, Action? doubleClicked = null)
    {
        var row = new Panel { CustomMinimumSize = new Vector2(CellWidth, RowHeight), TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Stop };
        row.AddThemeStyleboxOverride("panel", Plate(PanelColor, selected ? Colors.White : null));
        row.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
                return;
            if (click.DoubleClick && doubleClicked != null)
                doubleClicked();
            else
                clicked();
        };
        return row;
    }


    /// <summary>Les images des artefacts du héros, reconstruites seulement quand la liste change.</summary>
    private void RefreshArtifacts(Hero? hero)
    {
        string key = hero == null ? "" : $"{hero.Id}:{string.Join(",", hero.Artifacts.Select(a => a.Id))}";
        if (key == _artifactsKey)
            return;
        _artifactsKey = key;
        Clear(_artifacts);
        foreach (var artifact in hero?.Artifacts ?? new List<ArtifactType>())
        {
            if (Art.Get($"artifacts/{artifact.Id}") is not { } image)
                continue;
            _artifacts.AddChild(new TextureRect
            {
                Texture = image, CustomMinimumSize = new Vector2(24, 24),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, TooltipText = Texts.ArtifactTooltip(artifact),
            });
        }
    }

    /// <summary>Ajoute un message : le dernier s'affiche sous la carte, tous restent dans le journal.</summary>
    public void AddMessage(string text)
    {
        _messages.Add(text);
        if (_messages.Count > MaxMessages)
            _messages.RemoveAt(0);
    }

    /// <summary>Le journal : tous les messages récents, le plus récent en bas.</summary>
    private void ShowLog() => Enqueue(done =>
    {
        var dialog = new GameDialog { Title = Texts.LogTitle, OkButtonText = Texts.CloseButton, Exclusive = true, Unresizable = true };
        var scroll = new ScrollContainer { CustomMinimumSize = V(620, 360), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var text = MakeLabel(_messages.Count == 0 ? Texts.LogEmpty : string.Join("\n", _messages), 14, TextColor);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = V(600, 0);
        scroll.AddChild(text);
        dialog.AddChild(scroll);
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            done();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(660), 0));
        // le plus récent en bas : on descend au bout de la liste une fois la fenêtre posée
        Callable.From(() => scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue).CallDeferred();
    });

    /// <summary>
    /// Ce qui est sous la souris, sur deux lignes : le terrain (coût, jours de marche) ; l'objet (mine, monstre et
    /// danger estimé, ville…). Recalculé seulement quand la case ou le héros change.
    /// </summary>
    private string DescribeCell(Cell? hovered)
    {
        var hero = ShownHero;
        var key = (hovered, hero?.Id ?? 0, hero?.Position ?? default, hero?.Movement ?? 0, _state.Map.Objects.Count);
        if (_hoverKey == key)
            return _hoverText;
        _hoverKey = key;
        _hoverText = Describe(hovered);
        return _hoverText;
    }

    private string Describe(Cell? hovered)
    {
        if (hovered is not { } cell || !Viewer.Fog.IsExplored(cell))
            return "";
        var terrain = _state.Map.GetTerrain(cell);
        int? days = ShownHero != null && (TerrainInfo.IsPassable(terrain) || _state.Map.IsWater(cell)) ? _state.DaysTo(cell) : null;
        // l'eau : on y va en bateau (seulement si le héros peut l'atteindre)
        bool passable = TerrainInfo.IsPassable(terrain) || (_state.Map.IsWater(cell) && days != null);
        string line = Texts.CellLine(terrain, _state.Map.HasRoad(cell), passable ? _state.Map.MoveCost(cell) : null, days);
        var obj = _state.Map.OccupantAt(cell);
        // un héros adverse sur la case : son nom, sa couleur et le danger de l'attaquer (son bonus compte)
        if (_state.EnemyHeroAt(cell, Viewer) is { } enemy)
            return $"{line}\n{Join(Texts.HeroWithColor(enemy), Prediction(enemy.Army, enemy))}";
        string? second = obj switch
        {
            Mine mine => $"{Texts.MineName(mine)} ({(mine.Owner == Viewer ? Texts.Owned : mine.Owner != null ? Texts.EnemyOwned : Texts.Free)})",
            MonsterGuard guard => Join(Texts.ObjectName(guard), Prediction(guard.Army)),
            Town town when town.Owner == Viewer => $"{town.Name} ({Texts.Owned})",
            Town town => Join($"{town.Name} ({(town.Owner != null ? Texts.EnemyOwned : Texts.Neutral)})",
                town.Garrison.IsEmpty ? "" : Texts.GarrisonOf(Prediction(town.Garrison))),
            null => null,
            _ => Texts.ObjectName(obj) + Texts.SiteState(obj, ShownHero, Viewer),
        };
        if (obj is not MonsterGuard && _state.Map.IsGuarded(cell))
            second = Join(second ?? "", Texts.Guarded);
        return second is { Length: > 0 } ? $"{line}\n{Texts.Capitalize(second)}" : line;
    }

    private static string Join(string a, string b) => a.Length == 0 ? b : b.Length == 0 ? a : $"{a} · {b}";

    /// <summary>Danger estimé d'un combat, en simulant le combat sur des copies des armées.</summary>
    private string Prediction(Army enemy, Hero? enemyHero = null)
    {
        int armyCount = ShownHero?.Army.TotalCount ?? 0;
        var key = (enemy, enemy.TotalCount, armyCount);
        if (_predictionKey == key)
            return _predictionText;
        _predictionKey = key;
        var report = _state.PredictBattle(enemy, enemyHero);
        _predictionText = report == null || armyCount == 0 ? ""
            : Texts.Danger(report.AttackerWon ? report.AttackerLosses.Sum(s => s.Count) / (double)armyCount : null);
        return _predictionText;
    }

    // --------------------------------------------------------------- fenêtres

    /// <summary>
    /// Le bilan d'un combat (étape 7), après chaque combat, tactique ou rapide : le vainqueur, les pertes des deux camps
    /// en images, l'expérience gagnée, les révoltes et les pertes d'une fuite.
    /// </summary>
    /// <param name="asDefender">Le joueur se défendait (son héros ou sa ville a été attaqué) : le bilan à son point de vue.</param>
    public void ShowBattle(BattleFought battle, bool asDefender = false) => Enqueue(done =>
    {
        var report = battle.Report;
        var dialog = new GameDialog
        {
            Title = asDefender ? Texts.DefenseTitle(report) : Texts.ReportTitle(report), OkButtonText = Texts.ContinueButton,
            Exclusive = true, Unresizable = true,
        };
        var content = new VBoxContainer { CustomMinimumSize = V(560, 0) };
        content.AddThemeConstantOverride("separation", (int)Px(8));
        var subtitle = MakeLabel(asDefender ? Texts.DefenseSubtitle(battle) : Texts.ReportSubtitle(battle), 14, SoftTextColor);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(subtitle);
        // d'abord l'expérience gagnée (et une révolte), puis nos pertes, puis les ennemis tués
        var lines = asDefender
            ? new[] { (Texts.DefenseExperience(battle), TextColor) }
            : new[] { (Texts.ReportExperience(report), TextColor), (Texts.Revolt(report) ?? "", MapRenderer.Rgb(240, 150, 130)) };
        foreach (var (line, color) in lines)
        {
            if (line.Length == 0)
                continue;
            var label = MakeLabel(line, 16, color);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = V(560, 0);
            content.AddChild(label);
        }
        content.AddChild(LossesRow(Texts.YourLosses, asDefender ? report.DefenderLosses : report.AttackerLosses, minus: true));
        content.AddChild(LossesRow(Texts.EnemyLosses, asDefender ? report.AttackerLosses : report.DefenderLosses, minus: false));
        dialog.AddChild(content);
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            done();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(600), 0));
    });

    /// <summary>Une partie du bilan : un titre, puis chaque troupe perdue (image et nombre, sans cadre), ou « aucune ».</summary>
    private static Control LossesRow(string title, IReadOnlyList<CreatureStack> losses, bool minus)
    {
        var column = new VBoxContainer { CustomMinimumSize = V(560, 0) };
        column.AddThemeConstantOverride("separation", (int)Px(2));
        var header = MakeLabel(title, 15, SoftTextColor);
        header.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(header);
        var row = new HFlowContainer { CustomMinimumSize = V(560, 0), Alignment = FlowContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("h_separation", (int)Px(8));
        row.AddThemeConstantOverride("v_separation", (int)Px(4));
        column.AddChild(row);
        if (losses.Count == 0)
            row.AddChild(MakeLabel(Texts.NoLoss, 13, SoftTextColor));
        foreach (var stack in losses)
        {
            var slot = new Control { CustomMinimumSize = V(60, 70), TooltipText = Texts.Stack(stack), MouseFilter = Control.MouseFilterEnum.Stop };
            if (CreaturePicture(stack.Type, new Rect2(V(2, 2), V(56, 48))) is { } image)
                slot.AddChild(image);
            var count = At(MakeLabel(minus ? $"−{stack.Count}" : $"{stack.Count}", 13, TextColor), 0, 50);
            count.Size = V(60, 18);
            count.HorizontalAlignment = HorizontalAlignment.Center;
            slot.AddChild(count);
            row.AddChild(slot);
        }
        return column;
    }

    /// <summary>Fenêtre de fin de partie, avec un bouton pour recommencer.</summary>
    /// <summary>Fenêtre de niveau : ce qui a changé sur le héros.</summary>

    /// <summary>
    /// La fin de la partie (étape 7) : victoire ou défaite, pourquoi, à quel jour, et quelques chiffres de la partie ;
    /// puis une nouvelle partie, ou revoir la carte (on ne peut plus y jouer).
    /// </summary>
    public void ShowGameOver(GameEnded end, Action mainMenu) => Enqueue(done =>
    {
        var player = _state.Players[0];
        var dialog = new GameDialog
        {
            Title = Texts.GameOverTitle, OkButtonText = Texts.MainMenuButton, Exclusive = true, Unresizable = true,
        };
        dialog.AddButton(Texts.ReviewMapButton, false, "review");
        var content = new VBoxContainer { CustomMinimumSize = V(520, 0) };
        content.AddThemeConstantOverride("separation", (int)Px(10));
        Label Line(string text, int size, Color color)
        {
            var label = MakeLabel(text, size, color);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = V(520, 0);
            content.AddChild(label);
            return label;
        }
        Line(Texts.GameOverHeadline(end.Outcome), 28, end.Outcome == GameOutcome.Victory ? BorderColor : MapRenderer.Rgb(220, 110, 90));
        Line(Texts.GameOverText(end), 15, TextColor);
        Line(Texts.GameOverDay(_state.Day), 14, SoftTextColor);
        Line(Texts.GameOverStatistics(player), 14, TextColor);
        dialog.AddChild(content);
        bool closed = false;
        void Close(bool restart)
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            done();
            if (restart)
                mainMenu();
        }
        dialog.Confirmed += () => Close(restart: true);
        dialog.CustomAction += _ => Close(restart: false);
        dialog.Canceled += () => Close(restart: false);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(580), 0));
    });

    /// <summary>
    /// La fenêtre des options (bouton du panneau ou Échap) : sauvegarder, charger, plein écran, menu principal, quitter,
    /// un bouton par ligne.
    /// </summary>
    public void ShowOptions() => Enqueue(done =>
    {
        var dialog = new GameDialog
        {
            Title = Texts.OptionsTitle, OkButtonText = Texts.CloseButton, Exclusive = true, Unresizable = true,
        };
        bool closed = false;
        void Close(Action? then)
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            done();
            then?.Invoke();
        }
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(8));
        foreach (var (text, tooltip, action) in new (string, string, Action?)[]
                 {
                     (Texts.SaveButton, Texts.KeyTooltip(Texts.SaveTooltip, "F5 : sauvegarde rapide"), SavePressed),
                     (Texts.LoadButton, Texts.KeyTooltip(Texts.LoadTooltip, "F9 : sauvegarde rapide"), LoadPressed),
                     (Texts.FullscreenButton, Texts.KeyTooltip(Texts.FullscreenTooltip, "F11 ou Alt+Entrée"), Main.ToggleFullscreen),
                     (Texts.MainMenuButton, Texts.MainMenuTooltip,
                         () => Confirm(Texts.MainMenuTitle, Texts.MainMenuText, Texts.MainMenuButton, MainMenuPressed)),
                     (Texts.QuitGameButton, Texts.QuitGameButton,
                         () => Confirm(Texts.QuitGameTitle, Texts.QuitGameText, Texts.QuitGameConfirm, QuitPressed)),
                 })
        {
            var button = MakeButton(text, 16);
            Sized(button, Px(320), ButtonHeight);
            button.TooltipText = tooltip;
            button.Pressed += () => Close(action);
            content.AddChild(button);
        }
        var note = MakeLabel(Texts.OptionsText, 13, SoftTextColor);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = V(320, 0);
        content.AddChild(note);
        dialog.AddChild(content);
        dialog.GetOkButton().TooltipText = Texts.KeyTooltip(Texts.CloseOptionsTooltip, "Échap");
        dialog.Confirmed += () => Close(null);
        dialog.Canceled += () => Close(null);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(360), 0));
    });

    /// <summary>Quitter le jeu ou revenir au menu : on demande d'abord (ce qui n'est pas sauvegardé serait perdu).</summary>
    private void Confirm(string title, string text, string ok, Action? confirmed) => Enqueue(done =>
    {
        var dialog = GameDialog.Confirm(title, text, ok);
        dialog.Confirmed += () => { dialog.QueueFree(); done(); confirmed?.Invoke(); };
        dialog.Canceled += () => { dialog.QueueFree(); done(); };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(480), 0));
    });

    /// <summary>
    /// Choix des conditions de la partie, au lancement : une case à cocher par façon de gagner (la première accomplie
    /// l'emporte) et une pour la limite de jours. Fermer la fenêtre garde ce qui est coché.
    /// </summary>
    public void ShowRulesChoice(Action<GameRules> choose) => Enqueue(done =>
    {
        var dialog = new GameDialog
        {
            Title = Texts.RulesTitle,
            OkButtonText = Texts.StartButton,
            Exclusive = true,
            Unresizable = true,
        };
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(6));
        var intro = MakeLabel(Texts.RulesText, 15, TextColor);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.CustomMinimumSize = V(480, 0);
        content.AddChild(intro);
        var goals = new System.Collections.Generic.Dictionary<VictoryCondition, CheckBox>();
        // « vaincre tous les adversaires » : seulement s'il y en a
        foreach (var condition in GameRules.Choices.Where(c => c != VictoryCondition.DefeatOpponents || _state.Opponents.Any()))
        {
            var box = MakeCheckBox(Texts.Goal(condition), condition == GameRules.Default.Victory);
            goals[condition] = box;
            content.AddChild(box);
        }
        content.AddChild(new HSeparator());
        var limit = MakeCheckBox(Texts.DayLimitChoice, false);
        content.AddChild(limit);
        dialog.AddChild(content);

        // il faut au moins une façon de gagner
        void Update() => dialog.GetOkButton().Disabled = !goals.Values.Any(b => b.ButtonPressed);
        foreach (var box in goals.Values)
            box.Toggled += _ => Update();

        bool chosen = false;
        void Choose()
        {
            if (chosen)
                return;
            chosen = true;
            var victory = goals.Where(g => g.Value.ButtonPressed).Aggregate(VictoryCondition.None, (all, g) => all | g.Key);
            choose(new GameRules(victory == VictoryCondition.None ? GameRules.Default.Victory : victory,
                limit.ButtonPressed ? GameRules.StandardDayLimit : null));
            dialog.QueueFree();
            done();
        }
        dialog.Confirmed += Choose;
        dialog.Canceled += Choose;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(540), 0));
    });

    /// <summary>La carte au trésor : les morceaux dévoilés par les obélisques visités, et la croix.</summary>
    public void ShowTreasureMap(GameState state, Player viewer) => Enqueue(done =>
    {
        var dialog = new GameDialog { Title = Texts.TreasureMapTitle, OkButtonText = "OK", Exclusive = true, Unresizable = true };
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(8));
        var pieces = state.TreasureMapPieces(viewer);
        var text = MakeLabel(state.Map.Treasure == null ? Texts.TreasureGone
            : Texts.TreasureMapText(pieces.Count(p => p), pieces.Count), 14, TextColor);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = V(TreasureMapView.Columns * TreasureMapView.CellSize, 0);
        content.AddChild(text);
        if (state.Map.Treasure != null)
        {
            var view = new TreasureMapView { State = state, Viewer = viewer };
            var center = new CenterContainer();
            center.AddChild(view);
            content.AddChild(center);
        }
        dialog.AddChild(content);
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            done();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(320), 0));
    });

    /// <summary>Avant un combat : le diriger (écran de combat) ou le laisser se dérouler (combat rapide).</summary>
    /// <summary>Un adversaire attaque : se défendre soi-même (combat tactique) ou le combat rapide.</summary>
    public void ShowDefenseChoice(string attacker, string? town, Action tactical, Action quick) =>
        ShowBattleChoice(attacker, tactical, quick, Texts.DefenseChoiceTitle, Texts.DefenseChoiceText(attacker, town));

    public void ShowBattleChoice(string opponent, Action tactical, Action quick, string? title = null, string? text = null) => Enqueue(done =>
    {
        var dialog = new GameDialog
        {
            Title = title ?? Texts.BattleChoiceTitle,
            DialogText = text ?? Texts.BattleChoiceText(opponent),
            OkButtonText = Texts.TacticalButton,
            Exclusive = true,
            Unresizable = true,
        };
        dialog.AddButton(Texts.QuickButton, false, "quick");
        bool chosen = false;
        void Choose(Action then)
        {
            if (chosen)
                return;
            chosen = true;
            dialog.QueueFree();
            done();
            then();
        }
        dialog.Confirmed += () => Choose(tactical);
        dialog.Canceled += () => Choose(tactical);
        dialog.CustomAction += _ => Choose(quick);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(480), 0));
    });

    /// <summary>Un message dans une fenêtre, qu'on ferme avec OK (le trésor déterré…).</summary>
    public void ShowMessage(string title, string text) => ShowDialog(title, text, "OK", null);

    /// <summary>Le trésor est déterré : l'image de l'artefact en grand (×3, pixels nets), puis ce qu'il apporte.</summary>
    public void ShowArtifactFound(string title, string text, ArtifactType artifact) => Enqueue(done =>
    {
        var dialog = new GameDialog { Title = title, OkButtonText = "OK", Exclusive = true, Unresizable = true };
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(10));
        if (Art.Get($"artifacts/{artifact.Id}") is { } image)
        {
            var center = new CenterContainer();
            center.AddChild(new TextureRect
            {
                Texture = image, CustomMinimumSize = image.GetSize() * 4,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            });
            content.AddChild(center);
        }
        var label = MakeLabel(text, 15, TextColor);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.CustomMinimumSize = V(380, 0);
        content.AddChild(label);
        dialog.AddChild(content);
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            done();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(420), 0));
    });

    /// <summary>True tant qu'une fenêtre est ouverte ou en attente (le jeu ignore alors les commandes de la carte).</summary>
    public bool IsDialogOpen => _dialogOpen || _pendingDialogs.Count > 0;

    private bool _dialogOpen;

    /// <summary>Chaque fenêtre en attente : une action qui l'ouvre et reçoit l'action à appeler quand elle est fermée.</summary>
    private readonly Queue<Action<Action>> _pendingDialogs = new();

    /// <summary>Les fenêtres s'affichent l'une après l'autre (ex. : compte rendu du combat, puis fin de partie).</summary>
    private void ShowDialog(string title, string text, string button, Action? onClose) => Enqueue(done =>
    {
        var dialog = new GameDialog
        {
            Title = title,
            DialogText = text,
            OkButtonText = button,
            Exclusive = true,
            Unresizable = true,
        };
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            onClose?.Invoke();
            done();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(360), 0));
    });

    /// <summary>Choix de la caractéristique d'un niveau : un bouton par caractéristique proposée. Fermer la fenêtre prend la première.</summary>
    public void ShowLevelChoice(HeroLevelChoice choice, Action<HeroStat> choose) => Enqueue(done =>
    {
        var dialog = new GameDialog { Title = Texts.LevelChoiceTitle(choice), Exclusive = true, Unresizable = true };
        dialog.GetOkButton().Hide();
        bool chosen = false;
        void Choose(HeroStat stat)
        {
            if (chosen)
                return;
            chosen = true;
            choose(stat);
            dialog.QueueFree();
            done();
        }
        // en haut : le portrait, et ce que le niveau donne dans tous les cas (le charisme)
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(10));
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", (int)Px(12));
        if (Art.Portrait(choice.Hero.Name) is { } portrait)
            top.AddChild(new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, Texture = portrait,
                CustomMinimumSize = new Vector2(128, 128), // le portrait ×2
            });
        var intro = MakeLabel($"{Texts.LevelChoiceText(choice)}\n{Texts.CharismaGain(choice.Hero)}", 15, TextColor);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.CustomMinimumSize = V(380, 0);
        intro.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        top.AddChild(intro);
        content.AddChild(top);
        // un grand bouton par caractéristique proposée : son nom, avant → après, ce qu'elle fait
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", (int)Px(10));
        foreach (var stat in choice.Options)
        {
            var size = V(170, 120);
            var button = MakeButton("", 14);
            button.CustomMinimumSize = size;
            button.TooltipText = Texts.StatTooltip(stat, choice.Hero);
            // les lignes partent du haut (les noms sont alignés d'un bouton à l'autre), avec une marge sur les côtés
            var lines = new VBoxContainer
            {
                Position = V(8, 10), Size = size - V(16, 20), Alignment = BoxContainer.AlignmentMode.Begin,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            lines.AddThemeConstantOverride("separation", (int)Px(2));
            foreach (var (text, fontSize, color) in new[]
                     {
                         (Texts.StatFullName(stat), 16, TextColor),
                         (Texts.StatChange(choice.Hero, stat), 20, BorderColor),
                         (Texts.StatEffect(stat, choice.Hero), 12, TextColor), // clair : lisible sur la pierre grise
                     })
            {
                var label = MakeLabel(text, fontSize, color);
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                label.CustomMinimumSize = new Vector2(size.X - Px(16), 0);
                lines.AddChild(label);
            }
            button.AddChild(lines);
            var picked = stat;
            button.Pressed += () => Choose(picked);
            buttons.AddChild(button);
        }
        content.AddChild(buttons);
        dialog.AddChild(content);
        dialog.Canceled += () => Choose(choice.Options[0]);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(560), 0));
    });

    /// <summary>Un coffre ouvert : deux grands boutons, l'or ou l'expérience (<paramref name="choose"/> reçoit vrai pour l'or).</summary>
    /// <summary>
    /// Le chantier naval de la côte : acheter un bateau (<paramref name="status"/> : peut-on ? sinon pourquoi), ou partir.
    /// </summary>
    public void ShowShipyard(BoatResult status, Action buy) => Enqueue(done =>
    {
        var dialog = new GameDialog { Title = Texts.ShipyardTitle, OkButtonText = Texts.CloseButton, Exclusive = true, Unresizable = true };
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(10));
        var text = MakeLabel(Texts.ShipyardText + (status == BoatResult.Ok ? "" : "\n" + Texts.BoatStatus(status)), 15, TextColor);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = V(440, 0);
        content.AddChild(text);
        var button = Sized(MakeButton(Texts.BuildBoatButton, 16), Px(260), ButtonHeight);
        button.Disabled = status != BoatResult.Ok;
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        content.AddChild(button);
        dialog.AddChild(content);
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            dialog.QueueFree();
            done();
        }
        button.Pressed += () =>
        {
            buy();
            Close();
        };
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(480), 0));
    });

    public void ShowChestChoice(ChestOpened chest, Action<bool> choose) => Enqueue(done =>
    {
        var dialog = new GameDialog { Title = Texts.ChestTitle, Exclusive = true, Unresizable = true };
        dialog.GetOkButton().Hide();
        bool chosen = false;
        void Choose(bool gold)
        {
            if (chosen)
                return;
            chosen = true;
            choose(gold);
            dialog.QueueFree();
            done();
        }
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(10));
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", (int)Px(12));
        if (Art.Portrait(chest.Hero.Name) is { } portrait)
            top.AddChild(new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, Texture = portrait,
                CustomMinimumSize = new Vector2(128, 128), // le portrait ×2
            });
        var intro = MakeLabel(Texts.ChestText(chest), 15, TextColor);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.CustomMinimumSize = V(340, 0);
        intro.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        top.AddChild(intro);
        content.AddChild(top);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", (int)Px(10));
        foreach (var (gold, text) in new[] { (true, Texts.ChestGold(chest.Chest)), (false, Texts.ChestExperience(chest.Chest)) })
        {
            var button = MakeButton(text, 16);
            Sized(button, Px(230), LargeButtonHeight);
            bool picked = gold;
            button.Pressed += () => Choose(picked);
            buttons.AddChild(button);
        }
        content.AddChild(buttons);
        dialog.AddChild(content);
        dialog.Canceled += () => Choose(true);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(500), 0));
    });

    /// <summary>
    /// Un gardien propose de rejoindre le héros (Diplomatie) ou s'enfuit : la troupe, ce qu'elle propose, et les choix
    /// possibles (accepter, les laisser partir, combattre ou les poursuivre). Fermer la fenêtre, c'est combattre.
    /// </summary>
    public void ShowEncounter(GuardEncounter encounter, Action<EncounterChoice> choose) => Enqueue(done =>
    {
        var dialog = new GameDialog { Title = Texts.EncounterTitle(encounter), Exclusive = true, Unresizable = true };
        dialog.GetOkButton().Hide();
        bool chosen = false;
        void Choose(EncounterChoice choice)
        {
            if (chosen)
                return;
            chosen = true;
            choose(choice);
            dialog.QueueFree();
            done();
        }
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", (int)Px(10));
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", (int)Px(12));
        var stack = new CreatureStack(encounter.Guard.Type, encounter.Guard.Count);
        if (CreaturePicture(stack.Type, new Rect2(Vector2.Zero, V(120, 120))) is { } picture)
        {
            var frame = new Panel { CustomMinimumSize = V(120, 120) };
            frame.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
            frame.AddChild(picture);
            top.AddChild(frame);
        }
        var intro = MakeLabel(Texts.EncounterText(encounter), 15, TextColor);
        intro.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        intro.CustomMinimumSize = V(380, 0);
        intro.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        top.AddChild(intro);
        content.AddChild(top);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", (int)Px(10));
        void Add(string text, EncounterChoice choice, bool enabled = true)
        {
            var button = MakeButton(text, 15);
            Sized(button, Px(170), LargeButtonHeight);
            button.Disabled = !enabled;
            button.Pressed += () => Choose(choice);
            buttons.AddChild(button);
        }
        if (encounter.Offer is { } offer)
            Add(Texts.EncounterJoin(encounter), EncounterChoice.Join, offer.CanAccept);
        if (encounter.Flees)
            Add(Texts.EncounterLetFlee, EncounterChoice.LetFlee);
        Add(Texts.EncounterFight(encounter), EncounterChoice.Fight);
        content.AddChild(buttons);
        dialog.AddChild(content);
        dialog.Canceled += () => Choose(EncounterChoice.Fight);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I((int)Px(580), 0));
    });

    private void Enqueue(Action<Action> open)
    {
        _pendingDialogs.Enqueue(open);
        if (!_dialogOpen)
            OpenNextDialog();
    }

    private void OpenNextDialog()
    {
        if (!_pendingDialogs.TryDequeue(out var open))
            return;
        _dialogOpen = true;
        open(() =>
        {
            _dialogOpen = false;
            OpenNextDialog();
        });
    }

}
