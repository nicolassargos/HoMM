using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'écran de ville, en plein écran par-dessus la carte : les bâtiments (construire),
/// la garnison et le héros présent. Le recrutement et l'échange de troupes viendront ensuite.
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
        _garrisonRow = MakeRow(root, armiesY + 26);
        _heroLabel = At(MakeLabel("", 18, TextColor), Margin, armiesY + 100);
        root.AddChild(_heroLabel);
        _heroRow = MakeRow(root, armiesY + 126);
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
        if (IsOpen && e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
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

        FillArmyRow(_garrisonRow, town.Garrison);
        var hero = _state.VisitingHero(town);
        _heroLabel.Text = hero != null ? $"Héros présent : {hero.Name}" : "Aucun héros dans la ville";
        FillArmyRow(_heroRow, hero?.Army);
    }

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
            content.AddChild(MakeLabel("✓ Construit", 14, GoodColor));
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

    private static void FillArmyRow(HBoxContainer row, Army? army)
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
            row.AddChild(slot);
        }
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
