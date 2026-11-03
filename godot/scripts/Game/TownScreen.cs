using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'écran de ville, en plein écran par-dessus la carte : les bâtiments (construire, recruter),
/// la garnison et le héros présent (cliquer sur une troupe la fait passer de l'autre côté).
/// </summary>
public partial class TownScreen : CanvasLayer
{
    private const int Margin = 24;
    private const int Columns = 4;
    private const int CardHeight = 118;
    private const int SlotWidth = 150;
    private const int SlotHeight = 58;

    private GameState _state = null!;
    private Town? _town;
    private Vector2 _screen;

    private Label _title = null!;
    private Label _resources = null!;
    private GridContainer _buildings = null!;
    private HBoxContainer _garrisonRow = null!;
    private Label _heroLabel = null!;
    private HBoxContainer _heroRow = null!;
    private Label _hint = null!;

    // Fenêtre de recrutement
    private Control _recruitOverlay = null!;
    private TextureRect _recruitPicture = null!;
    private Label _recruitTitle = null!;
    private Label _recruitInfo = null!;
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
        Layer = 5; // au-dessus de la carte et du panneau
        Visible = false;

        // Le fond arrête la souris : la carte en dessous ne reçoit plus les clics
        var root = new Panel { Size = screenSize, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        AddChild(root);

        _title = At(MakeLabel("", 28, TextColor), Margin, 10);
        root.AddChild(_title);
        _resources = At(MakeLabel("", 15, SoftTextColor), Margin, 52);
        root.AddChild(_resources);

        var back = MakeButton("Retour (Échap)");
        back.Position = new Vector2(screenSize.X - Margin - 190, 14);
        back.Size = new Vector2(190, 40);
        back.Pressed += Close;
        root.AddChild(back);

        // Le reste se suit de haut en bas : les armées restent sous les bâtiments, quelle que soit leur hauteur
        var page = new VBoxContainer { Position = new Vector2(Margin, 86) };
        page.AddThemeConstantOverride("separation", 4);
        root.AddChild(page);
        page.AddChild(MakeLabel("Bâtiments", 18, TextColor));
        _buildings = new GridContainer { Columns = Columns };
        _buildings.AddThemeConstantOverride("h_separation", 10);
        _buildings.AddThemeConstantOverride("v_separation", 6);
        page.AddChild(_buildings);

        var garrisonTitle = new HBoxContainer();
        garrisonTitle.AddThemeConstantOverride("separation", 24);
        garrisonTitle.AddChild(MakeLabel("Garnison", 18, TextColor));
        _hint = MakeLabel(Texts.TransferHint, 13, SoftTextColor);
        _hint.VerticalAlignment = VerticalAlignment.Center;
        garrisonTitle.AddChild(_hint);
        page.AddChild(garrisonTitle);
        _garrisonRow = MakeRow(page);
        _heroLabel = MakeLabel("", 18, TextColor);
        page.AddChild(_heroLabel);
        _heroRow = MakeRow(page);

        BuildRecruitWindow(root);
    }

