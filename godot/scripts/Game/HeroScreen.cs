using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'écran du héros (étape 7), une grande fenêtre par-dessus la carte : portrait, niveau, expérience et mouvement ;
/// les caractéristiques en colonne, avec ce que fait chacune ; ses artefacts ; une place pour la magie (plus tard) ;
/// son armée en grand, avec le commandement de chaque troupe. S'ouvre d'un clic sur le portrait du panneau ou d'un
/// double clic dans la liste des héros.
/// </summary>
public partial class HeroScreen : ScreenWindow
{
    private const int PortraitScale = 2;

    private Hero? _hero;
    private TextureRect _portrait = null!;
    private Label _level = null!;
    private Label _class = null!;
    private Label _experienceText = null!;
    private ProgressBar _experienceBar = null!;
    private Label _movementText = null!;
    private ProgressBar _movementBar = null!;
    private readonly List<(HeroStat? Stat, Label Name, Label Value, Label Effect)> _stats = new();
    private VBoxContainer _artifacts = null!;
    private Label _skillPoints = null!;
    private VBoxContainer _skills = null!;
    private Button _skillTrees = null!;
    private string? _skillsKey;
    private Label _spellPoints = null!;
    private VBoxContainer _spells = null!;
    private string? _spellsKey;
    private ArmySlots _army = null!;

    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        var size = V(1000, 640);
        BuildFrame(screenSize, new Rect2((screenSize - size) / 2 + new Vector2(0, Hud.TopBarHeight / 2f), size));

