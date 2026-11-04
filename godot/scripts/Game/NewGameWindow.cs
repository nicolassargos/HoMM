using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>Ce que le joueur a choisi pour une nouvelle partie.</summary>
public sealed record GameSetup(int Seed, MapSize Size, GameRules Rules);

/// <summary>
/// Nouvelle partie (étape 7), depuis le menu principal : la taille de la carte (cinq choix), la graine (vide : au
/// hasard), les façons de gagner et la limite de jours.
/// </summary>
public partial class NewGameWindow : ScreenWindow
{
    public event Action<GameSetup>? Started;

    private MapSize _size = MapSize.Normal;
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
        var seedHelp = At(MakeLabel(Texts.SeedHelp, 12, SoftTextColor), 420, 205);
        Window.AddChild(seedHelp);

        // comment gagner
        Window.AddChild(At(MakeLabel(Texts.GoalsLabel, 16, BorderColor), 20, 252));
        var goals = new VBoxContainer { Position = V(20, 278) };
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
    }

    private void UpdateStart() => _start.Disabled = !_goals.Values.Any(b => b.ButtonPressed);

    private void Start()
    {
        if (_start.Disabled)
            return;
        int seed = int.TryParse(_seed.Text, out int typed) ? typed : Random.Shared.Next(1, 1_000_000);
        var victory = _goals.Where(g => g.Value.ButtonPressed).Aggregate(VictoryCondition.None, (all, g) => all | g.Key);
        var rules = new GameRules(victory, _limit.ButtonPressed ? GameRules.StandardDayLimit : null);
        Close();
        Started?.Invoke(new GameSetup(seed, _size, rules));
    }
}
