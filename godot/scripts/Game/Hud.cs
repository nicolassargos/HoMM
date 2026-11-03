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
    public const int TopBarHeight = 44;

    /// <summary>Hauteur du journal, en bas de la carte (quatre ou cinq lignes).</summary>
    private const int LogHeight = 92;

    private readonly Dictionary<ResourceType, Label> _resourceLabels = new();
    private GameState _state = null!;
    private Label _date = null!;
    private Label _movementText = null!;
    private ProgressBar _movementBar = null!;
    private Label _heroName = null!;
    private Label _army = null!;
    private Label _stats = null!;
    private Label _mines = null!;
    private HFlowContainer _towns = null!;
    private HFlowContainer _heroes = null!;
    private string? _heroesKey;
    private string? _townsKey;

    // Prévision de combat mise en cache : la simulation n'est refaite que si quelque chose change
    private (Army Enemy, int EnemyCount, int ArmyCount)? _predictionKey;
    private string _predictionText = "";
    private Label _hover = null!;
    private Label _log = null!;
    private readonly List<string> _messages = new();
    private const int MaxMessages = 6;

    public Minimap Minimap { get; private set; } = null!;
    public event Action? EndTurnPressed;
    public event Action? SavePressed;
    public event Action? LoadPressed;

    /// <summary>Le joueur a cliqué sur une de ses villes dans la liste.</summary>
    public event Action<Town>? TownClicked;

    /// <summary>Le joueur a cliqué sur un de ses héros dans la liste.</summary>
    public event Action<Hero>? HeroClicked;

    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        float panelX = screenSize.X - PanelWidth;

        // ---- Barre des ressources
        var topBar = new Panel { Position = Vector2.Zero, Size = new Vector2(panelX, TopBarHeight) };
        topBar.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        AddChild(topBar);

        var resources = new HBoxContainer { Position = new Vector2(16, 0), Size = new Vector2(600, TopBarHeight) };
        resources.AddThemeConstantOverride("separation", 6);
        topBar.AddChild(resources);
        foreach (var resource in ResourceInfo.All)
        {
            resources.AddChild(new ResourceIcon { Resource = resource, CustomMinimumSize = new Vector2(18, TopBarHeight) });
            var label = MakeLabel("", 16, TextColor);
            label.CustomMinimumSize = new Vector2(100, 0);
            label.VerticalAlignment = VerticalAlignment.Center;
            resources.AddChild(label);
            _resourceLabels[resource] = label;
        }

        _date = MakeLabel("", 16, TextColor);
        _date.HorizontalAlignment = HorizontalAlignment.Right;
        _date.VerticalAlignment = VerticalAlignment.Center;
        _date.Position = new Vector2(panelX - 222, 0);
        _date.Size = new Vector2(200, TopBarHeight);
        topBar.AddChild(_date);

        // ---- Panneau de droite
        var panel = new Panel { Position = new Vector2(panelX, 0), Size = new Vector2(PanelWidth, screenSize.Y) };
        panel.AddThemeStyleboxOverride("panel", Frame(PanelColor));
        AddChild(panel);

        const int margin = 16;
        float innerWidth = PanelWidth - 2 * margin;

        int scale = Math.Min((PanelWidth - 16) / state.Map.Width, 6);
        var minimapSize = new Vector2(state.Map.Width, state.Map.Height) * scale;
        var minimapFrame = new Panel
        {
            Position = new Vector2((PanelWidth - minimapSize.X) / 2 - 2, 16),
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

        _stats = At(MakeLabel("", 12, SoftTextColor), margin, y);
        _stats.Size = new Vector2(innerWidth, 82);
        panel.AddChild(_stats);
        y += 86;

        panel.AddChild(At(MakeLabel("Armée", 14, SoftTextColor), margin, y));
        y += 20;
        _army = At(MakeLabel("", 13, TextColor), margin + 8, y);
        _army.Size = new Vector2(innerWidth - 8, 118);
        panel.AddChild(_army);
        y += 122;

        // La liste des héros du joueur : un clic sélectionne le héros (celui en cours est mis en valeur)
        _heroes = new HFlowContainer { Position = new Vector2(margin, y), Size = new Vector2(innerWidth, 28) };
        _heroes.AddThemeConstantOverride("h_separation", 6);
        _heroes.AddThemeConstantOverride("v_separation", 4);
        panel.AddChild(_heroes);
        y += 34;

        // La liste des villes du joueur : un bouton par ville ouvre son écran
        _towns = new HFlowContainer { Position = new Vector2(margin, y), Size = new Vector2(innerWidth, 28) };
        _towns.AddThemeConstantOverride("h_separation", 6);
        _towns.AddThemeConstantOverride("v_separation", 4);
        panel.AddChild(_towns);
        y += 36;

        _mines = At(MakeLabel("", 13, SoftTextColor), margin, y);
        _mines.Size = new Vector2(innerWidth, 36);
        _mines.ClipText = true;
        panel.AddChild(_mines);
        y += 40;

        _hover = At(MakeLabel("", 13, SoftTextColor), margin, y);
        _hover.Size = new Vector2(innerWidth, 48);
        _hover.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(_hover);
        y += 52;

        // ---- Options (sauvegarder, charger) et fin de tour, en bas du panneau
        const float optionsWidth = 78;
        var options = MakeButton("Options", 13);
        options.Position = new Vector2(margin, screenSize.Y - 60);
        options.Size = new Vector2(optionsWidth, 42);
        options.Pressed += ShowOptions;
        panel.AddChild(options);

        var endTurn = MakeButton("Fin du tour", 18);
        endTurn.Position = new Vector2(margin + optionsWidth + 6, screenSize.Y - 60);
        endTurn.Size = new Vector2(innerWidth - optionsWidth - 6, 42);
        endTurn.Pressed += () => EndTurnPressed?.Invoke();
        panel.AddChild(endTurn);

        // ---- Journal : en bas de la carte, sur un fond transparent (les clics passent à travers)
        var logBox = new Panel
        {
            Position = new Vector2(8, screenSize.Y - 8 - LogHeight),
            Size = new Vector2(Math.Min(620, panelX - 16), LogHeight),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        logBox.AddThemeStyleboxOverride("panel", Frame(new Color(PanelColor, 0.75f)));
        AddChild(logBox);
        _log = At(MakeLabel("", 13, TextColor), 16, 12);
        _log.Size = logBox.Size - new Vector2(32, 22);
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _log.VerticalAlignment = VerticalAlignment.Bottom;
        _log.ClipText = true;
        _log.MouseFilter = Control.MouseFilterEnum.Ignore;
        logBox.AddChild(_log);

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
            _stats.Text = Texts.HeroStats(hero);
            _army.Text = string.Join("\n", hero.Army.Stacks.Select(s => Texts.ArmyLine(s, hero.Charisma)));
        }
        else
        {
            _heroName.Text = "Aucun héros";
            _movementBar.Value = 0;
            _movementText.Text = "";
            _stats.Text = "";
            _army.Text = "";
        }

        RefreshHeroes(player);
        RefreshTowns(player);
        var income = player.DailyIncome();
        _mines.Text = $"Mines : {player.Mines.Count}\n"
            + (income.Count == 0 ? "Revenus : aucun" : $"Revenus : {Texts.Income(income)} /jour");

        _hover.Text = DescribeCell(hovered);

        // Les derniers messages, du plus ancien au plus récent
        string log = string.Join("\n", _messages.TakeLast(4));
        if (_log.Text != log)
            _log.Text = log;
    }

    /// <summary>Reconstruit les boutons des héros, seulement quand la liste ou la sélection change.</summary>
    private void RefreshHeroes(Player player)
    {
        string key = string.Join("|", player.Heroes.Select(h => h.Name)) + "#" + _state.CurrentHero?.Name;
        if (key == _heroesKey)
            return;
        _heroesKey = key;

        foreach (var child in _heroes.GetChildren())
        {
            _heroes.RemoveChild(child);
            child.QueueFree();
        }
        _heroes.AddChild(MakeLabel("Héros :", 13, SoftTextColor));
        foreach (var hero in player.Heroes)
        {
            var button = MakeButton(hero.Name, 13);
            button.CustomMinimumSize = new Vector2(0, 26);
            if (hero == _state.CurrentHero) // le héros sélectionné : bordure claire
                MarkSelected(button);
            button.Pressed += () => HeroClicked?.Invoke(hero);
            _heroes.AddChild(button);
        }
    }

    /// <summary>Reconstruit les boutons des villes, seulement quand la liste change.</summary>
    private void RefreshTowns(Player player)
    {
        string key = string.Join("|", player.Towns.Select(t => t.Name));
        if (key == _townsKey)
            return;
        _townsKey = key;

        foreach (var child in _towns.GetChildren())
        {
            _towns.RemoveChild(child);
            child.QueueFree();
        }
        _towns.AddChild(MakeLabel("Villes :", 13, SoftTextColor));
        if (player.Towns.Count == 0)
            _towns.AddChild(MakeLabel("aucune", 13, SoftTextColor));
        foreach (var town in player.Towns)
        {
            var button = MakeButton(town.Name, 13);
            button.CustomMinimumSize = new Vector2(0, 26);
            button.Pressed += () => TownClicked?.Invoke(town);
            _towns.AddChild(button);
        }
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
    /// <summary>Fenêtre de niveau : ce qui a changé sur le héros.</summary>
    public void ShowLevelUp(HeroLeveledUp levelUp) =>
        ShowDialog(Texts.LevelUpTitle(levelUp), Texts.LevelUpSummary(levelUp), "OK", null);

    public void ShowGameOver(GameEnded end, Action newGame)
    {
        ShowDialog(Texts.GameOverTitle(end.Outcome), Texts.GameOverText(end), "Nouvelle partie", newGame);
    }

    /// <summary>La fenêtre des options : sauvegarder ou charger la partie (aussi avec F5 et F9).</summary>
    public void ShowOptions() => Enqueue(done =>
    {
        var dialog = new AcceptDialog
        {
            Title = Texts.OptionsTitle,
            DialogText = Texts.OptionsText,
            OkButtonText = Texts.CloseButton,
            Exclusive = true,
            Unresizable = true,
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
        // chaque bouton ajouté se place à gauche des précédents
        dialog.AddButton(Texts.LoadButton, false, "load");
        dialog.AddButton(Texts.SaveButton, false, "save");
        dialog.CustomAction += action => Close(action.ToString() == "save" ? SavePressed : LoadPressed);
        dialog.Confirmed += () => Close(null);
        dialog.Canceled += () => Close(null);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(420, 0));
    });

    /// <summary>Choix des conditions de la partie, au lancement : un bouton par choix. Fermer la fenêtre prend le premier.</summary>
    public void ShowRulesChoice(IReadOnlyList<GameRules> options, Action<GameRules> choose) => Enqueue(done =>
    {
        var dialog = new AcceptDialog
        {
            Title = Texts.RulesTitle,
            DialogText = Texts.RulesText,
            Exclusive = true,
            Unresizable = true,
        };
        dialog.GetOkButton().Hide();
        bool chosen = false;
        void Choose(GameRules rules)
        {
            if (chosen)
                return;
            chosen = true;
            choose(rules);
            dialog.QueueFree();
            done();
        }
        for (int i = 0; i < options.Count; i++)
            dialog.AddButton(Texts.Objective(options[i]), false, i.ToString());
        dialog.CustomAction += action => Choose(options[int.Parse(action.ToString())]);
        dialog.Canceled += () => Choose(options[0]);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(560, 0));
    });

    /// <summary>True tant qu'une fenêtre est ouverte ou en attente (le jeu ignore alors les commandes de la carte).</summary>
    public bool IsDialogOpen => _dialogOpen || _pendingDialogs.Count > 0;

    private bool _dialogOpen;

    /// <summary>Chaque fenêtre en attente : une action qui l'ouvre et reçoit l'action à appeler quand elle est fermée.</summary>
    private readonly Queue<Action<Action>> _pendingDialogs = new();

    /// <summary>Les fenêtres s'affichent l'une après l'autre (ex. : compte rendu du combat, puis fin de partie).</summary>
    private void ShowDialog(string title, string text, string button, Action? onClose) => Enqueue(done =>
    {
        var dialog = new AcceptDialog
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
        dialog.PopupCentered(new Vector2I(360, 0));
    });

    /// <summary>Choix de la caractéristique d'un niveau : un bouton par caractéristique proposée. Fermer la fenêtre prend la première.</summary>
    public void ShowLevelChoice(HeroLevelChoice choice, Action<HeroStat> choose) => Enqueue(done =>
    {
        var dialog = new AcceptDialog
        {
            Title = Texts.LevelChoiceTitle(choice),
            DialogText = Texts.LevelChoiceText(choice),
            Exclusive = true,
            Unresizable = true,
        };
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
        foreach (var stat in choice.Options)
            dialog.AddButton(Texts.StatChoice(choice.Hero, stat), false, stat.ToString());
        dialog.CustomAction += action => Choose(Enum.Parse<HeroStat>(action.ToString()));
        dialog.Canceled += () => Choose(choice.Options[0]);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(560, 0));
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
