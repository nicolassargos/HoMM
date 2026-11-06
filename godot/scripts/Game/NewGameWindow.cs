using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>Ce que le joueur a choisi pour une nouvelle partie.</summary>
public sealed record GameSetup(int Seed, MapSize Size, GameRules Rules, int Opponents = 0);

/// <summary>
/// Nouvelle partie (étape 7), depuis le menu principal : la taille de la carte (cinq choix), le nombre d'adversaires
/// (étape 8, selon la taille), la graine (vide : au hasard), les façons de gagner et la limite de jours.
/// </summary>
public partial class NewGameWindow : ScreenWindow
{
    public event Action<GameSetup>? Started;

    private MapSize _size = MapSize.Normal;
    private int _opponents = 1;
    private Difficulty _difficulty = Difficulty.Normal;
    private readonly List<(Button Button, StyleBox Normal)> _difficultyButtons = new();
    private readonly List<(Button Button, StyleBox Normal)> _opponentButtons = new();
    private readonly Dictionary<MapSize, (Button Button, StyleBox Normal)> _sizeButtons = new();
    private Label _sizeDetails = null!;
    private LineEdit _seed = null!;
    private readonly Dictionary<VictoryCondition, CheckBox> _goals = new();
    private CheckBox _limit = null!;
    private Button _start = null!;

    public void Build(Vector2 screenSize)
    {
        var size = V(760, 560);
        BuildFrame(screenSize, new Rect2((screenSize - size) / 2, size), veilAll: true);
        Layer = 7;
        SetTitle(Texts.NewGameTitle);

        // la taille de la carte
        Window.AddChild(At(MakeLabel(Texts.MapSizeLabel, 16, BorderColor), 20, 60));
        float width = (size.X - Px(40) - 4 * Px(8)) / 5;
        foreach (var (mapSize, i) in MapSizes.All.Select((s, i) => (s, i)))
        {
            var button = FixedButton(Texts.MapSizeName(mapSize), 14, new Vector2(width, Px(40)));
            button.Position = new Vector2(Px(20) + i * (width + Px(8)), Px(88));
            var chosen = mapSize;
            button.Pressed += () => SelectSize(chosen);
            Window.AddChild(button);
            _sizeButtons[mapSize] = (button, button.GetThemeStylebox("normal"));
        }
        _sizeDetails = At(MakeLabel("", 13, SoftTextColor), 20, 134);
        Window.AddChild(_sizeDetails);

        // les adversaires : de 0 à 3, selon la taille de la carte
        Window.AddChild(At(MakeLabel(Texts.OpponentsLabel, 16, BorderColor), 440, 172));
        for (int count = 0; count <= 3; count++)
        {
            var button = FixedButton(count.ToString(), 14, V(52, 34));
            button.Position = V(440 + count * 60, 198);
            button.TooltipText = Texts.OpponentsTooltip(count);
            int chosen = count;
            button.Pressed += () => SelectOpponents(chosen);
            Window.AddChild(button);
            _opponentButtons.Add((button, button.GetThemeStylebox("normal")));
        }

        // la difficulté (la force des adversaires), sous leur nombre
        Window.AddChild(At(MakeLabel(Texts.DifficultyLabel, 16, BorderColor), 440, 240));
        foreach (var (level, i) in Enum.GetValues<Difficulty>().Select((d, i) => (d, i)))
        {
            var button = FixedButton(Texts.DifficultyName(level), 14, V(94, 34));
            button.Position = V(440 + i * 100, 266);
            button.TooltipText = Texts.DifficultyTooltip(level);
            var chosen = level;
            button.Pressed += () => SelectDifficulty(chosen);
            Window.AddChild(button);
            _difficultyButtons.Add((button, button.GetThemeStylebox("normal")));
        }

        // la graine
        Window.AddChild(At(MakeLabel(Texts.SeedLabel, 16, BorderColor), 20, 172));
        _seed = new LineEdit { Position = V(20, 198), Size = V(220, 34), PlaceholderText = Texts.SeedPlaceholder, MaxLength = 9 };
        SetFontSize(_seed, 15);
        _seed.TextChanged += text =>
        {
            // seulement des chiffres
            string digits = new(text.Where(char.IsDigit).ToArray());
            if (digits != text)
            {
                _seed.Text = digits;
                _seed.CaretColumn = digits.Length;
            }
        };
        Window.AddChild(_seed);
        var random = MakeButton(Texts.RandomSeed, 14);
        random.Position = V(252, 198);
        random.Size = V(150, 34);
        random.Pressed += () => _seed.Text = Random.Shared.Next(1, 1_000_000).ToString();
        Window.AddChild(random);
        var seedHelp = At(MakeLabel(Texts.SeedHelp, 12, SoftTextColor), 20, 236);
        Window.AddChild(seedHelp);

        // comment gagner
        Window.AddChild(At(MakeLabel(Texts.GoalsLabel, 16, BorderColor), 20, 266));
        var goals = new VBoxContainer { Position = V(20, 292) };
        goals.AddThemeConstantOverride("separation", (int)Px(4));
        foreach (var condition in GameRules.Choices)
        {
            var box = MakeCheckBox(Texts.Goal(condition), condition == GameRules.Default.Victory);
            box.Toggled += _ => UpdateStart();
            _goals[condition] = box;
            goals.AddChild(box);
        }
        _limit = MakeCheckBox(Texts.DayLimitChoice, false);
        goals.AddChild(_limit);
        var note = MakeLabel(Texts.GoalsNote, 12, SoftTextColor);
        goals.AddChild(note);
        Window.AddChild(goals);

        _start = MakeButton(Texts.StartButton, 18);
        _start.Position = new Vector2(size.X - Px(20 + 220), size.Y - Px(60));
        _start.Size = V(220, 44);
        _start.Pressed += Start;
        Window.AddChild(_start);
        SelectSize(_size);
        SelectDifficulty(_difficulty);
        SelectOpponents(_opponents);
    }

