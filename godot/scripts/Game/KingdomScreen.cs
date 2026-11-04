using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// Le royaume (étape 7, touche K) : les héros en portraits (mouvement restant dessous), les villes en images (revenu
/// dessous), le détail au survol ; en bas, une bande « mines et revenus » (comme Heroes 3).
/// Un clic choisit un héros (double clic : son écran) ou ouvre une ville ; un clic sur une ressource centre la carte
/// sur une de ses mines (la suivante à chaque clic).
/// </summary>
public partial class KingdomScreen : ScreenWindow
{
    private GameState _state = null!;
    private HBoxContainer _heroes = null!;
    private HBoxContainer _towns = null!;
    private Label _noTown = null!;
    private readonly Dictionary<ResourceType, (Label Counts, Label Income, Control Box)> _mines = new();
    private readonly Dictionary<ResourceType, int> _nextMine = new();
    private string? _key;

    public event Action<Hero>? HeroChosen;
    public event Action<Hero>? HeroDetailsWanted;
    public event Action<Town>? TownChosen;
    public event Action<Cell>? MineChosen;

    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        var size = new Vector2(1120, 640);
        BuildFrame(screenSize, new Rect2((screenSize - size) / 2 + new Vector2(0, Hud.TopBarHeight / 2f), size));
        SetTitle(Texts.KingdomTitle);

        _heroes = Section(56, 170, Texts.KingdomHeroes(0), out var heroesTitle);
        _heroesTitle = heroesTitle;
        _towns = Section(238, 190, Texts.KingdomTowns, out _);
        _noTown = At(MakeLabel(Texts.KingdomNoTown, 14, SoftTextColor), 32, 300);
        Window.AddChild(_noTown);

