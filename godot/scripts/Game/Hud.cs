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
    private Label _heroName = null!;
    private Label _army = null!;
    private Label _mines = null!;

    // Prévision de combat mise en cache : la simulation n'est refaite que si quelque chose change
    private (MonsterGuard Guard, int GuardCount, int ArmyCount)? _predictionKey;
    private string _predictionText = "";
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
        _heroName = At(MakeLabel("", 22, TextColor), margin, y);
        panel.AddChild(_heroName);
        y += 34;
        panel.AddChild(At(MakeLabel("Mouvement", 14, SoftTextColor), margin, y));
        y += 22;

        _movementBar = new ProgressBar
        {
            Position = new Vector2(margin, y), Size = new Vector2(innerWidth, 22),
            ShowPercentage = false, MinValue = 0, MaxValue = Hero.DefaultMovement,
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

        panel.AddChild(At(MakeLabel("Armée", 14, SoftTextColor), margin, y));
        y += 20;
        _army = At(MakeLabel("", 13, TextColor), margin + 8, y);
        _army.Size = new Vector2(innerWidth - 8, 118);
        panel.AddChild(_army);
        y += 122;

        _mines = At(MakeLabel("", 13, SoftTextColor), margin, y);
        _mines.Size = new Vector2(innerWidth, 20);
        _mines.ClipText = true;
        panel.AddChild(_mines);
        y += 24;

        _hover = At(MakeLabel("", 13, SoftTextColor), margin, y);
        _hover.Size = new Vector2(innerWidth, 54);
        _hover.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_hover);
        y += 60;

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
        var player = _state.CurrentPlayer;
        foreach (var (resource, label) in _resourceLabels)
            label.Text = $"{Capitalize(ResourceInfo.Name(resource))} : {player.Resources[resource]}";
        _date.Text = _state.DateText;

        if (_state.CurrentHero is { } hero)
        {
            _heroName.Text = hero.Name;
            _movementBar.MaxValue = hero.MaxMovement;
            _movementBar.Value = hero.Movement;
            _movementText.Text = $"{hero.Movement} / {hero.MaxMovement}";
            _army.Text = string.Join("\n", hero.Army.Stacks.Select(s => $"{s.Count}  {s.Type.NameFor(s.Count)}"));
        }
        else
        {
            _heroName.Text = "Aucun héros";
            _movementBar.Value = 0;
            _movementText.Text = "";
            _army.Text = "";
        }

        var income = player.DailyIncome();
        _mines.Text = player.Mines.Count == 0
            ? "Mines : aucune"
            : $"Mines : {player.Mines.Count} (" + string.Join(", ", income.Select(i => $"+{i.Value} {ResourceInfo.Name(i.Key)}")) + "/jour)";

        _hover.Text = DescribeCell(hovered);

        // Les derniers messages, du plus ancien au plus récent
        string log = string.Join("\n", _state.Messages.TakeLast(4));
        if (_log.Text != log)
            _log.Text = log;
    }

    private string DescribeCell(Cell? hovered)
    {
        if (hovered is not { } cell || !_state.CurrentPlayer.Fog.IsExplored(cell))
            return "";
        var terrain = _state.Map.GetTerrain(cell);
        string text = TerrainInfo.Name(terrain) + (TerrainInfo.IsPassable(terrain)
            ? $" (coût {TerrainInfo.MoveCost(terrain)})"
            : " (infranchissable)");
        var obj = _state.Map.ObjectAt(cell);
        if (obj is Mine mine)
            text += $"\n{mine.Name}" + (mine.Owner == _state.CurrentPlayer ? " (à vous)" : mine.Owner != null ? " (ennemie)" : " (libre)");
        else if (obj is MonsterGuard guard)
            text += $"\n{guard.Name}\n{Prediction(guard)}";
        else if (obj != null)
            text += $"\n{obj.Name}";
        if (obj is not MonsterGuard && _state.Map.IsGuarded(cell))
            text += "\nGardé par un monstre";
        return text;
    }

    /// <summary>Danger estimé d'un combat, en simulant le combat sur des copies des armées.</summary>
    private string Prediction(MonsterGuard guard)
    {
        int armyCount = _state.CurrentHero?.Army.TotalCount ?? 0;
        var key = (guard, guard.Count, armyCount);
        if (_predictionKey == key)
            return _predictionText;
        _predictionKey = key;

        var report = _state.PredictBattle(guard);
        if (report == null || armyCount == 0)
            _predictionText = "";
        else if (!report.AttackerWon)
            _predictionText = "Danger : mortel !";
        else
        {
            double lost = report.AttackerLosses.Sum(s => s.Count) / (double)armyCount;
            string word = lost switch { < 0.05 => "faible", < 0.25 => "modéré", < 0.6 => "élevé", _ => "très élevé" };
            _predictionText = $"Danger : {word} (pertes ≈ {lost:P0})";
        }
        return _predictionText;
    }

    // --------------------------------------------------------------- fenêtres

    /// <summary>Fenêtre du compte rendu de combat.</summary>
    public void ShowBattle(BattleReport report, Action? onClose = null)
    {
        string text = (report.AttackerWon ? "Victoire !" : "Défaite...")
            + $"\n\nVos pertes : {BattleReport.DescribeLosses(report.AttackerLosses)}"
            + $"\nPertes ennemies : {BattleReport.DescribeLosses(report.DefenderLosses)}";
        ShowDialog(report.Title, text, "OK", onClose);
    }

    /// <summary>Fenêtre de fin de partie, avec un bouton pour recommencer.</summary>
    public void ShowGameOver(GameOutcome outcome, Action newGame)
    {
        string text = outcome == GameOutcome.Victory
            ? "Tous les monstres ont été vaincus. La région est à vous !"
            : "Votre dernier héros est tombé. La partie est perdue.";
        ShowDialog(outcome == GameOutcome.Victory ? "Victoire" : "Défaite", text, "Nouvelle partie", newGame);
    }

    /// <summary>True tant qu'une fenêtre est ouverte (le jeu ignore alors les commandes de la carte).</summary>
    public bool IsDialogOpen => _openDialogs > 0;
    private int _openDialogs;

    private void ShowDialog(string title, string text, string button, Action? onClose)
    {
        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = text,
            OkButtonText = button,
            Exclusive = true,
            Unresizable = true,
        };
        _openDialogs++;
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            _openDialogs--;
            dialog.QueueFree();
            onClose?.Invoke();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(360, 0));
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