    public void Open() => OpenWindow();

    private void SelectSize(MapSize size)
    {
        var (oldButton, oldNormal) = _sizeButtons[_size];
        oldButton.AddThemeStyleboxOverride("normal", oldNormal);
        _size = size;
        MarkSelected(_sizeButtons[size].Button);
        var (w, h) = MapSizes.Dimensions(size);
        _sizeDetails.Text = Texts.MapSizeDetails(size, w, h);
        // plus d'adversaires que la carte n'en accueille : on en garde le plus possible
        if (_opponentButtons.Count > 0)
            SelectOpponents(Math.Min(_opponents, MapSizes.MaxOpponents(size)));
    }

    private void SelectDifficulty(Difficulty difficulty)
    {
        _difficulty = difficulty;
        foreach (var ((button, normal), i) in _difficultyButtons.Select((b, i) => (b, i)))
        {
            button.AddThemeStyleboxOverride("normal", normal);
            if (i == (int)difficulty)
                MarkSelected(button);
        }
    }

    private void SelectOpponents(int count)
    {
        _opponents = count;
        int max = MapSizes.MaxOpponents(_size);
        for (int i = 0; i < _opponentButtons.Count; i++)
        {
            var (button, normal) = _opponentButtons[i];
            button.AddThemeStyleboxOverride("normal", normal);
            button.Disabled = i > max;
            button.Modulate = new Color(1, 1, 1, i > max ? 0.35f : 1); // trop pour cette carte : estompé, pas « choisi »
            if (i == count)
                MarkSelected(button);
        }
        // sans adversaire, pas de difficulté à choisir
        foreach (var (button, _) in _difficultyButtons)
        {
            button.Disabled = count == 0;
            button.Modulate = new Color(1, 1, 1, count == 0 ? 0.35f : 1);
        }
        // « vaincre tous les adversaires » : seulement s'il y en a, et coché d'office
        if (_goals.TryGetValue(VictoryCondition.DefeatOpponents, out var defeat))
        {
            defeat.Disabled = count == 0;
            defeat.ButtonPressed = count > 0;
        }
        UpdateStart();
    }

    private void UpdateStart() => _start.Disabled = !_goals.Values.Any(b => b.ButtonPressed);

    private void Start()
    {
        if (_start.Disabled)
            return;
        int seed = int.TryParse(_seed.Text, out int typed) ? typed : Random.Shared.Next(1, 1_000_000);
        var victory = _goals.Where(g => g.Value.ButtonPressed).Aggregate(VictoryCondition.None, (all, g) => all | g.Key);
        var rules = new GameRules(victory, _limit.ButtonPressed ? GameRules.StandardDayLimit : null, _difficulty);
        Close();
        Started?.Invoke(new GameSetup(seed, _size, rules, _opponents));
    }
}
