using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>Ce que le joueur a choisi pour une nouvelle partie.</summary>
public sealed record GameSetup(int Seed, MapSize Size, GameRules Rules, int Opponents = 0, string? HeroName = null, string? Faction = null,
    ZoneAccess Access = ZoneAccess.Normal, MapType MapType = MapType.Continent);

/// <summary>
/// Nouvelle partie, depuis le menu principal (arrangement en deux colonnes, choisi par l'utilisateur). À gauche,
/// **la partie** : listes déroulantes (taille de la carte, adversaires, difficulté), la graine, les façons de gagner et la
/// limite de jours. À droite, **votre héros** : la faction, le héros choisi en grand (portrait ×4, nom, classe et ce
/// qu'elle donne), et en bas une bande de petits portraits qui défile (◄ ►) : la liste des héros peut grandir.
/// </summary>
public partial class NewGameWindow : ScreenWindow
{
    public event Action<GameSetup>? Started;

    private const int PortraitScale = 2, VisibleHeroes = 5; // le portrait ×2, comme tout le pixel art
    private const float Thumb = 62, ThumbGap = 6;

    private MapSize _size = MapSize.Normal;
    private int _opponents = 1;
    private Difficulty _difficulty = Difficulty.Normal;
    private ZoneAccess _access = ZoneAccess.Normal;
    private MapType _mapType = MapType.Continent;
    private OptionButton _opponentList = null!, _difficultyList = null!;
    private LineEdit _seed = null!;
    private readonly Dictionary<VictoryCondition, CheckBox> _goals = new();
    private CheckBox _limit = null!;
    private Button _start = null!;

    // le héros (un nom et un portrait : sa classe vient de la faction)
    private GameData _data = null!;
    private int _hero, _firstShown;
    private string _faction = GameState.StartingFaction;
    private Label _className = null!, _classDetails = null!;
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
        Sized(_start, Px(220), LargeButtonHeight);
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
            new Vector2(Px(300), ButtonHeight), i => SelectSize(MapSizes.All.ElementAt(i)));
        sizes.Position = V(14, 54);
        game.AddChild(sizes);
        // les passages entre les zones, à côté de la taille de la carte
        game.AddChild(Small(Texts.ZoneAccessLabel, 330, 34));
        var access = MakeDropdown(Enum.GetValues<ZoneAccess>().Select(Texts.ZoneAccessName), (int)_access, new Vector2(Px(126), ButtonHeight),
            i => _access = (ZoneAccess)i);
        access.Position = V(330, 54);
        access.TooltipText = Texts.ZoneAccessHelp;
        game.AddChild(access);
        // développement (« --dropdown-open ») : la liste des tailles ouverte, pour une capture
        if (OS.GetCmdlineUserArgs().Contains("--dropdown-open"))
            Callable.From(sizes.ShowPopup).CallDeferred();

        game.AddChild(Small(Texts.OpponentsLabel, 14, 100));
        _opponentList = MakeDropdown(Enumerable.Range(0, 4).Select(Texts.OpponentsChoice), _opponents, new Vector2(Px(140), ButtonHeight), SelectOpponents);
        _opponentList.Position = V(14, 120);
        game.AddChild(_opponentList);

        game.AddChild(Small(Texts.DifficultyLabel, 170, 100));
        _difficultyList = MakeDropdown(Enum.GetValues<Difficulty>().Select(Texts.DifficultyName), (int)_difficulty, new Vector2(Px(160), ButtonHeight),
            i => _difficulty = (Difficulty)i);
        _difficultyList.Position = V(170, 120);
        game.AddChild(_difficultyList);

        game.AddChild(Small(Texts.SeedLabel, 14, 166));
        _seed = new LineEdit { Position = V(14, 186), Size = new Vector2(Px(170), ButtonHeight), PlaceholderText = Texts.SeedPlaceholder, MaxLength = 9 };
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
        var random = FixedButton(Texts.RandomSeed, 13, new Vector2(Px(120), ButtonHeight));
        random.Position = V(194, 186);
        random.Pressed += () => _seed.Text = Random.Shared.Next(1, 1_000_000).ToString();
        game.AddChild(random);
        // le type de la carte (la mer), à côté de la graine
        game.AddChild(Small(Texts.MapTypeLabel, 330, 166));
        var mapType = MakeDropdown(Enum.GetValues<MapType>().Select(Texts.MapTypeName), (int)_mapType, new Vector2(Px(126), ButtonHeight),
            i => _mapType = (MapType)i);
        mapType.Position = V(330, 186);
        mapType.TooltipText = Texts.MapTypeHelp;
        game.AddChild(mapType);

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
        // les factions qu'on peut jouer (celles qui ont une classe)
        var factions = _data.Classes.PlayableFactions.Count > 0 ? _data.Classes.PlayableFactions : new[] { GameState.StartingFaction };
        var faction = MakeDropdown(factions.Select(Texts.FactionName), 0, new Vector2(Px(220), ButtonHeight), i => SelectFaction(factions[i]));
        faction.Position = V(14, 54);
        hero.AddChild(faction);
        _faction = factions[0];

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
        // sa classe : celle de la faction choisie
        _className = At(MakeLabel("", 15, BorderColor), textX, 126);
        hero.AddChild(_className);
        // ce qu'elle donne, puis la note, l'une sous l'autre (la description change de longueur avec la faction)
        var column = new VBoxContainer { Position = V(textX, 150) };
        column.AddThemeConstantOverride("separation", (int)Px(10));
        hero.AddChild(column);
        _classDetails = MakeLabel("", 12, SoftTextColor);
        _classDetails.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _classDetails.CustomMinimumSize = V(440 - textX + 14, 0);
        column.AddChild(_classDetails);
        var note = MakeLabel(Texts.ClassNote, 12, DimColor);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.CustomMinimumSize = V(440 - textX + 14, 0);
        column.AddChild(note);
        SelectFaction(_faction);

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

    /// <summary>La faction choisie : sa classe et ce qu'elle donne (caractéristiques, sort, talent, armée).</summary>
    private void SelectFaction(string faction)
    {
        _faction = faction;
        var heroClass = _data.Classes.ForFaction(faction);
        _className.Text = heroClass?.Name ?? "";
        _classDetails.Text = heroClass != null ? Texts.ClassDetails(heroClass, _data) : "";
        // les héros de cette faction : la bande repart du premier
        _hero = _firstShown = 0;
        if (_heroCount != null)
            ShowHero();
    }

    /// <summary>Les héros qu'on peut choisir : ceux de la faction choisie (tous, si elle n'en a aucun).</summary>
    private IReadOnlyList<HeroType> Heroes =>
        _data.Heroes.OfFaction(_faction) is { Count: > 0 } heroes ? heroes : _data.Heroes.All;

    /// <summary>Choisit ce héros (en boucle) ; la bande défile pour qu'il reste visible.</summary>
    private void SelectHero(int index)
    {
        int count = Heroes.Count;
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
        var heroes = Heroes;
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
        string? hero = Heroes.Count > 0 ? Heroes[_hero].Name : null;
        Close();
        Started?.Invoke(new GameSetup(seed, _size, rules, _opponents, hero, _faction, _access, _mapType));
    }
}