    private static HBoxContainer MakeRow(Control parent)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);
        return row;
    }

    public void Open(Town town)
    {
        _town = town;
        Visible = true;
        _recruitOverlay.Visible = false;
        Refresh();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        _town = null;
        Visible = false;
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!IsOpen || e is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            return;
        // Échap ferme d'abord la fenêtre de recrutement, puis l'écran de ville
        if (_recruitOverlay.Visible)
            _recruitOverlay.Visible = false;
        else
            Close();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Reconstruit tout l'écran à partir de l'état de la ville (après chaque action).</summary>
    public void Refresh()
    {
        if (_town is not { } town)
            return;

        _title.Text = town.Name;
        var player = _state.CurrentPlayer;
        _resources.Text = string.Join("     ", ResourceInfo.All.Select(r =>
            $"{Texts.Capitalize(Texts.Resource(r))} : {player.Resources.GetValueOrDefault(r)}"))
            + $"     ·     Revenus de la ville : +{town.DailyGold} or par jour";

        Clear(_buildings);
        foreach (var building in town.Buildings)
            _buildings.AddChild(BuildingCard(town, building));

        var hero = _state.VisitingHero(town);
        // Info-bulle d'avertissement si la troupe ferait dépasser la limite de commandement du héros
        FillArmyRow(_garrisonRow, town.Garrison, index => _state.TransferStack(town, fromGarrison: true, index),
            stack => OverLimitWarning(hero, stack.Type, stack.Count + (hero?.Army.Stacks.FirstOrDefault(s => s.Type == stack.Type)?.Count ?? 0)));
        _heroLabel.Text = hero != null ? $"Héros présent : {hero.Name}" : "Aucun héros dans la ville";
        FillArmyRow(_heroRow, hero?.Army, index => _state.TransferStack(town, fromGarrison: false, index),
            stack => OverLimitWarning(hero, stack.Type, stack.Count));
        _hint.Visible = hero != null;
    }

    // -------------------------------------------------------------- bâtiments

    private Control BuildingCard(Town town, BuildingType building)
    {
        var result = town.CanBuild(building, _state.CurrentPlayer);
        bool built = result == BuildResult.AlreadyBuilt;
        // le cadre est teinté : normal si construit, vert si on peut construire, terne sinon
        var tint = built ? Colors.White : result == BuildResult.Ok ? new Color(0.7f, 1.15f, 0.7f) : new Color(0.6f, 0.58f, 0.55f);

        // Largeur fixe : les textes trop longs vont à la ligne ou sont coupés, la carte ne s'élargit jamais
        float width = (_screen.X - 2 * Margin - (Columns - 1) * 10) / Columns;
        var card = new PanelContainer { CustomMinimumSize = new Vector2(width, CardHeight), Size = new Vector2(width, CardHeight) };
        card.AddThemeStyleboxOverride("panel", Frame(DarkPanelColor));
        card.SelfModulate = tint;

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 4);
        card.AddChild(content);

        // En haut : l'image de la créature d'une habitation, puis le nom et l'effet du bâtiment
        var header = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 8);
        if (town.DwellingCreature(building) is { } dwellingCreature && CreatureImage(dwellingCreature) is { } picture)
        {
            picture.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            header.AddChild(picture);
        }
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 0);
        var title = MakeLabel(building.Name, 16, built ? TextColor : SoftTextColor);
        title.ClipText = true;
        words.AddChild(title);
        var effect = MakeLabel(Texts.BuildingEffect(town, building), 12, SoftTextColor);
        effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.AddChild(effect);
        if (!built)
        {
            var cost = MakeLabel("Coût : " + Texts.Cost(building.Cost), 12, SoftTextColor);
            cost.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            words.AddChild(cost);
        }
        header.AddChild(words);
        content.AddChild(header);

        // En bas : « Construit » et l'action possible (recruter, engager un héros), ou le bouton Construire
        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 8);
        content.AddChild(bottom);
        Button? action = null;
        if (built)
        {
            var done = MakeLabel("✓ Construit", 12, GoodColor);
            done.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            done.SizeFlagsStretchRatio = 0.8f;
            done.VerticalAlignment = VerticalAlignment.Center;
            bottom.AddChild(done);
            // Une habitation construite : on peut y recruter
            if (town.DwellingCreature(building) is { } creature)
            {
                action = MakeButton("Recruter", 13);
                action.Disabled = town.Available.GetValueOrDefault(creature) == 0;
                action.Pressed += () => OpenRecruit(town, creature);
            }
            // La taverne : on peut y recruter un héros
            else if (building.Tavern)
            {
                var hire = _state.CanHireHero(town);
                action = MakeButton(Texts.HireStatus(hire), 13);
                action.Disabled = hire != HireResult.Ok;
                action.Pressed += () =>
                {
                    _state.HireHero(town);
                    Refresh();
                };
            }
        }
        else
        {
            action = MakeButton(Texts.BuildStatus(result, town, building), 13);
            action.Disabled = result != BuildResult.Ok;
            action.Pressed += () =>
            {
                _state.Build(town, building.Id);
                Refresh();
            };
        }
        if (action != null)
        {
            action.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; // la place laissée par « Construit »
            action.ClipText = true;
            action.TooltipText = action.Text; // le texte entier, s'il est coupé
            bottom.AddChild(action);
        }
        return card;
    }

    // ---------------------------------------------------------------- armées

    /// <summary>Les 7 emplacements d'une armée. Un clic sur une troupe appelle <paramref name="onClick"/> avec son numéro.</summary>
    /// <summary>L'avertissement de commandement pour une troupe du héros, ou null si tout va bien.</summary>
    private static string? OverLimitWarning(Hero? hero, CreatureType type, int count) =>
        hero != null && Command.IsOverLimit(type, count, hero.Charisma) ? Texts.CommandWarning(Command.Limit(hero.Charisma)) : null;

    /// <summary>Les 7 emplacements d'une armée. Un clic sur une troupe appelle <paramref name="onClick"/> avec son numéro.
    /// <paramref name="warning"/> donne l'avertissement affiché au survol d'une troupe, ou null.</summary>
    private void FillArmyRow(HBoxContainer row, Army? army, Func<int, bool> onClick, Func<CreatureStack, string?>? warning = null)
    {
        Clear(row);
        for (int i = 0; i < Army.MaxStacks; i++)
        {
            var stack = army != null && i < army.Stacks.Count ? army.Stacks[i] : null;
            var slot = new PanelContainer { CustomMinimumSize = new Vector2(SlotWidth, SlotHeight) };
            slot.AddThemeStyleboxOverride("panel", Frame(DarkPanelColor));
            if (stack == null)
                slot.SelfModulate = new Color(0.6f, 0.58f, 0.55f);
            slot.TooltipText = stack != null ? warning?.Invoke(stack) ?? "" : "";
            var label = MakeLabel(stack != null ? $"{stack.Count}\n{Texts.Creature(stack.Type, stack.Count)}" : "", 14, TextColor);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            if (stack != null && CreatureImage(stack.Type) is { } picture)
            {
                var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                line.AddThemeConstantOverride("separation", 2);
                line.AddChild(picture);
                label.HorizontalAlignment = HorizontalAlignment.Left;
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                line.AddChild(label);
                slot.AddChild(line);
            }
            else
                slot.AddChild(label);

            if (stack != null)
            {
                int index = i;
                slot.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
                slot.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && onClick(index))
                        Callable.From(Refresh).CallDeferred(); // pas pendant le traitement du clic sur ce même emplacement
                };
            }
            row.AddChild(slot);
        }
    }

    // ----------------------------------------------------------- recrutement

    private void BuildRecruitWindow(Control root)
    {
        // Voile sombre qui bloque l'écran de ville tant que la fenêtre est ouverte
        _recruitOverlay = new ColorRect { Color = new Color(0, 0, 0, 0.6f), Size = _screen, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        root.AddChild(_recruitOverlay);

        var size = new Vector2(480, 240);
        var window = new Panel { Position = (_screen - size) / 2, Size = size };
        window.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        _recruitOverlay.AddChild(window);

        _recruitTitle = At(MakeLabel("", 22, TextColor), 20, 14);
        window.AddChild(_recruitTitle);
        _recruitPicture = new TextureRect
        {
            Position = new Vector2(size.X - 20 - 72, 14), Size = new Vector2(72, 72),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        window.AddChild(_recruitPicture);
        _recruitInfo = At(MakeLabel("", 14, SoftTextColor), 20, 50);
        window.AddChild(_recruitInfo);

        _recruitSlider = new HSlider { Position = new Vector2(20, 90), Size = new Vector2(size.X - 40, 24), Step = 1, FocusMode = Control.FocusModeEnum.None };
        _recruitSlider.ValueChanged += _ => UpdateRecruitTotal();
        window.AddChild(_recruitSlider);
        _recruitTotal = At(MakeLabel("", 16, TextColor), 20, 124);
        _recruitTotal.Size = new Vector2(size.X - 40, 24);
        window.AddChild(_recruitTotal);

        _recruitMax = MakeButton("Max", 15);
        _recruitMax.Position = new Vector2(20, size.Y - 60);
        _recruitMax.Size = new Vector2(100, 40);
        _recruitMax.Pressed += () => _recruitSlider.Value = _recruitSlider.MaxValue;
        window.AddChild(_recruitMax);

        _recruitButton = MakeButton("Recruter", 15);
        _recruitButton.Position = new Vector2(size.X - 300, size.Y - 60);
        _recruitButton.Size = new Vector2(140, 40);
        _recruitButton.Pressed += ConfirmRecruit;
        window.AddChild(_recruitButton);

        var cancel = MakeButton("Annuler", 15);
        cancel.Position = new Vector2(size.X - 150, size.Y - 60);
        cancel.Size = new Vector2(130, 40);
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
        _recruitPicture.Texture = Art.Get($"creatures/{type.Id}"); // agrandie ×3
        _recruitInfo.Text = Texts.RecruitInfo(type, available);
        _recruitSlider.MinValue = 0;
        _recruitSlider.MaxValue = max;
        _recruitSlider.Value = max;
        _recruitSlider.Editable = max > 0;
        _recruitMax.Disabled = max == 0;
        UpdateRecruitTotal(Texts.CannotRecruit(available, hasRoom, max));
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
        Refresh();
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