        // la bande des mines et revenus
        var strip = new Panel { Position = new Vector2(20, 440), Size = new Vector2(size.X - 40, 110) };
        strip.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, DimColor, 1, 1, 1, 1));
        strip.AddChild(At(MakeLabel(Texts.KingdomMines, 15, SoftTextColor), 12, 6));
        Window.AddChild(strip);
        float column = (strip.Size.X - 24) / ResourceInfo.All.Count();
        int i = 0;
        foreach (var resource in ResourceInfo.All)
        {
            var box = new Panel { Position = new Vector2(12 + i++ * column, 36), Size = new Vector2(column - 8, 62), MouseFilter = Control.MouseFilterEnum.Stop };
            box.AddThemeStyleboxOverride("panel", Box(PanelColor, DimColor, 1, 1, 1, 1));
            box.AddChild(new ResourceIcon { Resource = resource, Position = new Vector2(8, 8), Size = new Vector2(20, 20), MouseFilter = Control.MouseFilterEnum.Ignore });
            var name = At(MakeLabel(Texts.Capitalize(Texts.Resource(resource)), 15, TextColor), 34, 6);
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            box.AddChild(name);
            var counts = At(MakeLabel("", 13, SoftTextColor), 10, 34);
            counts.MouseFilter = Control.MouseFilterEnum.Ignore;
            box.AddChild(counts);
            var income = At(MakeLabel("", 15, TextColor), column - 140, 6);
            income.Size = new Vector2(120, 22);
            income.HorizontalAlignment = HorizontalAlignment.Right;
            income.MouseFilter = Control.MouseFilterEnum.Ignore;
            box.AddChild(income);
            var r = resource;
            box.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    CenterOnNextMine(r);
            };
            strip.AddChild(box);
            _mines[resource] = (counts, income, box);
        }
    }

    private Label _heroesTitle = null!;

    /// <summary>Une section (héros ou villes) : un titre et une rangée de vignettes.</summary>
    private HBoxContainer Section(float y, float height, string title, out Label titleLabel)
    {
        var section = new Panel { Position = new Vector2(20, y), Size = new Vector2(Window.Size.X - 40, height) };
        section.AddThemeStyleboxOverride("panel", Box(DarkPanelColor, DimColor, 1, 1, 1, 1));
        titleLabel = At(MakeLabel(title, 15, SoftTextColor), 12, 6);
        section.AddChild(titleLabel);
        Window.AddChild(section);
        var row = new HBoxContainer { Position = new Vector2(12, 32), Size = new Vector2(section.Size.X - 24, height - 40) };
        row.AddThemeConstantOverride("separation", 10);
        section.AddChild(row);
        return row;
    }

    public void Open()
    {
        _key = null;
        OpenWindow();
        Refresh();
    }

    /// <summary>Met à jour les vignettes (seulement quand quelque chose change) et la bande des mines.</summary>
    public void Refresh()
    {
        if (!IsOpen)
            return;
        var player = _state.CurrentPlayer;
        string key = string.Join("|", player.Heroes.Select(h => $"{h.Id}:{h.Movement}:{h.Level}:{h.Army.TotalCount}"))
            + "#" + string.Join("|", player.Towns.Select(t => $"{t.Id}:{t.BuiltToday}:{t.DailyGold}:{t.Garrison.TotalCount}:{t.Available.Values.Sum()}"))
            + $"#{_state.CurrentHero?.Id}";
        if (key != _key)
        {
            _key = key;
            RebuildHeroes(player);
            RebuildTowns(player);
        }
        var income = player.DailyIncome();
        foreach (var (resource, (counts, amount, box)) in _mines)
        {
            counts.Text = Texts.MineCounts(player, resource);
            amount.Text = Texts.PerDay(income.GetValueOrDefault(resource));
            box.TooltipText = Texts.MinesWhere(player, resource);
        }
    }

    private void RebuildHeroes(Player player)
    {
        _heroesTitle.Text = Texts.KingdomHeroes(player.Heroes.Count);
        Clear(_heroes);
        foreach (var hero in player.Heroes)
        {
            var tile = Tile(new Vector2(100, 128), hero == _state.CurrentHero, Texts.HeroDetails(hero),
                () => HeroChosen?.Invoke(hero), () => HeroDetailsWanted?.Invoke(hero));
            if (Art.Portrait(hero.Name) is { } portrait)
                tile.AddChild(Picture(portrait, new Vector2(18, 6), new Vector2(64, 64)));
            var name = At(MakeLabel(hero.Name, 13, TextColor), 2, 74);
            name.Size = new Vector2(96, 18);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.ClipText = true;
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            tile.AddChild(name);
            // le mouvement restant
            tile.AddChild(new ColorRect { Position = new Vector2(10, 100), Size = new Vector2(80, 8), Color = MapRenderer.Rgb(20, 16, 12), MouseFilter = Control.MouseFilterEnum.Ignore });
            tile.AddChild(new ColorRect
            {
                Position = new Vector2(10, 100), Size = new Vector2(80f * hero.Movement / Math.Max(1, hero.MaxMovement), 8),
                Color = MapRenderer.Rgb(60, 180, 60), MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            _heroes.AddChild(tile);
        }
    }

    private void RebuildTowns(Player player)
    {
        Clear(_towns);
        _noTown.Visible = player.Towns.Count == 0;
        foreach (var town in player.Towns)
        {
            var tile = Tile(new Vector2(120, 146), false, Texts.TownDetails(town), () => TownChosen?.Invoke(town), null);
            if (Art.Get("objects/town") is { } image)
                tile.AddChild(Picture(image, new Vector2(12, 6), new Vector2(96, 72)));
            var name = At(MakeLabel(town.Name, 13, TextColor), 2, 84);
            name.Size = new Vector2(116, 18);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.ClipText = true;
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            tile.AddChild(name);
            var income = At(MakeLabel(Texts.TownIncome(town) + (town.BuiltToday ? "" : "  ●"), 12, town.BuiltToday ? SoftTextColor : GoodColor), 2, 104);
            income.Size = new Vector2(116, 16);
            income.HorizontalAlignment = HorizontalAlignment.Center;
            income.MouseFilter = Control.MouseFilterEnum.Ignore;
            tile.AddChild(income);
            _towns.AddChild(tile);
        }
    }

    /// <summary>Une vignette cliquable, avec son détail au survol ; <paramref name="selected"/> : bordée de clair.</summary>
    private static Panel Tile(Vector2 size, bool selected, string tooltip, Action clicked, Action? doubleClicked)
    {
        var tile = new Panel { CustomMinimumSize = size, TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Stop };
        tile.AddThemeStyleboxOverride("panel", Box(PanelColor, selected ? Colors.White : DimColor, 1, 1, 1, 1));
        tile.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
                return;
            if (click.DoubleClick && doubleClicked != null)
                doubleClicked();
            else
                clicked();
        };
        return tile;
    }

    /// <summary>Centre la carte sur une mine de cette ressource (la suivante à chaque clic).</summary>
    private void CenterOnNextMine(ResourceType resource)
    {
        var mines = _state.CurrentPlayer.Mines.Where(m => m.Resource == resource).ToList();
        if (mines.Count == 0)
            return;
        int index = _nextMine.GetValueOrDefault(resource) % mines.Count;
        _nextMine[resource] = index + 1;
        MineChosen?.Invoke(mines[index].Position);
    }
}
