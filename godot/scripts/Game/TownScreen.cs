using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'écran de ville (étape 7), en plein écran par-dessus la carte. En haut, la barre des ressources (à la même place
/// que sur la carte) et le nom de la ville. Au centre, la ville vue de face : chaque bâtiment construit à sa place
/// (<see cref="BuildingType.View"/>, des boîtes en attendant les images), un nouveau bâtiment qui apparaît peu à peu ;
/// un clic sur un bâtiment l'utilise (habitation : recruter, taverne, hôtel de ville : construire). En bas, toujours
/// visibles, la garnison et le héros présent : on glisse les troupes (Maj ou clic droit pour partager), un clic les
/// fait passer de l'autre côté. Construire, recruter et la taverne s'ouvrent en fenêtres par-dessus la ville.
/// </summary>
public partial class TownScreen : CanvasLayer
{
    // en unités de l'interface (×1,5 à l'écran), comme les places des bâtiments dans buildings.json (vue de 1280 × 390)
    private const float ViewTop = 84, ViewHeight = 390;
    private const float ArmyX = 100;

    private GameState _state = null!;
    private Town? _town;
    private Vector2 _screen;
    private Control _root = null!;

    private ResourceBar _bar = null!;
    private Label _title = null!;
    private Label _header = null!;
    private Control _view = null!;
    private readonly Dictionary<string, Control> _buildingBoxes = new();
    private string? _viewKey;
    private Button _tavernShortcut = null!;

    private ArmySlots _garrison = null!;
    private ArmySlots _visitor = null!;
    private Label _visitorLabel = null!;
    private TextureRect _visitorPortrait = null!;
    private Button _giveAll = null!, _leaveAll = null!;

    // les fenêtres par-dessus la ville : une à la fois ; le curseur de recrutement par-dessus tout
    private Control? _window;
    private bool _singleRecruit; // la fenêtre Recruter ne montre qu'une habitation (ouverte d'un clic sur elle)
    private Action? _reopen; // pour remettre à jour la fenêtre ouverte (après un recrutement…)

    // fenêtre du nombre à recruter
    private Control _recruitOverlay = null!;
    private TextureRect _recruitPicture = null!;
    private Label _recruitTitle = null!;
    private Label _recruitInfo = null!;
    private Label _recruitStats = null!;
    private HSlider _recruitSlider = null!;
    private Label _recruitTotal = null!;
    private Button _recruitButton = null!;
    private Button _recruitMax = null!;
    private CreatureType? _recruitType;

    public bool IsOpen => _town != null;
    public event Action? Closed;

    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        _screen = screenSize;
        Layer = 5; // au-dessus de la carte, du panneau et des grandes fenêtres
        Visible = false;

        // Le fond arrête la souris : la carte en dessous ne reçoit plus les clics
        _root = new Panel { Size = screenSize, MouseFilter = Control.MouseFilterEnum.Stop };
        _root.AddThemeStyleboxOverride("panel", Box(PanelColor, null));
        AddChild(_root);

        _bar = new ResourceBar();
        _root.AddChild(_bar);
        _bar.Build(screenSize.X - Hud.PanelWidth, Hud.TopBarHeight);
        var back = MakeButton(Texts.QuitTown, 15);
        back.TooltipText = Texts.KeyTooltip(Texts.QuitTownTooltip, "Échap");
        back.Position = new Vector2(screenSize.X - Hud.PanelWidth + Px(12), Px(4));
        back.Size = new Vector2(Hud.PanelWidth - Px(24), Px(36));
        back.Pressed += Close;
        _root.AddChild(back);

        _title = At(MakeLabel("", 22, TextColor), 16, Hud.TopBarHeight / Ui.Scale + 6);
        _root.AddChild(_title);
        _header = At(MakeLabel("", 14, SoftTextColor), 16, Hud.TopBarHeight / Ui.Scale + 10);
        _root.AddChild(_header);

        // la ville vue de face : un ciel, un sol, les bâtiments
        _view = new Control { Position = new Vector2(0, Px(ViewTop)), Size = new Vector2(screenSize.X, Px(ViewHeight)), ClipContents = true };
        _root.AddChild(_view);
        _view.AddChild(new ColorRect { Size = new Vector2(screenSize.X, Px(ViewHeight * 0.4f)), Color = MapRenderer.Rgb(58, 72, 92), MouseFilter = Control.MouseFilterEnum.Ignore });
        _view.AddChild(new ColorRect { Position = new Vector2(0, Px(ViewHeight * 0.4f)), Size = new Vector2(screenSize.X, Px(ViewHeight * 0.6f)), Color = MapRenderer.Rgb(60, 74, 48), MouseFilter = Control.MouseFilterEnum.Ignore });

