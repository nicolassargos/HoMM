using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>Ce que le joueur a choisi pour une nouvelle partie.</summary>
public sealed record GameSetup(int Seed, MapSize Size, GameRules Rules, int Opponents = 0, string? HeroName = null);

/// <summary>
/// Nouvelle partie, depuis le menu principal (arrangement en deux colonnes, choisi par l'utilisateur). À gauche,
/// **la partie** : listes déroulantes (taille de la carte, adversaires, difficulté), la graine, les façons de gagner et la
/// limite de jours. À droite, **votre héros** : la faction, le héros choisi en grand (portrait ×4, nom, classe et ce
/// qu'elle donne), et en bas une bande de petits portraits qui défile (◄ ►) : la liste des héros peut grandir.
/// </summary>
public partial class NewGameWindow : ScreenWindow
{
    public event Action<GameSetup>? Started;

    private const int PortraitScale = 4, VisibleHeroes = 5;
    private const float Thumb = 62, ThumbGap = 6;

    private MapSize _size = MapSize.Normal;
    private int _opponents = 1;
    private Difficulty _difficulty = Difficulty.Normal;
    private OptionButton _opponentList = null!, _difficultyList = null!;
    private LineEdit _seed = null!;
    private readonly Dictionary<VictoryCondition, CheckBox> _goals = new();
    private CheckBox _limit = null!;
    private Button _start = null!;

    // le héros (un nom et un portrait : sa classe vient de la faction)
    private GameData _data = null!;
    private int _hero, _firstShown;
    private TextureRect _portrait = null!;
    private Label _initials = null!, _heroName = null!, _heroCount = null!;
    private readonly List<(Button Button, TextureRect Picture, Panel Frame)> _thumbs = new();

    public void Build(Vector2 screenSize)
    {
        _data = Main.LoadData();
        var size = V(1000, 620);
        BuildFrame(screenSize, new Rect2((screenSize - size) / 2, size), veilAll: true);
        Layer = 7;
        SetTitle(Texts.NewGameTitle);
        BuildGame();
        BuildHero();

        _start = MakeButton(Texts.StartButton, 18);
        _start.Position = new Vector2(size.X - Px(20 + 220), size.Y - Px(58));
        _start.Size = V(220, 44);
        _start.Pressed += Start;
        Window.AddChild(_start);
        SelectOpponents(_opponents);
        ShowHero();
    }

    public void Open() => OpenWindow();

    private Panel Section(float x, float y, float w, float h, string title)
    {
        var section = new Panel { Position = V(x, y), Size = V(w, h) };
        section.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        section.AddChild(At(MakeLabel(title, 16, BorderColor), 12, 6));
        Window.AddChild(section);
        return section;
    }

    private static Label Small(string text, float x, float y) => At(MakeLabel(text, 13, SoftTextColor), x, y);

    // ------------------------------------------------------------------ à gauche : la partie

