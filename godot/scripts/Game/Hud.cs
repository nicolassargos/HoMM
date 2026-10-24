using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'interface (barre des ressources en haut, panneau à droite), entièrement construite par le code.
/// </summary>
public partial class Hud : CanvasLayer
{
    public const int PanelWidth = 256;
    public const int TopBarHeight = 32;

    private readonly Dictionary<ResourceType, Label> _resourceLabels = new();
    private GameState _state = null!;
    private Label _date = null!;
    private Label _movementText = null!;
    private ProgressBar _movementBar = null!;
    private Label _heroName = null!;
    private Label _army = null!;
    private Label _mines = null!;

    // Prévision de combat mise en cache : la simulation n'est refaite que si quelque chose change
    private (Army Enemy, int EnemyCount, int ArmyCount)? _predictionKey;
    private string _predictionText = "";
    private Label _hover = null!;
    private Label _log = null!;
    private readonly List<string> _messages = new();
    private const int MaxMessages = 6;

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
        _mines.Size = new Vector2(innerWidth, 36);
        _mines.ClipText = true;
        panel.AddChild(_mines);
        y += 40;

        _hover = At(MakeLabel("", 13, SoftTextColor), margin, y);
        _hover.Size = new Vector2(innerWidth, 54);
        _hover.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_hover);
        y += 60;

        // ---- Bouton de fin de tour et journal
        var endTurn = MakeButton("Fin du tour", 22);
        endTurn.Position = new Vector2(margin + 8, screenSize.Y - 60);
        endTurn.Size = new Vector2(innerWidth - 16, 42);
        endTurn.Pressed += () => EndTurnPressed?.Invoke();
        panel.AddChild(endTurn);

        _log = At(MakeLabel("", 13, TextColor), margin, y);
        _log.Size = new Vector2(innerWidth, endTurn.Position.Y - 10 - y);
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _log.VerticalAlignment = VerticalAlignment.Bottom;
        _log.ClipText = true;
        panel.AddChild(_log);

        if (state.CurrentHero is { } hero)
            AddMessage(Texts.Welcome(hero));
    }

    /// <summary>Met à jour les textes. Appelé à chaque image : c'est très peu coûteux.</summary>
    public void Refresh(Cell? hovered)
    {
        var player = _state.CurrentPlayer;
        foreach (var (resource, label) in _resourceLabels)
            label.Text = $"{Texts.Capitalize(Texts.Resource(resource))} : {player.Resources[resource]}";
        _date.Text = Texts.Date(_state.Day);

        if (_state.CurrentHero is { } hero)
        {
            _heroName.Text = hero.Name;
            _movementBar.MaxValue = hero.MaxMovement;
            _movementBar.Value = hero.Movement;
            _movementText.Text = $"{hero.Movement} / {hero.MaxMovement}";
            _army.Text = string.Join("\n", hero.Army.Stacks.Select(s => $"{s.Count}  {Texts.Creature(s.Type, s.Count)}"));
        }
        else
        {
            _heroName.Text = "Aucun héros";
            _movementBar.Value = 0;
            _movementText.Text = "";
            _army.Text = "";
        }

        var income = player.DailyIncome();
        _mines.Text = $"Villes : {player.Towns.Count} · Mines : {player.Mines.Count}\n"
            + (income.Count == 0 ? "Revenus : aucun" : $"Revenus : {Texts.Income(income)} /jour");

        _hover.Text = DescribeCell(hovered);

        // Les derniers messages, du plus ancien au plus récent
        string log = string.Join("\n", _messages.TakeLast(4));
        if (_log.Text != log)
            _log.Text = log;
    }

    /// <summary>Ajoute une ligne au journal (seules les dernières sont gardées).</summary>
    public void AddMessage(string text)
    {
        _messages.Add(text);
        if (_messages.Count > MaxMessages)
            _messages.RemoveAt(0);
    }

    private string DescribeCell(Cell? hovered)
    {
        if (hovered is not { } cell || !_state.CurrentPlayer.Fog.IsExplored(cell))
            return "";
        var terrain = _state.Map.GetTerrain(cell);
        string text = Texts.Terrain(terrain) + (TerrainInfo.IsPassable(terrain)
            ? $" (coût {TerrainInfo.MoveCost(terrain)})"
            : " (infranchissable)");
        var obj = _state.Map.OccupantAt(cell);
        if (obj is Mine mine)
            text += $"\n{Texts.MineName(mine)}" + (mine.Owner == _state.CurrentPlayer ? " (à vous)" : mine.Owner != null ? " (ennemie)" : " (libre)");
        else if (obj is MonsterGuard guard)
            text += $"\n{Texts.ObjectName(guard)}\n{Prediction(guard.Army)}";
        else if (obj is Town town && town.Owner == _state.CurrentPlayer)
            text += $"\n{town.Name} (à vous)";
        else if (obj is Town other)
        {
            text += $"\n{other.Name}" + (other.Owner != null ? " (ennemie)" : " (neutre)");
            if (!other.Garrison.IsEmpty)
                text += $"\nGarnison : {Prediction(other.Garrison)}";
        }
        else if (obj != null)
            text += $"\n{Texts.ObjectName(obj)}";
        if (obj is not MonsterGuard && _state.Map.IsGuarded(cell))
            text += "\nGardé par un monstre";
        return text;
    }

    /// <summary>Danger estimé d'un combat, en simulant le combat sur des copies des armées.</summary>
    private string Prediction(Army enemy)
    {
        int armyCount = _state.CurrentHero?.Army.TotalCount ?? 0;
        var key = (enemy, enemy.TotalCount, armyCount);
        if (_predictionKey == key)
            return _predictionText;
        _predictionKey = key;

        var report = _state.PredictBattle(enemy);
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
    public void ShowBattle(BattleFought battle)
    {
        var report = battle.Report;
        string text = (report.AttackerWon ? "Victoire !" : "Défaite...")
            + $"\n\nVos pertes : {Texts.Losses(report.AttackerLosses)}"
            + $"\nPertes ennemies : {Texts.Losses(report.DefenderLosses)}";
        ShowDialog(Texts.BattleTitle(battle), text, "OK", null);
    }

    /// <summary>Fenêtre de fin de partie, avec un bouton pour recommencer.</summary>
    public void ShowGameOver(GameOutcome outcome, Action newGame)
    {
        ShowDialog(Texts.GameOverTitle(outcome), Texts.GameOverText(outcome), "Nouvelle partie", newGame);
    }

    /// <summary>True tant qu'une fenêtre est ouverte ou en attente (le jeu ignore alors les commandes de la carte).</summary>
    public bool IsDialogOpen => _dialogOpen || _pendingDialogs.Count > 0;

    private bool _dialogOpen;
    private readonly Queue<(string Title, string Text, string Button, Action? OnClose)> _pendingDialogs = new();

    /// <summary>Les fenêtres s'affichent l'une après l'autre (ex. : compte rendu du combat, puis fin de partie).</summary>
    private void ShowDialog(string title, string text, string button, Action? onClose)
    {
        _pendingDialogs.Enqueue((title, text, button, onClose));
        if (!_dialogOpen)
            OpenNextDialog();
    }

    private void OpenNextDialog()
    {
        if (!_pendingDialogs.TryDequeue(out var next))
            return;
        var dialog = new AcceptDialog
        {
            Title = next.Title,
            DialogText = next.Text,
            OkButtonText = next.Button,
            Exclusive = true,
            Unresizable = true,
        };
        _dialogOpen = true;
        bool closed = false;
        void Close()
        {
            if (closed)
                return;
            closed = true;
            _dialogOpen = false;
            dialog.QueueFree();
            next.OnClose?.Invoke();
            OpenNextDialog();
        }
        dialog.Confirmed += Close;
        dialog.Canceled += Close;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(360, 0));
    }

}
