using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;

namespace HoMM.Game;

/// <summary>
/// L'interface (barre des ressources en haut, panneau à droite), entièrement construite par le code.
/// </summary>
public partial class Hud : CanvasLayer
{
    public const int PanelWidth = 256;
    public const int TopBarHeight = 32;

    private static readonly Color PanelColor = MapRenderer.Rgb(40, 32, 24);
    private static readonly Color BorderColor = MapRenderer.Rgb(190, 160, 90);
    private static readonly Color TextColor = MapRenderer.Rgb(240, 228, 200);
    private static readonly Color SoftTextColor = MapRenderer.Rgb(180, 168, 140);

    private readonly Dictionary<ResourceType, Label> _resourceLabels = new();
    private GameState _state = null!;
    private Label _date = null!;
    private Label _movementText = null!;
    private ProgressBar _movementBar = null!;
    private Label _mines = null!;
    private Label _hover = null!;
    private Label _log = null!;

    public Minimap Minimap { get; private set; } = null!;
    public event Action? EndTurnPressed;

    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        float panelX = screenSize.X - PanelWidth;

        // ---- Barre des ressources
        var topBar = new Panel { Position = Vector2.Zero, Size = new Vector2(panelX, TopBarHeight) };
        topBar.AddThemeStyleboxOverride("panel", Box(PanelColor, BorderColor, bottom: 2));
        AddChild(topBar);

        var resources = new HBoxContainer { Position = new Vector2(12, 0), Size = new Vector2(600, TopBarHeight) };
        resources.AddThemeConstantOverride("separation", 6);
        topBar.AddChild(resources);
        foreach (var resource in ResourceInfo.All)
        {
            resources.AddChild(new ResourceIcon { Resource = resource, CustomMinimumSize = new Vector2(18, TopBarHeight) });
            var label = MakeLabel("", 16, TextColor);
            label.CustomMinimumSize = new Vector2(120, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            resources.AddChild(label);
            _resourceLabels[resource] = label;
        }

        _date = MakeLabel("", 16, TextColor);
        _date.HorizontalAlignment = HorizontalAlignment.Right;
        _date.VerticalAlignment = VerticalAlignment.Center;
        _date.Position = new Vector2(panelX - 316, 0);
        _date.Size = new Vector2(300, TopBarHeight);
        topBar.AddChild(_date);

        // ---- Panneau de droite
        var panel = new Panel { Position = new Vector2(panelX, 0), Size = new Vector2(PanelWidth, screenSize.Y) };
        panel.AddThemeStyleboxOverride("panel", Box(PanelColor, BorderColor, left: 3));
        AddChild(panel);

        const int margin = 16;
        float innerWidth = PanelWidth - 2 * margin;

        int scale = Math.Min((PanelWidth - 16) / state.Map.Width, 6);
        var minimapSize = new Vector2(state.Map.Width, state.Map.Height) * scale;
        var minimapFrame = new Panel
        {
            Position = new Vector2((PanelWidth - minimapSize.X) / 2 - 2, 10),
            Size = minimapSize + new Vector2(4, 4),
        };
        minimapFrame.AddThemeStyleboxOverride("panel", Box(Colors.Black, BorderColor, 2, 2, 2, 2));
        panel.AddChild(minimapFrame);
        Minimap = new Minimap { State = state, CellSize = scale, Position = new Vector2(2, 2), Size = minimapSize, ClipContents = true };
        minimapFrame.AddChild(Minimap);

        float y = minimapFrame.Position.Y + minimapFrame.Size.Y + 12;
        panel.AddChild(At(MakeLabel(state.Hero.Name, 22, TextColor), margin, y));
        y += 34;
        panel.AddChild(At(MakeLabel("Mouvement", 14, SoftTextColor), margin, y));
        y += 22;

        _movementBar = new ProgressBar
        {
            Position = new Vector2(margin, y), Size = new Vector2(innerWidth, 22),
            ShowPercentage = false, MinValue = 0, MaxValue = state.Hero.MaxMovement,
        };
        _movementBar.AddThemeStyleboxOverride("background", Box(MapRenderer.Rgb(20, 16, 12), BorderColor, 1, 1, 1, 1));
        _movementBar.AddThemeStyleboxOverride("fill", Box(MapRenderer.Rgb(60, 180, 60), null));
        panel.AddChild(_movementBar);
        _movementText = MakeLabel("", 12, TextColor);
        _movementText.HorizontalAlignment = HorizontalAlignment.Center;
        _movementText.VerticalAlignment = VerticalAlignment.Center;
        _movementText.Size = _movementBar.Size;
        _movementText.Position = _movementBar.Position;
        panel.AddChild(_movementText);
        y += 34;

        _mines = At(MakeLabel("", 14, SoftTextColor), margin, y);
        _mines.Size = new Vector2(innerWidth, 90);
        panel.AddChild(_mines);
        y += 96;

        _hover = At(MakeLabel("", 13, SoftTextColor), margin, y);
        _hover.Size = new Vector2(innerWidth, 40);
        panel.AddChild(_hover);
        y += 46;

        // ---- Bouton de fin de tour et journal
        var endTurn = new Button
        {
            Text = "Fin du tour",
            Position = new Vector2(margin + 8, screenSize.Y - 60),
            Size = new Vector2(innerWidth - 16, 42),
            FocusMode = Control.FocusModeEnum.None, // sinon Espace/Entrée « cliqueraient » le bouton
        };
        endTurn.AddThemeFontSizeOverride("font_size", 22);
        endTurn.AddThemeColorOverride("font_color", TextColor);
        endTurn.AddThemeColorOverride("font_hover_color", Colors.White);
        endTurn.AddThemeStyleboxOverride("normal", Box(MapRenderer.Rgb(90, 66, 30), BorderColor, 2, 2, 2, 2, radius: 6));
        endTurn.AddThemeStyleboxOverride("hover", Box(MapRenderer.Rgb(120, 90, 40), BorderColor, 2, 2, 2, 2, radius: 6));
        endTurn.AddThemeStyleboxOverride("pressed", Box(MapRenderer.Rgb(70, 50, 20), BorderColor, 2, 2, 2, 2, radius: 6));
        endTurn.Pressed += () => EndTurnPressed?.Invoke();
        panel.AddChild(endTurn);

        _log = At(MakeLabel("", 13, TextColor), margin, y);
        _log.Size = new Vector2(innerWidth, endTurn.Position.Y - 10 - y);
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _log.VerticalAlignment = VerticalAlignment.Bottom;
        _log.ClipText = true;
        panel.AddChild(_log);
    }