    private void BuildGame()
    {
        var game = Section(20, 60, 470, 490, Texts.GameLabel);
        game.AddChild(Small(Texts.MapSizeLabel, 14, 34));
        var sizes = MakeDropdown(MapSizes.All.Select(s => Texts.MapSizeChoice(s, MapSizes.Dimensions(s))), MapSizes.All.ToList().IndexOf(_size),
            V(300, 34), i => SelectSize(MapSizes.All.ElementAt(i)));
        sizes.Position = V(14, 54);
        game.AddChild(sizes);
        // développement (« --dropdown-open ») : la liste des tailles ouverte, pour une capture
        if (OS.GetCmdlineUserArgs().Contains("--dropdown-open"))
            Callable.From(sizes.ShowPopup).CallDeferred();

        game.AddChild(Small(Texts.OpponentsLabel, 14, 100));
        _opponentList = MakeDropdown(Enumerable.Range(0, 4).Select(Texts.OpponentsChoice), _opponents, V(140, 34), SelectOpponents);
        _opponentList.Position = V(14, 120);
        game.AddChild(_opponentList);

        game.AddChild(Small(Texts.DifficultyLabel, 170, 100));
        _difficultyList = MakeDropdown(Enum.GetValues<Difficulty>().Select(Texts.DifficultyName), (int)_difficulty, V(160, 34),
            i => _difficulty = (Difficulty)i);
        _difficultyList.Position = V(170, 120);
        game.AddChild(_difficultyList);

        game.AddChild(Small(Texts.SeedLabel, 14, 166));
        _seed = new LineEdit { Position = V(14, 186), Size = V(170, 34), PlaceholderText = Texts.SeedPlaceholder, MaxLength = 9 };
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
        _seed.TooltipText = Texts.SeedHelp;
        game.AddChild(_seed);
        var random = FixedButton(Texts.RandomSeed, 13, V(120, 34));
        random.Position = V(194, 186);
        random.Pressed += () => _seed.Text = Random.Shared.Next(1, 1_000_000).ToString();
        game.AddChild(random);

        // comment gagner
        game.AddChild(Small(Texts.GoalsLabel, 14, 248));
        var goals = new VBoxContainer { Position = V(14, 270) };
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
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = V(440, 0);
        goals.AddChild(note);
        game.AddChild(goals);
    }

    private void SelectSize(MapSize size)
    {
        _size = size;
        // plus d'adversaires que la carte n'en accueille : on en garde le plus possible
        SelectOpponents(Math.Min(_opponents, MapSizes.MaxOpponents(size)));
    }

    private void SelectOpponents(int count)
    {
        _opponents = count;
        int max = MapSizes.MaxOpponents(_size);
        for (int i = 0; i < _opponentList.ItemCount; i++)
            _opponentList.SetItemDisabled(i, i > max); // trop pour cette carte
        _opponentList.Selected = count;
        // sans adversaire, pas de difficulté à choisir
        _difficultyList.Disabled = count == 0;
        // « vaincre tous les adversaires » : seulement s'il y en a, et coché d'office
        if (_goals.TryGetValue(VictoryCondition.DefeatOpponents, out var defeat))
        {
            defeat.Disabled = count == 0;
            defeat.ButtonPressed = count > 0;
        }
        UpdateStart();
    }

    // ------------------------------------------------------------------ à droite : votre héros