        // les raccourcis, à droite de la bande du nom (au-dessus de la vue : ils ne cachent aucun bâtiment)
        float bx = screenSize.X - Px(12 + 3 * 138 - 8);
        foreach (var (text, open) in new (string, Action)[] { (Texts.BuildButton, OpenBuild), (Texts.RecruitButton, () => OpenRecruitWindow(null)), (Texts.TavernButton, OpenTavern) })
        {
            var button = MakeButton(text, 14);
            button.Position = new Vector2(bx, Hud.TopBarHeight + Px(3));
            button.Size = V(130, 32);
            button.Pressed += open;
            _root.AddChild(button);
            if (text == Texts.TavernButton)
                _tavernShortcut = button;
            bx += Px(138);
        }

        // la garnison et le héros présent, toujours visibles
        float y = ViewTop + ViewHeight + 8;
        _root.AddChild(At(MakeLabel(Texts.GarrisonHeader, 15, TextColor), 16, y));
        var townPicture = new Panel { Position = V(12, y + 22), Size = new Vector2(132, 132) };
        townPicture.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, DimColor, 1, 1, 1, 1));
        if (Art.Get("objects/town") is { } image)
            townPicture.AddChild(Picture(image, new Vector2(30, 30), new Vector2(72, 72))); // l'image de 72 pixels, ×1
        _root.AddChild(townPicture);
        _garrison = MakeArmy(V(ArmyX, y + 22));
        var hint = At(MakeLabel(Texts.DragHint, 13, SoftTextColor), ArmyX + 7 * 86 + 20, y + 26);
        _root.AddChild(hint);

        y += 110;
        _visitorLabel = At(MakeLabel("", 15, TextColor), 16, y);
        _root.AddChild(_visitorLabel);
        var portraitFrame = new Panel { Position = V(12, y + 22), Size = new Vector2(132, 132) };
        portraitFrame.AddThemeStyleboxOverride("panel", Box(Colors.Black, DimColor, 1, 1, 1, 1));
        _root.AddChild(portraitFrame);
        _visitorPortrait = Picture(null!, new Vector2(2, 2), new Vector2(128, 128)); // le portrait ×2
        portraitFrame.AddChild(_visitorPortrait);
        _visitor = MakeArmy(V(ArmyX, y + 22));
        _giveAll = MakeButton(Texts.GiveAllToHero, 13);
        _giveAll.Position = V(ArmyX + 7 * 86 + 20, y + 30);
        _giveAll.Size = V(200, 34);
        _giveAll.Pressed += GiveAll;
        _root.AddChild(_giveAll);
        _leaveAll = MakeButton(Texts.LeaveAllInGarrison, 13);
        _leaveAll.Position = V(ArmyX + 7 * 86 + 230, y + 30);
        _leaveAll.Size = V(210, 34);
        _leaveAll.Pressed += LeaveAll;
        _root.AddChild(_leaveAll);

        BuildRecruitWindow();
    }

    private ArmySlots MakeArmy(Vector2 position)
    {
        var slots = new ArmySlots { Position = position, SlotSize = V(84, 88), ImageScale = 3, CountFontSize = 14, DragGroup = this };
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
        _viewKey = null;
        Visible = true;
        CloseWindow();
        _recruitOverlay.Visible = false;
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        CloseWindow();
        _town = null;
        Visible = false;
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

    /// <summary>Met l'écran à jour d'après l'état de la ville (après chaque action).</summary>
    public void Refresh()
    {
        if (_town is not { } town)
            return;
        _bar.Refresh(_state, _state.CurrentPlayer);
        _title.Text = town.Name;
        _header.Position = new Vector2(_title.Position.X + _title.GetMinimumSize().X + Px(16), _header.Position.Y);
        _header.Text = Texts.TownHeader(town);
        RefreshView(town);
        _tavernShortcut.Disabled = !town.Buildings.Any(b => b.Tavern && town.Has(b.Id));

        var hero = _state.VisitingHero(town);
        _garrison.Show(town.Garrison, null);
        _visitor.Show(hero?.Army, hero?.Charisma);
        _visitorLabel.Text = hero != null ? Texts.VisitingHero(hero) : Texts.NoVisitor;
        _visitorPortrait.Texture = hero != null ? Art.Portrait(hero.Name) : null;
        _giveAll.Disabled = hero == null || town.Garrison.Stacks.Count == 0;
        _leaveAll.Disabled = hero == null || hero.Army.Stacks.Count <= 1;
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
        // de l'arrière (haut) vers l'avant (bas) : ceux du bas passent devant
        foreach (var building in shown.OrderBy(b => b.View[1] + b.View[3]))
        {
            var box = BuildingBox(town, building);
            _view.AddChild(box);
            _buildingBoxes[building.Id] = box;
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
        box.AddThemeStyleboxOverride("panel", Box(MapRenderer.Rgb(107, 98, 86), MapRenderer.Rgb(200, 184, 144), 1, 1, 1, 1));
        var name = MakeLabel(building.Name, 12, TextColor);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.CustomMinimumSize = new Vector2(w - Px(8), 0);
        name.Position = new Vector2(Px(4), h / 2f - Px(16));
        name.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(name);
        box.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                return;
            if (town.DwellingCreature(building) is { } creature)
                OpenRecruitWindow(creature);
            else if (building.Tavern)
                OpenTavern();
            else if (building.DailyGold > 0)
                OpenBuild();
        };
        return box;
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
        // la poussière : de petits carrés qui s'écartent du pied du bâtiment en s'effaçant
        var rng = new Random(built.Building.Id.GetHashCode());
        for (int i = 0; i < 14; i++)
        {
            var foot = box.Position + new Vector2((float)rng.NextDouble() * box.Size.X, box.Size.Y - Px(4));
            var dust = new ColorRect { Position = foot, Size = V(6, 6), Color = MapRenderer.Rgb(190, 175, 140), MouseFilter = Control.MouseFilterEnum.Ignore };
            _view.AddChild(dust);
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
            Position = new Vector2(0, overView ? Px(ViewTop) : Hud.TopBarHeight),
            Size = new Vector2(_screen.X, overView ? Px(ViewHeight) : _screen.Y - Hud.TopBarHeight),
            Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _root.AddChild(veil);
        var window = new Panel { Position = (veil.Size - size) / 2, Size = size };
        window.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        veil.AddChild(window);
        window.AddChild(At(MakeLabel(title, 20, TextColor), 24, 18));
        var close = MakeButton(Texts.CloseButton, 14);
        close.TooltipText = Texts.KeyTooltip(Texts.CloseButton, "Échap");
        close.Position = new Vector2(size.X - Px(22 + 110), Px(18));
        close.Size = V(110, 30);
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
        var buildings = town.Buildings.ToList();
        int rows = Math.Max(1, (buildings.Count + 2) / 3);
        // la fenêtre suit son contenu, sans dépasser l'écran (la grille défile alors)
        float listHeight = Math.Min(rows * 128 - 10, (_screen.Y - Hud.TopBarHeight) / Ui.Scale - 40 - 84 - 20);
        var window = OpenWindow(V(1000, 84 + listHeight + 20), Texts.BuildTitle(town), overView: false);
        window.AddChild(At(MakeLabel(Texts.ToBuild, 14, SoftTextColor), 24, 56));
        var scroll = new ScrollContainer
        {
            Position = V(18, 84), Size = V(970, listHeight), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        window.AddChild(scroll);
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", (int)Px(10));
        grid.AddThemeConstantOverride("v_separation", (int)Px(10));
        scroll.AddChild(grid);
        foreach (var building in buildings)
            grid.AddChild(BuildCard(town, building));
    }

    private Control BuildCard(Town town, BuildingType building)
    {
        bool built = town.Has(building.Id);
        var result = town.CanBuild(building, _state.CurrentPlayer);
        var card = new Panel { CustomMinimumSize = V(314, 118) };
        card.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, result == BuildResult.Ok ? GoodColor : DimColor, 1, 1, 1, 1));
        if (result is BuildResult.MissingRequirement)
            card.Modulate = new Color(1, 1, 1, 0.6f);
        var x = 8f;
        if (town.DwellingCreature(building) is { } creature && Art.Get($"creatures/{creature.Id}") is { } image)
        {
            card.AddChild(Picture(image, V(6, 8), V(48, 48)));
            x = 62;
        }
        card.AddChild(At(MakeLabel(building.Name, 15, TextColor), x, 6));
        var effect = MakeLabel(Texts.BuildingEffect(town, building), 12, SoftTextColor);
        effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        effect.CustomMinimumSize = V(306 - x, 0);
        effect.Position = V(x, 26);
        card.AddChild(effect);
        card.AddChild(At(MakeLabel(Texts.Cost(building.Cost), 12, TextColor), x, 62));
        if (built)
        {
            card.AddThemeStyleboxOverride("panel", Box(PanelColor, DimColor, 1, 1, 1, 1));
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
        var action = FixedButton(status, 13, V(298, 30));
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
        var window = OpenWindow(V(only == null ? 1256 : 420, 386), Texts.RecruitWindowTitle(town), overView: true);
        var row = new HBoxContainer { Position = V(10, 58) };
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
        card.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, DimColor, 1, 1, 1, 1));
        float w = card.CustomMinimumSize.X;
        bool built = town.Has(dwelling.Id);
        if (!built)
            card.Modulate = new Color(1, 1, 1, 0.55f);
        if (Art.Get($"creatures/{creature.Id}") is { } image)
            card.AddChild(Picture(image, new Vector2((w - 96) / 2, Px(8)), new Vector2(96, 96))); // ×4
        Label Centered(string text, int size, Color color, float y)
        {
            var label = MakeLabel(text, size, color);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(w - Px(8), 0);
            label.Position = V(4, y);
            card.AddChild(label);
            return label;
        }
        Centered(creature.Name, 15, TextColor, 84);
        Centered(Texts.CreatureLevel(creature), 11, SoftTextColor, 104);
        Button Action(string text, float y)
        {
            var button = MakeButton(text, 13);
            button.Position = V(8, y);
            button.Size = new Vector2(w - Px(16), Px(30));
            card.AddChild(button);
            return button;
        }
        if (!built)
        {
            Centered(Texts.ToBuildFirst(dwelling), 13, TextColor, 150);
            Action(Texts.GoBuild, 278).Pressed += OpenBuild;
            return card;
        }
        int available = town.Available.GetValueOrDefault(creature);
        // le niveau et les capacités peuvent prendre deux lignes : la suite commence plus bas
        Centered(Texts.AvailableLine(creature, available), 12, TextColor, 140);
        Centered(Texts.PriceLine(creature), 12, TextColor, 178);
        Centered(Texts.CreatureStats(creature), 11, SoftTextColor, 200);
        var hero = _state.VisitingHero(town);
        bool hasRoom = town.Garrison.CanReceive(creature) || (hero != null && hero.Army.CanReceive(creature));
        int max = hasRoom ? town.MaxRecruitable(creature, _state.CurrentPlayer) : 0;
        var more = Action(Texts.RecruitMore, 242);
        more.Disabled = available == 0;
        more.Pressed += () => OpenRecruit(town, creature);
        var all = Action(Texts.RecruitAllButton(max), 278);
        all.Disabled = max == 0;
        all.Pressed += () =>
        {
            _state.Recruit(town, creature, max);
            AfterRecruit();
        };
        return card;
    }

    /// <summary>La taverne : les deux héros qu'elle propose, leur prix, et « Recruter ».</summary>
    private void OpenTavern()
    {
        if (_town is not { } town)
            return;
        var status = _state.CanHireHero(town);
        // si l'on ne peut pas recruter, pourquoi : une fois, sous le texte (pas sur des boutons grisés)
        float cardsY = status == HireResult.Ok ? 100 : 122;
        var window = OpenWindow(V(620, cardsY + 240 - (status == HireResult.Ok ? 0 : 42)), Texts.TavernTitle, overView: true);
        var text = At(MakeLabel(Texts.TavernText, 13, SoftTextColor), 18, 58);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = V(584, 0);
        window.AddChild(text);
        if (status != HireResult.Ok)
            window.AddChild(At(MakeLabel(Texts.HireStatus(status), 14, TextColor), 18, 90));
        float x = 18;
        foreach (var name in _state.TavernOffers())
        {
            var card = new Panel { Position = V(x, cardsY), Size = V(284, status == HireResult.Ok ? 224 : 182) };
            card.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, DimColor, 1, 1, 1, 1));
            window.AddChild(card);
            if (Art.Portrait(name) is { } portrait)
                card.AddChild(Picture(portrait, new Vector2(Px(284) - 192, Px(16)) / 2, new Vector2(192, 192))); // le portrait ×3
            var label = At(MakeLabel(name, 15, TextColor), 0, 142);
            label.Size = V(284, 20);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            card.AddChild(label);
            if (status != HireResult.Ok)
            {
                x += 300;
                continue;
            }
            var hire = FixedButton($"{Texts.HireButton} ({GameState.HeroCost} or)", 13, V(264, 32));
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
        _recruitPicture = Picture(null!, V(20, 52), new Vector2(96, 96)); // ×4
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

        _recruitMax = MakeButton("Max", 15);
        _recruitMax.Position = new Vector2(Px(20), size.Y - Px(60));
        _recruitMax.Size = V(100, 40);
        _recruitMax.Pressed += () => _recruitSlider.Value = _recruitSlider.MaxValue;
        window.AddChild(_recruitMax);

        _recruitButton = MakeButton(Texts.RecruitButton, 15);
        _recruitButton.Position = new Vector2(size.X - Px(300), size.Y - Px(60));
        _recruitButton.Size = V(140, 40);
        _recruitButton.Pressed += ConfirmRecruit;
        window.AddChild(_recruitButton);

        var cancel = MakeButton(Texts.CancelButton, 15);
        cancel.Position = new Vector2(size.X - Px(150), size.Y - Px(60));
        cancel.Size = V(130, 40);
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
        _recruitPicture.Texture = Art.Get($"creatures/{type.Id}");
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
            text += "\n" + Texts.CommandWarning(Command.Limit(risky.Charisma));
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