    /// <summary>Met à jour les textes. Appelé à chaque image : c'est très peu coûteux.</summary>
    public void Refresh(Cell? hovered)
    {
        foreach (var (resource, label) in _resourceLabels)
            label.Text = $"{Capitalize(ResourceInfo.Name(resource))} : {_state.Player.Resources[resource]}";
        _date.Text = _state.DateText;

        var hero = _state.Hero;
        _movementBar.Value = hero.Movement;
        _movementText.Text = $"{hero.Movement} / {hero.MaxMovement}";

        var mines = _state.Player.Mines;
        _mines.Text = $"Mines : {mines.Count}" + string.Concat(mines.Select(m => $"\n  • {m.Name}"));

        _hover.Text = DescribeCell(hovered);

        // Les derniers messages, du plus ancien au plus récent
        string log = string.Join("\n", _state.Messages.TakeLast(4));
        if (_log.Text != log)
            _log.Text = log;
    }

    private string DescribeCell(Cell? hovered)
    {
        if (hovered is not { } cell || !_state.Map.IsExplored(cell))
            return "";
        var terrain = _state.Map.GetTerrain(cell);
        string text = TerrainInfo.Name(terrain) + (TerrainInfo.IsPassable(terrain)
            ? $" (coût {TerrainInfo.MoveCost(terrain)})"
            : " (infranchissable)");
        var obj = _state.Map.ObjectAt(cell);
        if (obj is Mine mine)
            text += $"\n{mine.Name}" + (mine.Owner != null ? " (à vous)" : " (libre)");
        else if (obj != null)
            text += $"\n{obj.Name}";
        return text;
    }

    // ------------------------------------------------------------------ outils

    private static string Capitalize(string s) => char.ToUpper(s[0]) + s[1..];

    private static T At<T>(T control, float x, float y) where T : Control
    {
        control.Position = new Vector2(x, y);
        return control;
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static StyleBoxFlat Box(Color background, Color? border,
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
}