    private void BuildHero()
    {
        var hero = Section(510, 60, 470, 490, Texts.YourHeroLabel);
        hero.AddChild(Small(Texts.FactionLabel, 14, 34));
        var faction = MakeDropdown(new[] { Texts.FactionName(GameState.StartingFaction) }, 0, V(220, 34), _ => { });
        faction.Position = V(14, 54);
        hero.AddChild(faction);

        // le héros choisi, en grand : le portrait de 64 pixels ×4
        float side = 64 * PortraitScale;
        var frame = new Panel { Position = V(14, 100), Size = new Vector2(side + 4, side + 4) };
        frame.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, SilverColor, 1, 1, 1, 1));
        hero.AddChild(frame);
        _portrait = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(2, 2), Size = new Vector2(side, side),
        };
        frame.AddChild(_portrait);
        _initials = MakeLabel("", 30, SoftTextColor); // pas encore de portrait : ses initiales
        _initials.Size = frame.Size;
        _initials.HorizontalAlignment = HorizontalAlignment.Center;
        _initials.VerticalAlignment = VerticalAlignment.Center;
        frame.AddChild(_initials);
        float textX = 14 + (side + 4) / Ui.Scale + 14;
        _heroName = At(MakeLabel("", 18, TextColor), textX, 100);
        hero.AddChild(_heroName);
        if (_data.Classes.ForFaction(GameState.StartingFaction) is { } heroClass)
        {
            hero.AddChild(At(MakeLabel(heroClass.Name, 15, BorderColor), textX, 126));
            var details = At(MakeLabel(Texts.ClassSummary(heroClass, _data.Spells).Split(" : ", 2)[1], 12, SoftTextColor), textX, 150);
            details.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            details.CustomMinimumSize = V(440 - textX + 14, 0);
            hero.AddChild(details);
            var note = At(MakeLabel(Texts.ClassNote, 12, DimColor), textX, 220);
            note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            note.CustomMinimumSize = V(440 - textX + 14, 0);
            hero.AddChild(note);
        }

        // la bande des héros, en bas de la colonne : ◄ quelques petits portraits ► (sans creux)
        float stripWidth = 34 + 4 + VisibleHeroes * (Thumb + ThumbGap) - ThumbGap + 4 + 34;
        var strip = new ArrowSelector(V(stripWidth, Thumb + 8), sunken: false) { Position = V(14, 490 - Thumb - 8 - 34) };
        strip.Stepped += step => SelectHero(_hero + step);
        hero.AddChild(strip);
        for (int i = 0; i < VisibleHeroes; i++)
        {
            int slot = i;
            var button = FixedButton("", 12, V(Thumb, Thumb));
            button.Position = V(4 + i * (Thumb + ThumbGap), 4);
            button.Pressed += () => SelectHero(_firstShown + slot);
            var picture = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = V(4, 4), Size = V(Thumb - 8, Thumb - 8),
            };
            button.AddChild(picture);
            // le héros choisi : un cadre d'argent bien visible
            var chosen = new Panel { Size = button.Size, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            chosen.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0), SilverColor, 3, 3, 3, 3));
            button.AddChild(chosen);
            strip.Content.AddChild(button);
            _thumbs.Add((button, picture, chosen));
        }
        _heroCount = At(MakeLabel("", 12, DimColor), 14, 490 - 30);
        _heroCount.Size = V(stripWidth, 18);
        _heroCount.HorizontalAlignment = HorizontalAlignment.Center;
        hero.AddChild(_heroCount);
    }

    /// <summary>Choisit ce héros (en boucle) ; la bande défile pour qu'il reste visible.</summary>
    private void SelectHero(int index)
    {
        int count = _data.Heroes.All.Count;
        if (count == 0)
            return;
        _hero = (index % count + count) % count;
        if (_hero < _firstShown)
            _firstShown = _hero;
        else if (_hero >= _firstShown + VisibleHeroes)
            _firstShown = _hero - VisibleHeroes + 1;
        ShowHero();
    }

    private void ShowHero()
    {
        var heroes = _data.Heroes.All;
        if (heroes.Count == 0)
            return;
        var hero = heroes[_hero];
        _portrait.Texture = Art.Portrait(hero.Name);
        _initials.Text = _portrait.Texture == null ? Texts.Initials(hero.Name) : "";
        _heroName.Text = hero.Name;
        _heroCount.Text = $"{_hero + 1} / {heroes.Count}";
        for (int i = 0; i < _thumbs.Count; i++)
        {
            var (button, picture, chosen) = _thumbs[i];
            int index = _firstShown + i;
            button.Visible = index < heroes.Count;
            if (!button.Visible)
                continue;
            picture.Texture = Art.Portrait(heroes[index].Name);
            button.Text = picture.Texture == null ? Texts.Initials(heroes[index].Name) : "";
            button.TooltipText = heroes[index].Name;
            chosen.Visible = index == _hero;
        }
    }

    // ------------------------------------------------------------------ commencer

    private void UpdateStart()
    {
        if (_start != null)
            _start.Disabled = !_goals.Values.Any(b => b.ButtonPressed);
    }

    private void Start()
    {
        if (_start.Disabled)
            return;
        int seed = int.TryParse(_seed.Text, out int typed) ? typed : Random.Shared.Next(1, 1_000_000);
        var victory = _goals.Where(g => g.Value.ButtonPressed).Aggregate(VictoryCondition.None, (all, g) => all | g.Key);
        var rules = new GameRules(victory, _limit.ButtonPressed ? GameRules.StandardDayLimit : null, _difficulty);
        string? hero = _data.Heroes.All.Count > 0 ? _data.Heroes.All[_hero].Name : null;
        Close();
        Started?.Invoke(new GameSetup(seed, _size, rules, _opponents, hero));
    }
}