        // le portrait, puis niveau, expérience et mouvement
        var portraitFrame = new Panel { Position = V(20, 56), Size = new Vector2(64, 64) * PortraitScale + V(4, 4) };
        portraitFrame.AddThemeStyleboxOverride("panel", Box(Colors.Black, SilverColor, 2, 2, 2, 2));
        Window.AddChild(portraitFrame);
        _portrait = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Position = V(2, 2), Size = new Vector2(64, 64) * PortraitScale, // en pixels : le portrait ×2
        };
        portraitFrame.AddChild(_portrait);
        // la classe sous le portrait, le niveau à côté
        _class = At(MakeLabel("", 15, SoftTextColor), 20, 152);
        _class.Size = V(136, 20);
        _class.HorizontalAlignment = HorizontalAlignment.Center;
        Window.AddChild(_class);
        _level = At(MakeLabel("", 16, TextColor), 166, 60);
        Window.AddChild(_level);
        // l'expérience et le mouvement : un mot, puis une barre avec les nombres dessus
        Window.AddChild(At(MakeLabel(Texts.ExperienceWord, 13, SoftTextColor), 166, 90));
        (_experienceBar, _experienceText) = Bar(V(166, 108), MapRenderer.Rgb(150, 120, 50));
        Window.AddChild(At(MakeLabel(Texts.MovementWord, 13, SoftTextColor), 166, 134));
        (_movementBar, _movementText) = Bar(V(166, 152), MovementColor);

        // les caractéristiques, en colonne
        float y = 204;
        foreach (HeroStat? stat in new HeroStat?[] { HeroStat.Attack, HeroStat.Defense, HeroStat.Power, HeroStat.Knowledge, null })
        {
            var name = At(MakeLabel("", 16, TextColor), 24, y);
            var value = At(MakeLabel("", 20, TextColor), 210, y - 2);
            value.Size = V(50, 24);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            var effect = At(MakeLabel("", 12, SoftTextColor), 24, y + 20);
            foreach (var label in new[] { name, value, effect })
            {
                label.MouseFilter = Control.MouseFilterEnum.Stop; // pour l'info-bulle
                Window.AddChild(label);
            }
            _stats.Add((stat, name, value, effect));
            y += 46;
        }

        // les compétences : les points à dépenser, celles du héros, et le bouton des arbres
        var skills = Section(V(290, 56), V(330, 250), Texts.SkillsHeader);
        _skillPoints = At(MakeLabel("", 13, TextColor), 12, 32);
        skills.AddChild(_skillPoints);
        _skills = new VBoxContainer { Position = V(12, 56), Size = V(306, 140) };
        _skills.AddThemeConstantOverride("separation", (int)Px(2));
        skills.AddChild(_skills);
        _skillTrees = FixedButton(Texts.SkillTreesButton, 14, V(306, 34));
        _skillTrees.Position = V(12, 206);
        _skillTrees.Pressed += OpenSkillTrees;
        skills.AddChild(_skillTrees);

        // les artefacts
        var artifacts = Section(V(290, 316), V(330, 120), Texts.ArtifactsHeader);
        _artifacts = new VBoxContainer { Position = V(12, 34), Size = V(306, 80) };
        _artifacts.AddThemeConstantOverride("separation", (int)Px(8));
        artifacts.AddChild(_artifacts);

        // la magie : les points de sort et le livre (les compétences viendront avec les classes de héros)
        var magic = Section(V(636, 56), V(344, 380), Texts.MagicHeader);
        _spellPoints = At(MakeLabel("", 15, TextColor), 12, 32);
        _spellPoints.MouseFilter = Control.MouseFilterEnum.Stop;
        _spellPoints.TooltipText = Texts.SpellPointsDaily;
        magic.AddChild(_spellPoints);
        var scroll = new ScrollContainer { Position = V(12, 60), Size = V(326, 312), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        magic.AddChild(scroll);
        _spells = new VBoxContainer();
        _spells.AddThemeConstantOverride("separation", (int)Px(6));
        scroll.AddChild(_spells);

        // l'armée, en grand
        var army = Section(V(20, 450), V(960, 170), Texts.ArmyHeader);
        _army = new ArmySlots
        {
            Position = V(12, 32), SlotSize = V(132, 128), CountFontSize = 18,
            ShowCommandPoints = true, DragGroup = this,
        };
        // glisser pour réordonner ou fusionner ; Maj en lâchant, ou clic droit, pour partager
        _army.TroopDropped += (_, from, to, split) =>
        {
            if (split)
                Split(from, to);
            else if (_hero != null)
                _state?.MoveTroop(_hero, from, to);
        };
        _army.SlotRightClicked += index => Split(index, null);
        army.AddChild(_army);
    }

    private (ProgressBar Bar, Label Text) Bar(Vector2 position, Color fill)
    {
        var bar = new ProgressBar { Position = position, Size = V(112, 20), ShowPercentage = false, MinValue = 0 };
        StyleBar(bar, fill);
        bar.Size = V(112, 20); // après le style (sinon la hauteur minimale du thème l'emporte)
        Window.AddChild(bar);
        var text = MakeLabel("", 12, TextColor);
        text.HorizontalAlignment = HorizontalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.Size = bar.Size;
        bar.AddChild(text);
        return (bar, text);
    }

    private Panel Section(Vector2 position, Vector2 size, string title)
    {
        var section = new Panel { Position = position, Size = size };
        section.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        section.AddChild(At(MakeLabel(title, 15, SoftTextColor), 12, 6));
        Window.AddChild(section);
        return section;
    }

    /// <summary>Partage une troupe du héros : combien en mettre à part, vers l'emplacement visé ou le premier libre.</summary>
    private void Split(int from, int? to)
    {
        if (_hero is not { } hero || _state == null || from >= hero.Army.Stacks.Count)
            return;
        var stack = hero.Army.Stacks[from];
        if (stack.Count < 2)
            return;
        int slot = to ?? Math.Min(hero.Army.Stacks.Count, Army.MaxStacks - 1);
        AskCount(this, Texts.SplitTitle(stack.Type), n => Texts.SplitText(stack.Type, n, stack.Count), stack.Count - 1,
            n => _state.MoveTroop(hero, from, slot, n));
    }

    private GameState? _state;

    /// <summary>Ouvre l'écran pour ce héros.</summary>
    public void Open(Hero hero)
    {
        _hero = hero;
        OpenWindow();
        Refresh();
    }

    /// <summary>Met à jour l'écran (le héros peut changer pendant qu'il est ouvert : un niveau, une troupe…).</summary>
    public void Refresh()
    {
        if (_hero is not { } hero || !IsOpen)
            return;
        SetTitle(hero.Name);
        _portrait.Texture = Art.Portrait(hero.Name);
        _level.Text = Texts.LevelLine(hero);
        _class.Text = hero.Class?.Name ?? "";
        _experienceText.Text = $"{hero.Experience} / {Hero.ExperienceForLevel(hero.Level + 1)}";
        int floor = Hero.ExperienceForLevel(hero.Level), next = Hero.ExperienceForLevel(hero.Level + 1);
        _experienceBar.MaxValue = next - floor;
        _experienceBar.Value = hero.Experience - floor;
        _movementText.Text = $"{hero.Movement} / {hero.MaxMovement}";
        _movementBar.MaxValue = hero.MaxMovement;
        _movementBar.Value = hero.Movement;
        foreach (var (stat, name, value, effect) in _stats)
        {
            name.Text = Texts.StatFullName(stat);
            value.Text = (stat switch
            {
                HeroStat.Attack => hero.Attack,
                HeroStat.Defense => hero.Defense,
                HeroStat.Power => hero.Power,
                HeroStat.Knowledge => hero.Knowledge,
                _ => hero.Charisma,
            }).ToString();
            effect.Text = Texts.StatEffect(stat, hero);
            name.TooltipText = value.TooltipText = effect.TooltipText = Texts.StatTooltip(stat, hero);
        }
        RefreshSkills(hero);
        RefreshArtifacts(hero);
        RefreshSpells(hero);
        _army.Show(hero.Army, hero.CommandLimit);
    }

    /// <summary>Les points de sort, et chaque sort du livre : son nom, son niveau, ses points, ce qu'il fait.</summary>
    private void RefreshSpells(Hero hero)
    {
        _spellPoints.Text = Texts.SpellPointsLine(hero.SpellPoints, hero.MaxSpellPoints);
        string key = $"{hero.Id}:{hero.Power}:{string.Join(",", hero.Spells.Select(s => s.Id))}";
        if (key == _spellsKey)
            return;
        _spellsKey = key;
        foreach (var child in _spells.GetChildren())
        {
            _spells.RemoveChild(child);
            child.QueueFree();
        }
        if (hero.Spells.Count == 0)
        {
            var none = MakeLabel(Texts.NoSpellsKnown, 13, SoftTextColor);
            none.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            none.CustomMinimumSize = V(310, 0);
            _spells.AddChild(none);
        }
        foreach (var spell in hero.Spells)
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 0);
            box.AddChild(MakeLabel(Texts.SpellLine(spell, hero), 14, TextColor));
            var effect = MakeLabel(Texts.SpellEffectText(spell, hero), 12, SoftTextColor);
            effect.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            effect.CustomMinimumSize = V(310, 0);
            box.AddChild(effect);
            _spells.AddChild(box);
        }
    }

    /// <summary>Les points de compétence à dépenser, puis chaque compétence du héros et ce qu'elle fait.</summary>
    private void RefreshSkills(Hero hero)
    {
        _skillPoints.Text = Texts.SkillPointsLine(hero.SkillPoints, hero.Skills.Count);
        _skillTrees.Disabled = _state == null || hero.Owner != _state.CurrentPlayer || hero.Owner.IsComputer;
        string key = $"{hero.Id}:{string.Join(",", hero.Skills.Select(p => $"{p.Key.Id}{p.Value}"))}";
        if (key == _skillsKey)
            return;
        _skillsKey = key;
        foreach (var child in _skills.GetChildren())
        {
            _skills.RemoveChild(child);
            child.QueueFree();
        }
        if (hero.Skills.Count == 0)
            _skills.AddChild(MakeLabel(Texts.NoSkills, 12, SoftTextColor));
        foreach (var (skill, rank) in hero.Skills)
        {
            var line = MakeLabel(Texts.SkillButton(skill, rank), 13, TextColor);
            line.TooltipText = Texts.HeroSkillLine(skill, rank);
            line.MouseFilter = Control.MouseFilterEnum.Stop;
            _skills.AddChild(line);
        }
    }

    /// <summary>Les arbres de compétences du héros (un clic y dépense un point).</summary>
    public void OpenSkillTrees()
    {
        if (_hero is not { } hero || _state == null)
            return;
        var window = new SkillsWindow(_state, hero, Refresh);
        AddChild(window);
        window.Open();
    }

    private string? _artifactsKey;

    /// <summary>Chaque artefact : son image (×2) et ce qu'il apporte. (Une silhouette avec des emplacements viendra
    /// quand il y aura plus d'artefacts.)</summary>
    private void RefreshArtifacts(Hero hero)
    {
        string key = $"{hero.Id}:{string.Join(",", hero.Artifacts.Select(a => a.Id))}";
        if (key == _artifactsKey)
            return;
        _artifactsKey = key;
        foreach (var child in _artifacts.GetChildren())
        {
            _artifacts.RemoveChild(child);
            child.QueueFree();
        }
        if (hero.Artifacts.Count == 0)
            _artifacts.AddChild(MakeLabel(Texts.NoArtifact, 13, SoftTextColor));
        foreach (var artifact in hero.Artifacts)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", (int)Px(10));
            if (Art.Get($"artifacts/{artifact.Id}") is { } image)
                row.AddChild(new TextureRect
                {
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    Texture = image, CustomMinimumSize = new Vector2(96, 96), // l'artefact de 32 pixels, ×3
                });
            var text = MakeLabel(Texts.ArtifactTooltip(artifact), 14, TextColor);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            text.CustomMinimumSize = V(220, 0);
            text.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(text);
            _artifacts.AddChild(row);
        }
    }
}
