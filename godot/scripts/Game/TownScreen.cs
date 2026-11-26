using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'écran de ville (étape 7), en plein écran par-dessus la carte. En haut, la barre des ressources (à la même place
/// que sur la carte) et le nom de la ville. Au centre, la ville vue de face : chaque bâtiment construit à sa place, dans la
/// scène de sa faction (<see cref="TownScene"/>, art/towns/&lt;faction&gt;/), ou dans une boîte (<see cref="BuildingType.View"/>)
/// pour les factions qui n'ont pas encore la leur ; un nouveau bâtiment qui apparaît peu à peu ;
/// un clic sur un bâtiment l'utilise (habitation : recruter, taverne, hôtel de ville : construire). En bas, toujours
/// visibles, la garnison et le héros présent : on glisse les troupes (Maj ou clic droit pour partager), un clic les
/// fait passer de l'autre côté. Construire, recruter et la taverne s'ouvrent en fenêtres par-dessus la ville.
/// </summary>
public partial class TownScreen : CanvasLayer
{
    // en unités de l'interface (×1,5 à l'écran), comme les places des bâtiments dans buildings.json (vue de 1280 × 390)
    private const float ViewTop = 84, ViewHeight = 390;
    private const float ArmyX = 100;
    // une place de l'armée, en pixels (choix de l'utilisateur, 2026-11-21 : de grandes places, la créature toujours ×2, le
    // nombre par-dessus) : la taille de la garnison dans la disposition, divisée en 7 ; l'image de la ville et le
    // portrait, plus étroits, devant chaque rangée
    private Vector2 _cell = new(210, 244), _firstCell = new(140, 244);

    private GameState _state = null!;
    private Town? _town;
    private TextureRect _townPicture = null!;
    private Vector2 _screen;
    private Control _root = null!;

    private ResourceBar _bar = null!;
    private Label _title = null!;
    private Label _header = null!;
    private PanelContainer _nameTab = null!;
    private Button[] _townArrows = null!;
    private bool _trialBackground; // un fond de scène à l'essai (--town-background)
    // la barre d'état, en bas (comme Heroes 2) : ce que montre la souris, et la date
    private Label _status = null!, _statusDate = null!;
    private Control _view = null!;
    private readonly Dictionary<string, Control> _buildingBoxes = new();
    // la scène de chaque faction (null : pas encore d'images), et ce qui en est montré : le fond, les ombres, les
    // bâtiments, le décor devant eux
    private readonly Dictionary<string, TownScene?> _scenes = new();
    private Control? _sceneLayer;
    private readonly Dictionary<string, Control> _buildingShadows = new();
    private string? _viewKey;
    private readonly List<Button> _actions = new();
    // les actions par-dessus la vue, à droite : Quitter toujours visible, les autres sortent du bord au survol
    private Control _drawer = null!;
    private bool _drawerOpen;
    private Tween? _drawerTween;

    // le marché : ce qu'on donne, ce qu'on reçoit, et combien (gardés tant que la fenêtre se reconstruit)
    private ResourceType _marketGive = ResourceType.Wood, _marketReceive = ResourceType.Gold;
    private int _marketCount;

    private ArmySlots _garrison = null!;
    private ArmySlots _visitor = null!;
    private Label _visitorLabel = null!;
    private TextureRect _visitorPortrait = null!;
    private Panel _portraitFrame = null!;
    private Button _giveAll = null!, _leaveAll = null!;

    // les fenêtres par-dessus la ville : une à la fois ; le curseur de recrutement par-dessus tout
    private Control? _window;
    private bool _singleRecruit; // la fenêtre Recruter ne montre qu'une habitation (ouverte d'un clic sur elle)
    private Action? _reopen; // pour remettre à jour la fenêtre ouverte (après un recrutement…)

    // fenêtre du nombre à recruter
    private Control _recruitOverlay = null!;
    private Control _recruitPicture = null!;
    // la place de l'image dans la fenêtre du nombre à recruter (en pixels)
    private Vector2 _recruitPlace, _recruitPlacePosition;
    private Label _recruitTitle = null!;
    private Label _recruitInfo = null!;
    private Label _recruitStats = null!;
    private HSlider _recruitSlider = null!;
    private Label _recruitTotal = null!;
    private Button _recruitButton = null!;
    private Button _recruitMax = null!;
    private CreatureType? _recruitType;

    public bool IsOpen => _town != null;
    /// <summary>La place des éléments, réglée à la souris (<c>tools/ui/disposition.py</c>).</summary>
    public ScreenLayout Layout { get; private set; } = null!;
    public event Action? Closed;

    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        _screen = screenSize;
        Layout = new ScreenLayout("town");
        if (Layout.SizeOf("garrison") is { } armySize)
        {
            _cell = new Vector2(MathF.Floor((armySize.X - (Army.MaxStacks - 1) * Px(2)) / Army.MaxStacks), armySize.Y);
            _firstCell = new Vector2(_firstCell.X, armySize.Y);
        }
        Layer = 5; // au-dessus de la carte, du panneau et des grandes fenêtres
        Visible = false;

