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
    private const int CardHeight = 112;
    private const int SlotWidth = 150;
    private const int SlotHeight = 64;

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
        root.AddThemeStyleboxOverride("panel", Box(PanelColor, null));
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

        root.AddChild(At(MakeLabel("Bâtiments", 18, TextColor), Margin, 86));
        _buildings = new GridContainer { Columns = Columns, Position = new Vector2(Margin, 114) };
        _buildings.AddThemeConstantOverride("h_separation", 10);
        _buildings.AddThemeConstantOverride("v_separation", 10);
        root.AddChild(_buildings);

        float armiesY = 114 + 3 * (CardHeight + 10) + 8;
        root.AddChild(At(MakeLabel("Garnison", 18, TextColor), Margin, armiesY));
        _hint = At(MakeLabel(Texts.TransferHint, 13, SoftTextColor), Margin + 120, armiesY + 4);
        root.AddChild(_hint);
        _garrisonRow = MakeRow(root, armiesY + 26);
        _heroLabel = At(MakeLabel("", 18, TextColor), Margin, armiesY + 100);
        root.AddChild(_heroLabel);
        _heroRow = MakeRow(root, armiesY + 126);

        BuildRecruitWindow(root);
    }

    private static HBoxContainer MakeRow(Control parent, float y)
    {
        var row = new HBoxContainer { Position = new Vector2(Margin, y) };
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
        FillArmyRow(_garrisonRow, town.Garrison, index => _state.TransferStack(town, fromGarrison: true, index));
        _heroLabel.Text = hero != null ? $"Héros présent : {hero.Name}" : "Aucun héros dans la ville";
        FillArmyRow(_heroRow, hero?.Army, index => _state.TransferStack(town, fromGarrison: false, index));
        _hint.Visible = hero != null;
    }

    // -------------------------------------------------------------- bâtiments

    private Control BuildingCard(Town town, BuildingType building)
    {
        var result = town.CanBuild(building, _state.CurrentPlayer);
        bool built = result == BuildResult.AlreadyBuilt;
        var border = built ? BorderColor : result == BuildResult.Ok ? GoodColor : DimColor;

        float width = (_screen.X - 2 * Margin - (Columns - 1) * 10) / Columns;
        var card = new PanelContainer { CustomMinimumSize = new Vector2(width, CardHeight) };
        card.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, border, 2, 2, 2, 2, radius: 6));

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 2);
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 8);
        margin.AddChild(content);
        card.AddChild(margin);

        content.AddChild(MakeLabel(building.Name, 16, built ? TextColor : SoftTextColor));
        content.AddChild(MakeLabel(Texts.BuildingEffect(town, building), 12, SoftTextColor));

        if (built)
        {
            content.AddChild(MakeLabel("✓ Construit", 12, GoodColor));
            // Une habitation construite : on peut y recruter
            if (town.DwellingCreature(building) is { } creature)
            {
                var recruit = MakeButton("Recruter", 13);
                recruit.Disabled = town.Available.GetValueOrDefault(creature) == 0;
                recruit.Pressed += () => OpenRecruit(town, creature);
                content.AddChild(recruit);
            }
            return card;
        }

        content.AddChild(MakeLabel("Coût : " + Texts.Cost(building.Cost), 12, SoftTextColor));
        var button = MakeButton(Texts.BuildStatus(result, town, building), 13);
        button.Disabled = result != BuildResult.Ok;
        button.ClipText = true;
        button.Pressed += () =>
        {
            _state.Build(town, building.Id);
            Refresh();
        };
        content.AddChild(button);
        return card;
    }

    // ---------------------------------------------------------------- armées

    /// <summary>Les 7 emplacements d'une armée. Un clic sur une troupe appelle <paramref name="onClick"/> avec son numéro.</summary>
    private void FillArmyRow(HBoxContainer row, Army? army, Func<int, bool> onClick)
    {
        Clear(row);
        for (int i = 0; i < Army.MaxStacks; i++)
        {
            var stack = army != null && i < army.Stacks.Count ? army.Stacks[i] : null;
            var slot = new PanelContainer { CustomMinimumSize = new Vector2(SlotWidth, SlotHeight) };
            slot.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, stack != null ? BorderColor : DimColor, 1, 1, 1, 1, radius: 4));
            var label = MakeLabel(stack != null ? $"{stack.Count}\n{Texts.Creature(stack.Type, stack.Count)}" : "", 14, TextColor);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
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
        window.AddThemeStyleboxOverride("panel", Box(PanelColor, BorderColor, 2, 2, 2, 2, radius: 8));
        _recruitOverlay.AddChild(window);

        _recruitTitle = At(MakeLabel("", 22, TextColor), 20, 14);
        window.AddChild(_recruitTitle);
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
        _recruitTotal.Text = problem ?? Texts.RecruitTotal(type, count);
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