        // Le fond arrête la souris : la carte en dessous ne reçoit plus les clics
        _root = new Panel { Size = screenSize, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.AddThemeStyleboxOverride("panel", Stone(PanelColor));
        AddChild(_root);

        _bar = new ResourceBar();
        _root.AddChild(Layout.Add("resources", _bar));
        // en colonne, en bas à droite (comme Heroes 2, choix de l'utilisateur, 2026-11-21)
        _bar.BuildColumn(Px(188));

        // le nom de la ville sur un onglet, posé sur le bas de la vue (comme Heroes 2), avec le revenu à la suite ;
        // centré au-dessus des armées (voir Refresh), sauf si l'utilisateur l'a placé lui-même
        _nameTab = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _nameTab.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        var tabLine = new HBoxContainer();
        tabLine.AddThemeConstantOverride("separation", (int)Px(12));
        _nameTab.AddChild(tabLine);
        _title = MakeLabel("", 22, TextColor);
        _header = MakeLabel("", 14, SoftTextColor);
        _header.VerticalAlignment = VerticalAlignment.Center;
        _header.SizeFlagsVertical = Control.SizeFlags.Fill;
        // de part et d'autre, les flèches passent à la ville précédente ou suivante du joueur (idée de l'utilisateur,
        // 2026-11-21) ; cachées s'il n'a qu'une ville
        _townArrows = new[] { TownArrow('<', -1), TownArrow('>', +1) };
        tabLine.AddChild(_townArrows[0]);
        tabLine.AddChild(_title);
        tabLine.AddChild(_header);
        tabLine.AddChild(_townArrows[1]);

        // un clic sur les ressources ouvre le marché, si le joueur en a un dans l'une de ses villes (ses boutons sont
        // retirés : le bâtiment du marché n'est pas dans toutes les villes)
        _bar.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && Market.Count(_state.CurrentPlayer) > 0)
                OpenMarket();
        };

        var statusBar = new Panel { Position = new Vector2(0, screenSize.Y - Px(22)), Size = new Vector2(screenSize.X, Px(22)) };
        statusBar.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        _status = MakeLabel("", 14, TextColor);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.VerticalAlignment = VerticalAlignment.Center;
        _status.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _status.MouseFilter = Control.MouseFilterEnum.Ignore;
        statusBar.AddChild(_status);
        _statusDate = MakeLabel("", 14, SoftTextColor);
        _statusDate.HorizontalAlignment = HorizontalAlignment.Right;
        _statusDate.VerticalAlignment = VerticalAlignment.Center;
        _statusDate.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _statusDate.OffsetRight = -Px(12);
        _statusDate.MouseFilter = Control.MouseFilterEnum.Stop; // pour l'objectif en info-bulle
        statusBar.AddChild(_statusDate);
        _root.AddChild(Layout.Add("status", statusBar, resizable: true));

        // la ville vue de face : un ciel, un sol, les bâtiments
        _view = new Control { Position = new Vector2(0, Px(ViewTop)), Size = new Vector2(screenSize.X, Px(ViewHeight)), ClipContents = true };
        _root.AddChild(Layout.Add("view", _view, resizable: true));
        _root.AddChild(Layout.Add("name-tab", _nameTab));
        // le ciel sur les 4 dixièmes du haut, le sol dessous, quelle que soit la taille de la vue (voir la disposition)
        _view.AddChild(new ColorRect { AnchorRight = 1, AnchorBottom = 0.4f, Color = MapRenderer.Rgb(58, 72, 92), MouseFilter = Control.MouseFilterEnum.Ignore });
        _view.AddChild(new ColorRect { AnchorTop = 0.4f, AnchorRight = 1, AnchorBottom = 1, Color = MapRenderer.Rgb(60, 74, 48), MouseFilter = Control.MouseFilterEnum.Ignore });
        // « --town-background=fichier.png » : un fond de scène à l'essai (pas encore dans le jeu), ×2, centré en bas de la
        // vue, sous les bâtiments, pour le juger en place
        if (OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--town-background=")) is { } background
            && Image.LoadFromFile(background["--town-background=".Length..]) is { } image && !image.IsEmpty()
            && (_trialBackground = true))
            _view.AddChild(new TextureRect
            {
                Texture = ImageTexture.CreateFromImage(image), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1,
                OffsetLeft = -image.GetWidth(), OffsetRight = image.GetWidth(), OffsetTop = -2 * image.GetHeight(),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

        // les actions de la ville en boutons à icône, le nom en info-bulle (choix de l'utilisateur, 2026-11-21) : Quitter
        // toujours visible, en haut à droite de la vue ; Construire et Recruter glissent du bord droit, dessous, quand la
        // souris approche. La taverne, la guilde et le marché s'ouvrent d'un clic sur leur bâtiment.
        _drawer = new Control
        {
            Position = new Vector2(screenSize.X - IconButtonSide - Px(6), Px(6)),
            Size = new Vector2(IconButtonSide, 3 * IconButtonSide + 2 * ButtonGap),
            ClipContents = true, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.AddChild(Layout.Add("actions", _drawer));
        float by = 0;
        foreach (var (icon, text, key, open) in new (string, string, string, Action)[]
                 {
                     ("quitter", Texts.QuitTownTooltip, "Échap", Close),
                     ("construire", Texts.BuildButton, "", OpenBuild),
                     ("recruter", Texts.RecruitButton, "", () => OpenRecruitWindow(null)),
                 })
        {
            var button = IconButtons.Make(icon, text, key, open);
            button.Position = new Vector2(_actions.Count == 0 ? 0 : IconButtonSide, by);
            if (_actions.Count > 0)
                button.MouseFilter = Control.MouseFilterEnum.Ignore;
            _drawer.AddChild(button);
            _actions.Add(button);
            by += IconButtonSide + ButtonGap;
        }

        // la garnison et le héros présent, toujours visibles
        float y = ViewTop + ViewHeight + 8;
        _root.AddChild(Layout.Add("garrison-label", At(MakeLabel(Texts.GarrisonHeader, 15, TextColor), 16, y)));
        var townPicture = new Panel { Position = V(12, y + 22), Size = _firstCell };
        townPicture.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        // l'image de 96 pixels, ×1, au milieu : celle de la faction de la ville ouverte (voir Open)
        _townPicture = Picture(null!, ((_firstCell - new Vector2(96, 96)) / 2).Floor(), new Vector2(96, 96));
        townPicture.AddChild(_townPicture);
        _root.AddChild(Layout.Add("town-picture", townPicture));
        _garrison = Layout.Add("garrison", MakeArmy(V(ArmyX, y + 22)), resizable: true);
        // à droite des armées : l'aide sur les troupes, et dessous les deux boutons d'échange
        float exchangeX = ArmyX + 7 * 86 + 10;
        var hint = At(MakeLabel(Texts.DragHint, 13, SoftTextColor), exchangeX + 14, y + 34);
        _root.AddChild(Layout.Add("hint", hint));

        y += 110;
        _visitorLabel = At(MakeLabel("", 15, TextColor), 16, y);
        _root.AddChild(Layout.Add("hero-label", _visitorLabel));
        // le nom du héros en info-bulle (voir Refresh)
        var portraitFrame = _portraitFrame = new Panel { Position = V(12, y + 22), Size = _firstCell };
        portraitFrame.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        _root.AddChild(Layout.Add("portrait", portraitFrame));
        // le portrait ×2 au milieu, dans un fin cadre d'argent
        var portraitBorder = new Panel
        {
            Position = ((_firstCell - new Vector2(132, 132)) / 2).Floor(), Size = new Vector2(132, 132),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        portraitBorder.AddThemeStyleboxOverride("panel", Box(Colors.Black, SilverColor, 1, 1, 1, 1));
        portraitFrame.AddChild(portraitBorder);
        _visitorPortrait = Picture(null!, new Vector2(2, 2), new Vector2(128, 128));
        portraitBorder.AddChild(_visitorPortrait);
        _visitor = Layout.Add("hero-army", MakeArmy(V(ArmyX, y + 22)), resizable: true);
        // deux boutons à icône (choix de l'utilisateur, 2026-11-20) : la flèche descend vers le héros, ou monte vers la
        // garnison ; le texte en info-bulle
        _giveAll = IconButtons.Make("donner-heros", Texts.GiveAllToHero, "", GiveAll);
        // à la hauteur du milieu de la rangée du héros
        _giveAll.Position = new Vector2(Px(exchangeX + 14), Snap(Px(y + 22 + 44) - IconButtonSide / 2));
        _root.AddChild(Layout.Add("give-all", _giveAll));
        _leaveAll = IconButtons.Make("laisser-garnison", Texts.LeaveAllInGarrison, "", LeaveAll);
        _leaveAll.Position = _giveAll.Position + new Vector2(IconButtonSide + ButtonGap, 0);
        _root.AddChild(Layout.Add("leave-all", _leaveAll));

        Layout.Apply();
        BuildRecruitWindow();
    }

    private ArmySlots MakeArmy(Vector2 position)
    {
        var slots = new ArmySlots { Position = position, SlotSize = _cell, CountFontSize = 16, CountOverlay = true, DragGroup = this };
        slots.SlotClicked += index => Act(() => _state.TransferStack(_town!, fromGarrison: slots == _garrison, index));
        slots.SlotRightClicked += index => Split(slots, index, slots, null);
        slots.TroopDropped += (source, from, to, split) =>
        {
            if (split)
                Split(source, from, slots, to);
            else
                Act(() => _state.MoveTroop(_town!, source == _garrison, from, slots == _garrison, to));
        };
        _root.AddChild(slots);
        return slots;
    }

    /// <summary>Partage une troupe : combien en déplacer, puis vers l'emplacement visé (ou le premier libre).</summary>
    private void Split(ArmySlots source, int from, ArmySlots target, int? to)
    {
        if (_town is not { } town)
            return;
        var army = ArmyOf(source);
        if (army == null || from >= army.Stacks.Count)
            return;
        var stack = army.Stacks[from];
        int max = source == target ? stack.Count - 1 : stack.Count;
        if (max < 1)
            return;
        int slot = to ?? Math.Min(ArmyOf(target)?.Stacks.Count ?? 0, Army.MaxStacks - 1);
        AskCount(_root, Texts.SplitTitle(stack.Type), n => Texts.SplitText(stack.Type, n, stack.Count), max,
            n => Act(() => _state.MoveTroop(town, source == _garrison, from, target == _garrison, slot, n)));
    }

    private Army? ArmyOf(ArmySlots slots) =>
        _town == null ? null : slots == _garrison ? _town.Garrison : _state.VisitingHero(_town)?.Army;

    /// <summary>Fait une action de la ville, puis met l'écran à jour (après le clic, pas pendant).</summary>
    private void Act(Func<bool> action)
    {
        if (_town != null && action())
            Callable.From(Refresh).CallDeferred();
    }

    private void GiveAll()
    {
        if (_town is not { } town)
            return;
        while (town.Garrison.Stacks.Count > 0 && _state.TransferStack(town, fromGarrison: true, 0)) { }
        Refresh();
    }

    private void LeaveAll()
    {
        if (_town is not { } town)
            return;
        while (_state.TransferStack(town, fromGarrison: false, 0)) { }
        Refresh();
    }

    public void Open(Town town)
    {
        _town = town;
        _townPicture.Texture = Art.Town(town.Faction);
        _viewKey = null;
        Visible = true;
        CloseWindow();
        _recruitOverlay.Visible = false;
        Refresh();
        Audio.PlayMusic($"town-{town.Faction}", "town");
        Audio.PlayAmbience(null); // la ville n'a que sa musique
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        CloseWindow();
        _town = null;
        Visible = false;
        Audio.PlayMapMusic();
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!IsOpen || e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            return;
        // Échap ferme d'abord le curseur de recrutement, puis la fenêtre, puis l'écran de ville
        if (_recruitOverlay.Visible)
            _recruitOverlay.Visible = false;
        else if (_window != null)
            CloseWindow();
        else
            Close();
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        // la barre d'état : la première ligne de l'info-bulle de ce que montre la souris (ou d'un de ses parents)
        Node? hovered = GetViewport().GuiGetHoveredControl();
        while (hovered is Control c && string.IsNullOrEmpty(c.TooltipText))
            hovered = c.GetParent();
        _status.Text = hovered is Control shown && shown != _statusDate ? shown.TooltipText.Split('\n')[0] : "";

        // les actions sortent quand la souris est sur leur colonne (pas pendant qu'une fenêtre est ouverte)
        bool open = _window == null && _drawer.GetGlobalRect().HasPoint(_drawer.GetGlobalMousePosition());
        if (open != _drawerOpen)
        {
            _drawerOpen = open;
            _drawerTween?.Kill();
            _drawerTween = _drawer.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            foreach (var button in _actions.Skip(1))
            {
                _drawerTween.TweenProperty(button, "position:x", open ? 0 : IconButtonSide, 0.18);
                button.MouseFilter = open ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
            }
        }
    }

    /// <summary>Une flèche de l'onglet du nom : passe à la ville d'à côté du joueur, en boucle.</summary>
    private Button TownArrow(char direction, int step)
    {
        var button = FixedButton("", 12, new Vector2(Px(22), Px(26)));
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        button.TooltipText = step < 0 ? Texts.PreviousTown : Texts.NextTown;
        var arrow = Picture(Arrow(direction, SilverColor), Vector2.Zero, Vector2.Zero);
        arrow.Size = arrow.Texture.GetSize();
        arrow.Position = ((button.Size - arrow.Size) / 2).Floor();
        button.AddChild(arrow);
        button.Pressed += () =>
        {
            var towns = _state.CurrentPlayer.Towns;
            if (_town != null && towns.Count > 1)
                Open(towns[(towns.IndexOf(_town) + step + towns.Count) % towns.Count]);
        };
        return button;
    }

    /// <summary>L'onglet du nom au milieu de la vue, posé sur son bas.</summary>
    private void CenterNameTab()
    {
        _nameTab.Size = _nameTab.GetCombinedMinimumSize();
        _nameTab.Position = new Vector2(Snap(_view.Position.X + (_view.Size.X - _nameTab.Size.X) / 2),
            Snap(_view.Position.Y + _view.Size.Y - _nameTab.Size.Y));
    }

    /// <summary>Met l'écran à jour d'après l'état de la ville (après chaque action).</summary>
    public void Refresh()
    {
        if (_town is not { } town)
            return;
        _bar.Refresh(_state, _state.CurrentPlayer);
        _title.Text = town.Name;
        foreach (var arrow in _townArrows)
            arrow.Visible = _state.CurrentPlayer.Towns.Count > 1;
        // les ressources ouvrent le marché (voir le constructeur) : la barre d'état le dit
        _bar.TooltipText = Market.Count(_state.CurrentPlayer) > 0 ? Texts.MarketFromResources : "";
        _header.Text = Texts.TownHeader(town);
        // sans barre d'état (retirée dans la disposition), la date suit le revenu sur l'onglet
        if (!_statusDate.GetParent<Control>().Visible)
            _header.Text += " · " + Texts.Date(_state.Day);
        _statusDate.Text = Texts.Date(_state.Day);
        _statusDate.TooltipText = Texts.GoalTooltip(_state, _state.CurrentPlayer);
        if (!Layout.Placed("name-tab"))
            Callable.From(CenterNameTab).CallDeferred(); // une fois sa taille connue, avec le nouveau texte
        RefreshView(town);

        var hero = _state.VisitingHero(town);
        _garrison.Show(town.Garrison, null);
        _visitor.Show(hero?.Army, hero?.CommandLimit);
        _visitorLabel.Text = hero != null ? Texts.VisitingHero(hero) : Texts.NoVisitor;
        _visitorPortrait.Texture = hero != null ? Art.Portrait(hero.Name) : null;
        _portraitFrame.TooltipText = _visitorLabel.Text;
        _giveAll.Disabled = hero == null || town.Garrison.Stacks.Count == 0;
        _leaveAll.Disabled = hero == null || hero.Army.Stacks.Count <= 1;
        IconButtons.Dim(_actions.Append(_giveAll).Append(_leaveAll));
        _reopen?.Invoke();
    }

    // ----------------------------------------------------------- la ville vue de face

    /// <summary>Les bâtiments construits, chacun à sa place ; ceux qu'un autre remplace ne sont plus dessinés.</summary>
    private IEnumerable<BuildingType> ShownBuildings(Town town)
    {
        var built = town.BuiltBuildings.ToList();
        var replaced = built.Where(b => b.Replaces != null).Select(b => b.Replaces!).ToHashSet();
        return built.Where(b => b.View.Count == 4 && !replaced.Contains(b.Id));
    }

    private void RefreshView(Town town)
    {
        var shown = ShownBuildings(town).ToList();
        string key = $"{town.Id}:{string.Join(",", shown.Select(b => b.Id))}";
        if (key == _viewKey)
            return;
        _viewKey = key;
        foreach (var box in _buildingBoxes.Values)
            box.QueueFree();
        _buildingBoxes.Clear();
        _buildingShadows.Clear();
        _sceneLayer?.QueueFree();
        _sceneLayer = null;
        if (!_trialBackground && SceneOf(town.Faction) is { } scene)
        {
            ShowScene(town, scene);
            return;
        }
        // de l'arrière (haut) vers l'avant (bas) : ceux du bas passent devant
        foreach (var building in shown.OrderBy(b => b.View[1] + b.View[3]))
        {
            var box = BuildingBox(town, building);
            _view.AddChild(box);
            _buildingBoxes[building.Id] = box;
        }
    }

    /// <summary>La scène d'une faction (art/towns/&lt;faction&gt;/scene.json), ou null si elle n'a pas encore d'images.</summary>
    private TownScene? SceneOf(string faction)
    {
        if (!_scenes.TryGetValue(faction, out var scene))
        {
            string path = $"{Art.Root}towns/{faction}/scene.json";
            _scenes[faction] = scene = Godot.FileAccess.FileExists(path) ? TownScene.FromJson(Godot.FileAccess.GetFileAsString(path)) : null;
        }
        return scene;
    }

    /// <summary>
    /// La ville en images, ×2, posée en bas au milieu de la vue : le fond, les ombres des bâtiments construits, les
    /// bâtiments du plus loin au plus proche, chacun suivi du décor qui passe devant lui et de ses runes qui flottent.
    /// </summary>
    private void ShowScene(Town town, TownScene scene)
    {
        var size = new Vector2(scene.Width, scene.Height) * ArtScale;
        _sceneLayer = new Control
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = -size.X / 2, OffsetRight = size.X / 2, OffsetTop = -size.Y, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _view.AddChild(_sceneLayer);
        string folder = $"towns/{town.Faction}/";
        TextureRect Layer(string name, float x, float y)
        {
            var texture = Art.Get(folder + name)!;
            var picture = Picture(texture, new Vector2(x, y) * ArtScale, texture.GetSize() * ArtScale);
            _sceneLayer.AddChild(picture);
            return picture;
        }
        Layer("fond", 0, 0);
        // une rune dérive doucement dans une direction tirée au sort en s'allumant puis en s'effaçant, et recommence
        // ailleurs près de sa place, dans une autre direction, chacune à son rythme
        void Float(Control rune, int n)
        {
            var home = rune.Position;
            var rng = new Random(n * 7919 + 17);
            float Next() => (float)rng.NextDouble();
            void Cycle()
            {
                if (!GodotObject.IsInstanceValid(rune))
                    return;
                float time = 4.2f + Next() * 2f, angle = Next() * MathF.Tau, reach = ArtPx(6 + Next() * 6);
                var start = home + new Vector2(Next() - 0.5f, Next() - 0.5f) * ArtPx(8);
                rune.Position = start;
                var tween = rune.CreateTween();
                tween.TweenProperty(rune, "position", start + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach, time)
                    .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.Parallel().TweenProperty(rune, "modulate:a", 1f, time * 0.35).From(0f);
                tween.Parallel().TweenProperty(rune, "modulate:a", 0f, time * 0.4).SetDelay(time * 0.6);
                tween.TweenInterval(Next() * 0.8);
                tween.TweenCallback(Callable.From(Cycle));
            }
            // filtrée en douceur : une rune si petite et si claire sautait d'un pixel à l'autre ; elle glisse entre eux
            rune.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
            rune.Modulate = new Color(1, 1, 1, 0);
            rune.CreateTween().TweenCallback(Callable.From(Cycle)).SetDelay(0.45 * n % 2.2);
        }
        var shown = scene.Shown(town.BuiltBuildings).ToList();
        foreach (var (building, place) in shown.Where(s => s.Place.ShadowX != null))
            _buildingShadows[building.Id] = Layer(place.Id + "-ombre", place.ShadowX!.Value, place.ShadowY!.Value);
        foreach (var (building, place) in shown)
        {
            var sprite = new TownSceneSprite(Art.GetImage(folder + place.Id)!)
            {
                Position = new Vector2(place.X, place.Y) * ArtScale, TooltipText = Texts.BuildingTooltip(town, building),
            };
            sprite.GuiInput += e => ClickBuilding(e, town, building);
            _sceneLayer.AddChild(sprite);
            _buildingBoxes[building.Id] = sprite;
            // le décor qui passe devant lui, tout de suite : les bâtiments plus proches le couvrent encore
            if (place.FrontX != null)
                Layer(place.Id + "-devant", place.FrontX.Value, place.FrontY!.Value);
            foreach (var (rune, n) in (place.Runes ?? new()).Select((r, n) => (r, n)))
                Float(Layer($"rune-{rune[2]}", rune[0], rune[1]), n);
        }
    }

    private Control BuildingBox(Town town, BuildingType building)
    {
        var (x, y, w, h) = (Px(building.View[0]), Px(building.View[1]), Px(building.View[2]), Px(building.View[3]));
        var box = new Panel
        {
            Position = new Vector2(x, y), Size = new Vector2(w, h), PivotOffset = new Vector2(w / 2f, h),
            TooltipText = Texts.BuildingTooltip(town, building), MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        // sur un fond à l'essai, la boîte presque transparente : le fond reste visible derrière
        var fill = MapRenderer.Rgb(107, 98, 86);
        box.AddThemeStyleboxOverride("panel", Box(_trialBackground ? fill with { A = 0.2f } : fill, MapRenderer.Rgb(200, 184, 144), 1, 1, 1, 1));
        var name = MakeLabel(building.Name, 12, TextColor);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(w - Px(8), 0);
        name.Position = new Vector2(Px(4), h / 2f - Px(16));
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(name);
        box.GuiInput += e => ClickBuilding(e, town, building);
        return box;
    }

    /// <summary>Un clic sur un bâtiment l'utilise : recruter, la taverne, la guilde, le marché, le chantier, construire.</summary>
    private void ClickBuilding(InputEvent e, Town town, BuildingType building)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;
        if (town.DwellingCreature(building) is { } creature)
            OpenRecruitWindow(creature);
        else if (building.Tavern)
            OpenTavern();
        else if (building.MageGuild > 0)
            OpenGuild();
        else if (building.Market)
            OpenMarket();
        else if (building.Shipyard)
            OpenShipyard();
        else if (building.DailyGold > 0)
            OpenBuild();
    }

    /// <summary>
    /// Un bâtiment vient d'être construit (événement des règles) : la vue se refait, et le nouveau bâtiment apparaît peu
    /// à peu, en montant du sol, avec un peu de poussière.
    /// </summary>
    public void ShowBuilt(BuildingBuilt built)
    {
        if (built.Town != _town)
            return;
        Refresh();
        if (!_buildingBoxes.TryGetValue(built.Building.Id, out var box))
            return;
        box.Scale = V(1, 0.05f);
        box.Modulate = new Color(1, 1, 1, 0);
        var tween = box.CreateTween().SetParallel();
        tween.TweenProperty(box, "scale", Vector2.One, 1.4).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(box, "modulate", Colors.White, 1.0);
        if (_buildingShadows.TryGetValue(built.Building.Id, out var shadow))
        {
            shadow.Modulate = new Color(1, 1, 1, 0);
            tween.TweenProperty(shadow, "modulate", Colors.White, 1.4);
        }
        // la poussière : de petits carrés qui s'écartent du pied du bâtiment en s'effaçant
        var rng = new Random(built.Building.Id.GetHashCode());
        for (int i = 0; i < 14; i++)
        {
            var foot = box.Position + new Vector2((float)rng.NextDouble() * box.Size.X, box.Size.Y - Px(4));
            var dust = new ColorRect { Position = foot, Size = V(6, 6), Color = MapRenderer.Rgb(190, 175, 140), MouseFilter = Control.MouseFilterEnum.Ignore };
            box.GetParent().AddChild(dust); // la vue, ou la scène de la ville
            var drift = new Vector2((float)(rng.NextDouble() - 0.5) * Px(80), -(float)rng.NextDouble() * Px(30));
            var puff = dust.CreateTween().SetParallel();
            puff.TweenProperty(dust, "position", foot + drift, 1.2).SetEase(Tween.EaseType.Out);
            puff.TweenProperty(dust, "modulate", new Color(1, 1, 1, 0), 1.2);
            puff.Chain().TweenCallback(Callable.From(dust.QueueFree));
        }
    }

    // ----------------------------------------------------------- les fenêtres

    /// <summary>Développement : ouvre une fenêtre de la ville (« build », « recruit », « tavern », « count ») pour une capture.</summary>
    public void ShowWindow(string which)
    {
        if (which == "build")
            OpenBuild();
        else if (which == "recruit")
            OpenRecruitWindow(null);
        else if (which == "tavern")
            OpenTavern();
        else if (which == "guild")
            OpenGuild();
        else if (which == "market")
            OpenMarket();
        else if (which == "count" && _town is { } town
                 && town.BuiltBuildings.Select(town.DwellingCreature).FirstOrDefault(c => c != null) is { } creature)
            OpenRecruit(town, creature);
    }

    /// <summary>
    /// Ouvre une fenêtre par-dessus la ville. <paramref name="overView"/> : seulement sur la vue de la ville (la garnison
    /// et le héros restent utilisables), sinon sur tout l'écran.
    /// </summary>
    private Panel OpenWindow(Vector2 size, string title, bool overView)
    {
        CloseWindow();
        var veil = new ColorRect
        {
            Position = overView ? _view.Position : Vector2.Zero,
            Size = overView ? _view.Size : _screen,
            Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _root.AddChild(veil);
        var window = new Panel { Position = (veil.Size - size) / 2, Size = size };
        window.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        veil.AddChild(window);
        // le titre et « Fermer » comme dans les grandes fenêtres (ScreenWindow)
        var heading = MakeLabel(title, 20, TextColor);
        heading.Position = new Vector2(WindowMargin, WindowMargin + (ButtonHeight - TitleText) / 2);
        window.AddChild(heading);
        var close = Sized(MakeButton(Texts.CloseButton, 14), CloseButtonWidth, ButtonHeight);
        close.TooltipText = Texts.KeyTooltip(Texts.CloseButton, "Échap");
        close.Position = new Vector2(size.X - WindowMargin - CloseButtonWidth, WindowMargin);
        close.Pressed += CloseWindow;
        window.AddChild(close);
        _window = veil;
        return window;
    }

    private void CloseWindow()
    {
        _window?.QueueFree();
        _window = null;
        _reopen = null;
    }

    /// <summary>
    /// La fenêtre Construire : tous les bâtiments de la ville, dans l'ordre des données, ceux qui sont déjà construits
    /// parmi les autres (marqués « Construit ») ; une carte par bâtiment, 3 par rangée.
    /// </summary>
    private void OpenBuild()
    {
        if (_town is not { } town)
            return;
        var buildings = town.Buildings.Where(b => ShownInBuild(town, b)).ToList();
        int rows = Math.Max(1, (buildings.Count + 2) / 3);
        // la fenêtre suit son contenu, sans dépasser l'écran (la grille défile alors)
        float listHeight = Math.Min(rows * 136 - 10, (_screen.Y - Hud.TopBarHeight) / Ui.Scale - 40 - 84 - 20);
        var window = OpenWindow(V(1000, 84 + listHeight + 36), Texts.BuildTitle(town), overView: false);
        window.AddChild(At(MakeLabel(Texts.ToBuild, 14, SoftTextColor), 24, 56));
        // les cartes (à plat) dans une zone creusée
        window.AddChild(SunkenArea(V(12, 80), V(976, listHeight + 16)));
        var scroll = new ScrollContainer
        {
            Position = V(19, 88), Size = V(966, listHeight), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        window.AddChild(scroll);
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", (int)Px(10));
        grid.AddThemeConstantOverride("v_separation", (int)Px(10));
        scroll.AddChild(grid);
        foreach (var building in buildings)
            grid.AddChild(BuildCard(town, building));
    }

    /// <summary>
    /// Un bâtiment qui s'améliore (salle du village → hôtel de ville → grand hôtel de ville, guilde des mages 1 → 4) n'a
    /// qu'une carte dans la fenêtre Construire, comme dans Heroes 3 : le prochain niveau à bâtir, ou le dernier, construit.
    /// </summary>
    private static bool ShownInBuild(Town town, BuildingType building)
    {
        // le chantier naval n'existe que dans une ville au bord de l'eau (choix de l'utilisateur, 2026-11-20)
        if (building.Shipyard && town.BoatSpot == null)
            return false;
        bool hasNext = town.Buildings.Any(b => b.Replaces == building.Id);
        return town.Has(building.Id)
            ? !hasNext // construit : seulement s'il n'y a pas de niveau au-dessus
            : building.Replaces == null || town.Has(building.Replaces); // à bâtir : seulement le niveau qui suit
    }

    /// <summary>Une zone creusée dans la pierre, sous des cartes à plat (bâtiments, créatures, héros de la taverne).</summary>
    private static Panel SunkenArea(Vector2 position, Vector2 size)
    {
        var area = new Panel { Position = position, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
        area.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        return area;
    }

    private Control BuildCard(Town town, BuildingType building)
    {
        bool built = town.Has(building.Id);
        var result = town.CanBuild(building, _state.CurrentPlayer);
        var card = new Panel { CustomMinimumSize = V(314, 126) };
        card.AddThemeStyleboxOverride("panel", Plate(PanelColor, result == BuildResult.Ok ? GoodColor : null));
        if (result is BuildResult.MissingRequirement)
            card.Modulate = new Color(1, 1, 1, 0.6f);
        var x = 8f;
        if (town.DwellingCreature(building) is { } creature && CreaturePicture(creature, new Rect2(V(4, 6), V(52, 52))) is { } image)
        {
            card.AddChild(image);
            x = 62;
        }
        card.AddChild(At(MakeLabel(building.Name, 15, TextColor), x, 6));
        var effect = MakeLabel(Texts.BuildingEffect(town, building), 12, SoftTextColor);
        effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        effect.CustomMinimumSize = V(306 - x, 0);
        effect.Position = V(x, 26);
        // deux lignes au plus, et le prix sur une ligne coupée au bord de la carte (le texte entier au survol)
        effect.MaxLinesVisible = 2;
        effect.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        card.AddChild(effect);
        // le texte après la taille : sinon l'étiquette garde la largeur du texte entier
        var cost = At(MakeLabel("", 12, TextColor), x, 62);
        cost.ClipText = true;
        cost.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        cost.Size = V(306 - x, 20);
        cost.Text = Texts.Cost(building.Cost);
        card.AddChild(cost);
        card.TooltipText = $"{Texts.BuildingEffect(town, building)}\n{Texts.Cost(building.Cost)}";
        if (built)
        {
            card.AddThemeStyleboxOverride("panel", Plate(DarkPanelColor)); // construit : en retrait
            card.AddChild(At(MakeLabel(Texts.BuiltMark, 13, GoodColor), x, 86));
            return card;
        }
        // un bouton seulement si l'on peut construire ; sinon, pourquoi (ressources, bâtiments requis…)
        string status = Texts.BuildStatus(result, town, building);
        if (result != BuildResult.Ok)
        {
            var why = MakeLabel(status, 12, result is BuildResult.MissingRequirement ? DimColor : SoftTextColor);
            why.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            why.CustomMinimumSize = V(306 - x, 0);
            why.Position = V(x, 84);
            card.AddChild(why);
            return card;
        }
        var action = FixedButton(status, 13, new Vector2(Px(298), ButtonHeight));
        action.Position = V(8, 82);
        action.Pressed += () =>
        {
            if (_state.Build(town, building.Id) == BuildResult.Ok)
                CloseWindow(); // l'apparition du bâtiment se voit sur la ville (événement BuildingBuilt)
        };
        card.AddChild(action);
        return card;
    }

    /// <summary>
    /// La fenêtre Recruter : les créatures de la ville, toujours dans le même ordre (ou seulement celle d'une
    /// habitation, d'un clic sur elle). La garnison et le héros restent visibles dessous.
    /// </summary>
    private void OpenRecruitWindow(CreatureType? only)
    {
        if (_town is not { } town)
            return;
        float width = only == null ? 1256 : 420;
        var window = OpenWindow(V(width, 386), Texts.RecruitWindowTitle(town), overView: true);
        window.AddChild(SunkenArea(V(6, 52), V(width - 12, 324)));
        var row = new HBoxContainer { Position = V(10, 55) };
        row.AddThemeConstantOverride("separation", (int)Px(8));
        window.AddChild(row);
        var dwellings = town.Buildings.Where(b => b.Dwelling != null)
            .Select(b => (Building: b, Creature: town.DwellingCreature(b)))
            .Where(d => d.Creature != null && (only == null || d.Creature == only))
            .OrderBy(d => d.Creature!.Tier).ToList();
        foreach (var (building, creature) in dwellings)
            row.AddChild(RecruitCard(town, building, creature!, wide: only != null));
        _singleRecruit = only != null;
        _reopen = () => Callable.From(() =>
        {
            if (_window != null && _town == town)
                OpenRecruitWindow(only);
        }).CallDeferred();
    }

    private Control RecruitCard(Town town, BuildingType dwelling, CreatureType creature, bool wide)
    {
        var card = new Panel { CustomMinimumSize = V(wide ? 400 : 168, 318) };
        card.AddThemeStyleboxOverride("panel", Plate(PanelColor));
        float w = card.CustomMinimumSize.X;
        bool built = town.Has(dwelling.Id);
        if (!built)
            card.Modulate = new Color(1, 1, 1, 0.55f);
        const int pictureBottom = 88;
        if (CreaturePicture(creature, new Rect2(Px(4), Px(4), w - Px(8), Px(pictureBottom - 4))) is { } image)
            card.AddChild(image);
        // les textes les uns sous les autres (une ligne de plus pousse la suite, sans chevauchement), sous l'image (le nom
        // ne mord plus sur les pieds de la créature), les boutons en bas
        var texts = new VBoxContainer { Position = new Vector2(Px(4), Px(pictureBottom + 2)), Size = new Vector2(w - Px(8), 0) };
        texts.AddThemeConstantOverride("separation", 0);
        card.AddChild(texts);
        void Centered(string text, int size, Color color)
        {
            var label = MakeLabel(text, size, color);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(w - Px(8), 0);
            texts.AddChild(label);
        }
        void Gap() => texts.AddChild(new Control { CustomMinimumSize = new Vector2(0, ArtPx(4)) });
        Centered(creature.Name, 15, TextColor);
        Centered(Texts.CreatureLevel(creature), 11, SoftTextColor);
        Gap();
        // les boutons comptés depuis le bas de la carte : le dernier à 4 pixels d'art du bord
        float bottom = card.CustomMinimumSize.Y - ArtPx(4);
        Button Action(string text, int fromBottom)
        {
            var button = MakeButton(text, 13);
            Sized(button, w - Px(16), ButtonHeight);
            button.Position = new Vector2(Px(8), bottom - (fromBottom + 1) * ButtonHeight - fromBottom * ButtonGap);
            card.AddChild(button);
            return button;
        }
        if (!built)
        {
            Centered(Texts.ToBuildFirst(dwelling), 13, TextColor);
            Action(Texts.GoBuild, 0).Pressed += OpenBuild;
            return card;
        }
        int available = town.Available.GetValueOrDefault(creature);
        Centered(Texts.AvailableLine(available, town.WeeklyGrowth(creature)), 12, TextColor);
        Centered(Texts.PriceLine(creature), 12, TextColor);
        Gap();
        Centered(Texts.CreatureStats(creature), 11, SoftTextColor);
        var hero = _state.VisitingHero(town);
        bool hasRoom = town.Garrison.CanReceive(creature) || (hero != null && hero.Army.CanReceive(creature));
        int max = hasRoom ? town.MaxRecruitable(creature, _state.CurrentPlayer) : 0;
        var more = Action(Texts.RecruitMore, 1);
        more.Disabled = available == 0;
        more.Pressed += () => OpenRecruit(town, creature);
        var all = Action(Texts.RecruitAllButton(max), 0);
        all.Disabled = max == 0;
        all.Pressed += () =>
        {
            _state.Recruit(town, creature, max);
            AfterRecruit();
        };
        return card;
    }

    /// <summary>La taverne : les deux héros qu'elle propose, leur prix, et « Recruter ».</summary>
    /// <summary>
    /// Le marché : donner une ressource pour en recevoir une autre (<see cref="Market"/>). Deux rangées de boutons (ce
    /// qu'on donne, avec ce qu'on en a ; ce qu'on reçoit, avec le prix d'une unité), le nombre à recevoir au curseur, le
    /// marché conclu avant de valider.
    /// </summary>
    private void OpenMarket()
    {
        var player = _state.CurrentPlayer;
        int markets = Market.Count(player);
        if (_town == null || markets == 0)
            return;
        var window = OpenWindow(V(860, 372), Texts.MarketTitle, overView: true);
        window.AddChild(At(MakeLabel(Texts.MarketRates(markets), 13, SoftTextColor), 24, 56));

        int have = player.Resources.GetValueOrDefault(_marketGive);
        int max = Market.MaxReceivable(_marketGive, _marketReceive, have, markets);
        _marketCount = Math.Clamp(_marketCount, 0, max);

        Button ResourceButton(ResourceType resource, string text, bool chosen, Action pressed)
        {
            var button = MakeButton(text, 13);
            button.ToggleMode = true;
            button.ButtonPressed = chosen;
            button.Alignment = HorizontalAlignment.Right;
            Sized(button, Px(110), ButtonHeight);
            button.TooltipText = Texts.Capitalize(Texts.Resource(resource));
            button.AddChild(new ResourceIcon { Resource = resource, Position = V(6, 10), Size = V(20, 20), MouseFilter = Control.MouseFilterEnum.Ignore });
            button.Pressed += pressed;
            // la ressource choisie : un cadre d'argent bien visible
            if (chosen)
            {
                var frame = new Panel { Size = button.CustomMinimumSize, MouseFilter = Control.MouseFilterEnum.Ignore };
                frame.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0), SilverColor, 3, 3, 3, 3));
                button.AddChild(frame);
            }
            return button;
        }
        HBoxContainer Row(string label, float y)
        {
            window.AddChild(At(MakeLabel(label, 15, TextColor), 24, y));
            var row = new HBoxContainer { Position = V(24, y + 22) };
            row.AddThemeConstantOverride("separation", (int)Px(6));
            window.AddChild(row);
            return row;
        }
        var giveRow = Row(Texts.MarketGive, 82);
        foreach (var resource in ResourceInfo.All)
            giveRow.AddChild(ResourceButton(resource, player.Resources.GetValueOrDefault(resource).ToString(), resource == _marketGive,
                () => { _marketGive = resource; OpenMarket(); }));
        var receiveRow = Row(Texts.MarketReceive, 156);
        foreach (var resource in ResourceInfo.All)
            receiveRow.AddChild(ResourceButton(resource, Texts.MarketUnitPrice(_marketGive, resource, markets), resource == _marketReceive,
                () => { _marketReceive = resource; OpenMarket(); }));

        // le nombre à recevoir, et le marché conclu
        var slider = new HSlider { Position = V(24, 244), Size = V(560, 24), MinValue = 0, MaxValue = Math.Max(0, max), Step = 1, Value = _marketCount, Editable = max > 0 };
        window.AddChild(slider);
        var max_ = MakeButton(Texts.MaxButton, 13);
        max_.Position = V(600, 238);
        Sized(max_, Px(80), ButtonHeight);
        max_.Disabled = max == 0;
        window.AddChild(max_);
        var deal = At(MakeLabel("", 15, TextColor), 24, 284);
        window.AddChild(deal);
        var trade = MakeButton(Texts.TradeButton, 15);
        trade.Position = V(620, 316);
        Sized(trade, Px(170), ButtonHeight);
        window.AddChild(trade);
        void Show()
        {
            int count = (int)slider.Value;
            deal.Text = max == 0 ? Texts.MarketNothing(_marketGive, _marketReceive)
                : Texts.MarketDeal(_marketGive, Market.Cost(_marketGive, _marketReceive, count, markets), _marketReceive, count);
            trade.Disabled = count == 0;
        }
        slider.ValueChanged += v => { _marketCount = (int)v; Show(); };
        max_.Pressed += () => slider.Value = max;
        trade.Pressed += () =>
        {
            if (_state.Trade(_marketGive, _marketReceive, (int)slider.Value) > 0)
            {
                _marketCount = 0;
                Refresh();
                OpenMarket();
            }
        };
        Show();
    }

    /// <summary>Le chantier naval : construire un bateau, qui apparaît sur l'eau à côté de la ville.</summary>
    private void OpenShipyard()
    {
        if (_town is not { } town)
            return;
        var status = _state.CanBuildBoat(town);
        var window = OpenWindow(V(560, 220), Texts.ShipyardTitle, overView: true);
        var text = At(MakeLabel(Texts.ShipyardText(_state.BoatCost(town.Owner!)), 13, SoftTextColor), 18, 58);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = V(524, 0);
        window.AddChild(text);
        if (status != BoatResult.Ok)
            window.AddChild(At(MakeLabel(Texts.BoatStatus(status), 14, TextColor), 18, 120));
        var build = Sized(MakeButton(Texts.BuildBoatButton, 15), Px(260), ButtonHeight);
        build.Position = V(150, 158);
        build.Disabled = status != BoatResult.Ok;
        build.Pressed += () =>
        {
            _state.BuildBoat(town);
            Refresh();
            OpenShipyard();
        };
        window.AddChild(build);
    }

    private void OpenTavern()
    {
        if (_town is not { } town)
            return;
        var status = _state.CanHireHero(town);
        // si l'on ne peut pas recruter, pourquoi : une fois, sous le texte (pas sur des boutons grisés)
        float cardsY = status == HireResult.Ok ? 100 : 132;
        float cardsHeight = status == HireResult.Ok ? 224 : 182;
        var window = OpenWindow(V(620, cardsY + cardsHeight + 28), Texts.TavernTitle, overView: true);
        window.AddChild(SunkenArea(V(12, cardsY - 6), V(596, cardsHeight + 12)));
        var text = At(MakeLabel(Texts.TavernText, 13, SoftTextColor), 18, 58);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = V(584, 0);
        window.AddChild(text);
        if (status != HireResult.Ok)
            window.AddChild(At(MakeLabel(Texts.HireStatus(status), 14, TextColor), 18, 96));
        float x = 18;
        foreach (var name in _state.TavernOffers(town))
        {
            var card = new Panel { Position = V(x, cardsY), Size = V(284, cardsHeight) };
            card.AddThemeStyleboxOverride("panel", Plate(PanelColor));
            window.AddChild(card);
            if (Art.Portrait(name) is { } portrait)
                card.AddChild(Picture(portrait, new Vector2(Px(284) - 192, Px(16)) / 2, new Vector2(192, 192))); // le portrait ×3
            var label = At(MakeLabel(name, 15, TextColor), 0, 142);
            label.Size = V(284, 20);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            card.AddChild(label);
            // sa classe : celle de la faction de la ville
            if (_state.Data.Classes.ForFaction(town.Faction) is { } heroClass)
            {
                var classLabel = At(MakeLabel(heroClass.Name, 12, SoftTextColor), 0, 160);
                classLabel.Size = V(284, 16);
                classLabel.HorizontalAlignment = HorizontalAlignment.Center;
                card.AddChild(classLabel);
            }
            if (status != HireResult.Ok)
            {
                x += 300;
                continue;
            }
            var hire = FixedButton($"{Texts.HireButton} ({GameState.HeroCost} or)", 13, new Vector2(Px(264), ButtonHeight));
            hire.Position = V(10, 180);
            hire.Pressed += () =>
            {
                if (_state.HireHero(town, name) == HireResult.Ok)
                {
                    CloseWindow();
                    Refresh();
                }
            };
            card.AddChild(hire);
            x += 300;
        }
        if (!town.Buildings.Any(b => b.Tavern && town.Has(b.Id)))
            window.AddChild(At(MakeLabel(Texts.NoTavern, 14, TextColor), 18, 120));
    }

    /// <summary>
    /// La guilde des mages (choix de l'utilisateur, 2026-11-11) : l'académie de la faction de la ville, une scène, et au
    /// milieu le grimoire de la guilde sur son lutrin ; un clic l'ouvre (le livre de sorts,
    /// <see cref="SpellBookWindow.ForGuild"/> : ses sorts niveau par niveau, ceux que le héros présent connaît déjà marqués,
    /// un niveau pas encore construit dit).
    /// </summary>
    private void OpenGuild()
    {
        if (_town is not { } town)
            return;
        var sceneSize = SceneBackdrop.AcademySize * Grid;
        float top = WindowMargin + ButtonHeight + ArtPx(4);
        // ce que fait la guilde, sur une ligne sous la scène (sur la scène, la plaque en cachait le coin)
        var text = MakeLabel(town.MageGuildLevel > 0 ? Texts.GuildText : Texts.NoGuild, 13, TextColor);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.Position = new Vector2(WindowMargin, top + sceneSize.Y + ArtPx(4));
        // sur deux lignes si elle ne tient pas sur une (la hauteur de la fenêtre suit)
        var oneLine = text.GetMinimumSize();
        int lines = (int)MathF.Ceiling(oneLine.X / sceneSize.X);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(sceneSize.X, 0);
        float textHeight = Snap(oneLine.Y * lines);
        var window = OpenWindow(new Vector2(sceneSize.X + 2 * WindowMargin, top + sceneSize.Y + ArtPx(4) + textHeight + WindowMargin),
            Texts.GuildTitle(town), overView: true);
        var scene = new SceneBackdrop { SceneName = $"academie-{town.Faction}", Faction = town.Faction, Size = sceneSize, Position = new Vector2(WindowMargin, top) };
        window.AddChild(scene);
        var lectern = new LecternView { TooltipText = Texts.OpenGuildBookHint, ShowImage = !scene.HasImage, GlowColor = scene.Light };
        lectern.Position = scene.Position + new Vector2(SnapDown((sceneSize.X - lectern.Size.X) / 2), sceneSize.Y - lectern.Size.Y - ArtPx(4));
        lectern.Opened += () =>
        {
            var hero = _state.VisitingHero(town) is { } h && h.Owner == town.Owner ? h : null;
            var book = SpellBookWindow.ForGuild(town, hero);
            _root.AddChild(book); // comme les autres fenêtres de la ville
            book.Open();
        };
        window.AddChild(lectern);
        window.AddChild(text);
        // développement : « --guild-book » ouvre aussitôt le grimoire (capture)
        if (OS.GetCmdlineUserArgs().Contains("--guild-book"))
            Callable.From(lectern.Open).CallDeferred();
    }

    // ----------------------------------------------------------- nombre à recruter

    private void BuildRecruitWindow()
    {
        // Voile sombre qui bloque l'écran de ville tant que la fenêtre est ouverte
        _recruitOverlay = new ColorRect { Color = new Color(0, 0, 0, 0.6f), Size = _screen, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.AddChild(_recruitOverlay);

        var size = V(560, 320);
        var window = new Panel { Position = (_screen - size) / 2, Size = size };
        window.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        _recruitOverlay.AddChild(window);

        _recruitTitle = At(MakeLabel("", 20, TextColor), 20, 14);
        window.AddChild(_recruitTitle);
        // l'image à gauche, et à côté : caractéristiques, disponibles et prix, ce qu'on peut payer, où vont les recrues
        _recruitPlacePosition = V(14, 48);
        _recruitPlace = V(76, 100);
        _recruitPicture = new Control { Position = _recruitPlacePosition, Size = _recruitPlace, MouseFilter = Control.MouseFilterEnum.Ignore };
        window.AddChild(_recruitPicture);
        _recruitStats = At(MakeLabel("", 13, SoftTextColor), 96, 50);
        window.AddChild(_recruitStats);
        _recruitInfo = At(MakeLabel("", 14, TextColor), 96, 92);
        window.AddChild(_recruitInfo);
        var where = At(MakeLabel(Texts.RecruitDestination, 12, SoftTextColor), 96, 132);
        where.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        where.CustomMinimumSize = new Vector2(size.X - Px(116), 0);
        window.AddChild(where);

        _recruitSlider = new HSlider { Position = V(20, 176), Size = new Vector2(size.X - Px(40), Px(24)), Step = 1, FocusMode = Control.FocusModeEnum.None };
        _recruitSlider.ValueChanged += _ => UpdateRecruitTotal();
        window.AddChild(_recruitSlider);
        _recruitTotal = At(MakeLabel("", 16, TextColor), 20, 208);
        _recruitTotal.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _recruitTotal.CustomMinimumSize = new Vector2(size.X - Px(40), 0);
        window.AddChild(_recruitTotal);

        _recruitMax = MakeButton(Texts.MaxButton, 15);
        _recruitMax.Position = new Vector2(Px(20), size.Y - Px(60));
        Sized(_recruitMax, Px(100), ButtonHeight);
        _recruitMax.Pressed += () => _recruitSlider.Value = _recruitSlider.MaxValue;
        window.AddChild(_recruitMax);

        _recruitButton = MakeButton(Texts.RecruitButton, 15);
        _recruitButton.Position = new Vector2(size.X - Px(300), size.Y - Px(60));
        Sized(_recruitButton, Px(140), ButtonHeight);
        _recruitButton.Pressed += ConfirmRecruit;
        window.AddChild(_recruitButton);

        var cancel = MakeButton(Texts.CancelButton, 15);
        cancel.Position = new Vector2(size.X - Px(150), size.Y - Px(60));
        Sized(cancel, Px(130), ButtonHeight);
        cancel.Pressed += () => _recruitOverlay.Visible = false;
        window.AddChild(cancel);
    }

    private void OpenRecruit(Town town, CreatureType type)
    {
        _recruitType = type;
        var player = _state.CurrentPlayer;
        int available = town.Available.GetValueOrDefault(type);
        var hero = _state.VisitingHero(town);
        bool hasRoom = town.Garrison.CanReceive(type) || (hero != null && hero.Army.CanReceive(type));
        int max = hasRoom ? town.MaxRecruitable(type, player) : 0;

        _recruitTitle.Text = Texts.RecruitTitle(type);
        // l'image de combat ×2 (coupée si la créature est trop grande)
        Clear(_recruitPicture);
        if (CreaturePicture(type, new Rect2(Vector2.Zero, _recruitPlace)) is { } picture)
            _recruitPicture.AddChild(picture);
        _recruitStats.Text = Texts.CreatureStats(type);
        _recruitInfo.Text = $"{Texts.RecruitInfo(type, available)}\n{Texts.Affordable(max)}";
        _recruitSlider.MinValue = 0;
        _recruitSlider.MaxValue = max;
        _recruitSlider.Value = max;
        _recruitSlider.Editable = max > 0;
        _recruitMax.Disabled = max == 0;
        UpdateRecruitTotal(Texts.CannotRecruit(available, hasRoom, max));
        // au premier plan : les fenêtres ouvertes après lui (celle des créatures) ne doivent pas le cacher ni prendre ses clics
        _recruitOverlay.MoveToFront();
        _recruitOverlay.Visible = true;
    }

    private void UpdateRecruitTotal(string? problem = null)
    {
        if (_recruitType is not { } type)
            return;
        int count = (int)_recruitSlider.Value;
        string text = problem ?? Texts.RecruitTotal(type, count);
        if (problem == null && _town is { } town && _state.RecruitOverloads(town, type, count) is { } risky)
            text += "\n" + Texts.CommandWarning(risky.CommandLimit);
        _recruitTotal.Text = text;
        _recruitButton.Disabled = count == 0;
    }

    private void ConfirmRecruit()
    {
        if (_town is { } town && _recruitType is { } type)
            _state.Recruit(town, type, (int)_recruitSlider.Value);
        _recruitOverlay.Visible = false;
        AfterRecruit();
    }

    /// <summary>
    /// Après un recrutement : la fenêtre d'une seule habitation (ouverte d'un clic sur elle) se ferme ; celle de toutes
    /// les créatures reste ouverte, à jour, pour en recruter d'autres.
    /// </summary>
    private void AfterRecruit()
    {
        if (_singleRecruit)
            CloseWindow();
        Refresh();
    }
}
