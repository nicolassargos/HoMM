using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HoMM.Core;
using static HoMM.Game.Ui;

namespace HoMM.Game;

/// <summary>
/// L'écran du héros (refait d'après la proposition B choisie par l'utilisateur, 2026-11-09), une grande fenêtre
/// par-dessus la carte. À gauche, toujours visibles : le portrait, le niveau, l'expérience, le mouvement, les
/// caractéristiques, le moral et la chance. À droite, trois onglets : <b>Équipement</b> (le héros en pied, ses 11
/// emplacements reliés par un trait à l'endroit où ils se portent, et un coffre de 16 places), <b>Compétences</b> (un
/// sous-onglet par arbre ; une icône par compétence, un clic y dépense un point) et <b>Magie</b> (les points de sort
/// et les sorts du livre). En bas, l'armée. S'ouvre d'un clic sur le portrait du panneau ou d'un double clic dans la
/// liste des héros ; « --hero-tab=skills » ou « magic » ouvre cet onglet (captures).
/// </summary>
public partial class HeroScreen : ScreenWindow
{
    private const int PortraitScale = 2;


    private Hero? _hero;
    private GameState? _state;
    private TextureRect _portrait = null!;
    private Label _level = null!;
    private Label _class = null!;
    private Label _experienceText = null!;
    private ProgressBar _experienceBar = null!;
    private Label _movementText = null!;
    private ProgressBar _movementBar = null!;
    private readonly List<(HeroStat? Stat, Label Name, Label Value, Label Effect)> _stats = new();
    private Label _morale = null!;
    private Label _luck = null!;
    private ArmySlots _army = null!;

    // les onglets (des marque-pages : TabStrip) : Équipement, Compétences, Magie
    private TabStrip _tabs = null!;
    private const int SkillsTabIndex = 1;

    // Équipement
    private HeroFigure _figure = null!;
    private readonly List<Panel> _slots = new();
    private readonly List<Panel> _chest = new();
    private string? _equipmentKey;

    // Compétences
    private SkillTree _tree = SkillTree.Aventure;
    private Label _skillPoints = null!;
    private Control _treeContent = null!;
    private string? _skillsKey;

    // Magie
    private Label _spellPoints = null!;
    private GridContainer _spells = null!;
    private string? _spellsKey;

    /// <summary>La place à droite, sous les onglets (en unités de l'interface).</summary>
    private static readonly Vector2 PagePosition = new(290, 56), PageSize = new(690, 414);

    public void Build(GameState state, Vector2 screenSize)
    {
        _state = state;
        var size = V(1000, 640);
        BuildFrame(screenSize, new Rect2((screenSize - size) / 2 + new Vector2(0, Hud.TopBarHeight / 2f), size));
        BuildLeftColumn();

        // les trois onglets : des marque-pages qui sortent du haut de la page (ajoutés avant les pages, qui en cachent le bas)
        _tabs = new TabStrip(14);
        _tabs.Position = V(PagePosition.X + 10, PagePosition.Y - _tabs.PageTop);
        Window.AddChild(_tabs);
        foreach (var (title, build) in new (string, Action<Control>)[]
                 { (Texts.EquipmentTab, BuildEquipment), (Texts.SkillsTab, BuildSkills), (Texts.MagicTab, BuildMagic) })
        {
            var page = new Panel { Position = V(PagePosition.X, PagePosition.Y), Size = V(PageSize.X, PageSize.Y) };
            page.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
            Window.AddChild(page);
            build(page);
            _tabs.AddTab(title, page);
        }
        _tabs.Select(StartTab(), notify: false);

        // l'armée, en bas
        var army = new Panel { Position = V(20, 480), Size = V(960, 146) };
        army.AddThemeStyleboxOverride("panel", Inset(DarkPanelColor));
        army.AddChild(At(MakeLabel(Texts.ArmyHeader, 15, SoftTextColor), 12, 4));
        Window.AddChild(army);
        _army = new ArmySlots
        {
            Position = V(12, 26), SlotSize = V(132, 114), CountFontSize = 18,
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

    /// <summary>L'onglet du lancement (« --hero-tab=skills » ou « magic », pour les captures), sinon l'équipement.</summary>
    private static int StartTab()
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--hero-tab="))?["--hero-tab=".Length..];
        return arg switch { "skills" => SkillsTabIndex, "magic" => 2, _ => 0 };
    }

    // ------------------------------------------------------------------ à gauche

    private void BuildLeftColumn()
    {
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
        _class.MouseFilter = Control.MouseFilterEnum.Stop; // pour l'info-bulle du talent
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
        float y = 196;
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
        // le moral et la chance de l'armée (le détail au survol)
        _morale = At(MakeLabel("", 16, TextColor), 24, y + 4);
        _luck = At(MakeLabel("", 16, TextColor), 150, y + 4);
        foreach (var label in new[] { _morale, _luck })
        {
            label.MouseFilter = Control.MouseFilterEnum.Stop;
            Window.AddChild(label);
        }
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

    // ------------------------------------------------------------------ Équipement

    private void BuildEquipment(Control page)
    {
        _figure = new HeroFigure { Position = V(10, 5), Size = V(HeroFigure.FigureSize.X, HeroFigure.FigureSize.Y), MouseFilter = Control.MouseFilterEnum.Ignore };
        page.AddChild(_figure);
        for (int i = 0; i < Equipment.Slots.Length; i++)
        {
            var slot = Slot(HeroFigure.SlotPosition(i), Texts.SlotName(Equipment.Slots[i]));
            _figure.AddChild(slot);
            _slots.Add(slot);
        }
        // le coffre : 4 × 4 places de la même taille
        const float chestX = 412, chestY = 34, step = HeroFigure.SlotSize + 4;
        var header = At(MakeLabel(Texts.ChestHeader, 15, SoftTextColor), chestX, 8);
        header.TooltipText = Texts.ChestTooltip;
        header.MouseFilter = Control.MouseFilterEnum.Stop;
        page.AddChild(header);
        for (int i = 0; i < Equipment.ChestSize; i++)
        {
            var place = Slot(new Vector2(chestX + i % 4 * step, chestY + i / 4 * step), null);
            place.TooltipText = Texts.ChestTooltip;
            page.AddChild(place);
            _chest.Add(place);
        }
    }

    /// <summary>Un emplacement vide (en unités) ; <paramref name="name"/> s'écrit dedans en attendant un artefact.</summary>
    private static Panel Slot(Vector2 position, string? name)
    {
        var slot = new Panel { Position = V(position.X, position.Y), Size = V(HeroFigure.SlotSize, HeroFigure.SlotSize), MouseFilter = Control.MouseFilterEnum.Stop };
        slot.AddThemeStyleboxOverride("panel", Inset(PanelColor, SilverColor));
        if (name != null)
        {
            var label = MakeLabel(name, 11, DimColor);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Size = slot.Size;
            label.Name = "Name";
            slot.AddChild(label);
        }
        return slot;
    }

    /// <summary>Met un artefact (son image ×3) dans un emplacement, ou le vide.</summary>
    private static void Fill(Panel slot, ArtifactType? artifact)
    {
        foreach (var child in slot.GetChildren().OfType<TextureRect>().ToList())
        {
            slot.RemoveChild(child);
            child.QueueFree();
        }
        if (slot.GetNodeOrNull<Label>("Name") is { } name)
            name.Visible = artifact == null;
        if (artifact == null)
            return;
        if (Art.Get($"artifacts/{artifact.Id}") is { } image)
            slot.AddChild(new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
                Texture = image, Size = slot.Size, // l'artefact de 32 pixels, ×3
            });
        else if (slot.GetNodeOrNull<Label>("Name") is { } label)
        {
            label.Text = artifact.Name; // sans image : son nom
            label.Visible = true;
        }
    }

    private void RefreshEquipment(Hero hero)
    {
        string key = $"{hero.Id}:{string.Join(",", hero.Artifacts.Select(a => a.Id))}";
        if (key == _equipmentKey)
            return;
        _equipmentKey = key;
        _figure.Picture = null; // l'image en pied de chaque héros viendra de la session images
        _figure.QueueRedraw();
        var (worn, chest) = Equipment.Arrange(hero.Artifacts);
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].GetNodeOrNull<Label>("Name") is { } label)
                label.Text = Texts.SlotName(Equipment.Slots[i]);
            Fill(_slots[i], worn[i]);
            _slots[i].TooltipText = Texts.SlotTooltip(Equipment.Slots[i], worn[i]);
        }
        for (int i = 0; i < _chest.Count; i++)
        {
            var artifact = i < chest.Count ? chest[i] : null;
            Fill(_chest[i], artifact);
            _chest[i].TooltipText = artifact != null ? Texts.ArtifactTooltip(artifact) : Texts.ChestTooltip;
        }
    }

    // ------------------------------------------------------------------ Compétences

    private void BuildSkills(Control page)
    {
        _skillPoints = At(MakeLabel("", 15, TextColor), 14, 8);
        page.AddChild(_skillPoints);
        // un marque-page par arbre (vert, pourpre, bleu), qui sort du haut de son cadre ; un seul contenu, refait au choix
        var trees = new TabStrip(13) { Position = V(14, 40) };
        page.AddChild(trees);
        var colors = new[] { TabStrip.Colors[3], TabStrip.Colors[1], TabStrip.Colors[2] };
        foreach (var tree in Enum.GetValues<SkillTree>())
            trees.AddTab(Texts.SkillTreeName(tree), color: colors[(int)tree]);
        trees.Changed += index =>
        {
            _tree = (SkillTree)index;
            if (_hero != null)
                RefreshSkills(_hero);
        };
        float top = 40 + trees.PageTop;
        var frame = new Panel { Position = V(14, top), Size = V(PageSize.X - 28, PageSize.Y - top - 12) };
        frame.AddThemeStyleboxOverride("panel", Inset(PanelColor));
        page.AddChild(frame);
        _treeContent = new Control { Position = V(12, 10), Size = V(PageSize.X - 52, PageSize.Y - top - 32) };
        frame.AddChild(_treeContent);
    }

    /// <summary>Le joueur peut-il dépenser les points de ce héros (le sien, à son tour) ?</summary>
    private bool CanSpend(Hero hero) => _state != null && hero.Owner == _state.CurrentPlayer && !hero.Owner.IsComputer;

    private void RefreshSkills(Hero hero)
    {
        _skillPoints.Text = Texts.SkillPointsLine(hero.SkillPoints, hero.Skills.Count);
        string key = $"{hero.Id}:{_tree}:{hero.SkillPoints}:{CanSpend(hero)}:{string.Join(",", hero.Skills.Select(p => $"{p.Key.Id}{p.Value}"))}";
        if (key == _skillsKey || _state == null)
            return;
        _skillsKey = key;
        foreach (var child in _treeContent.GetChildren())
        {
            _treeContent.RemoveChild(child);
            child.QueueFree();
        }
        int spent = hero.PointsIn(_tree);
        var skills = _state.Data.Skills.InTree(_tree).ToList();
        float y = 0;
        foreach (var row in skills.Select(s => s.Row).Distinct().OrderBy(r => r))
        {
            int needed = Skills.PointsToOpenRow(row);
            bool open = spent >= needed;
            _treeContent.AddChild(At(MakeLabel(Texts.SkillRowLine(row, needed, open), 13, open ? SoftTextColor : MapRenderer.Rgb(200, 140, 120)), 0, y));
            float x = 0;
            foreach (var skill in skills.Where(s => s.Row == row))
            {
                _treeContent.AddChild(SkillEntry(hero, skill, open, new Vector2(x, y + 22)));
                x += 220;
            }
            y += 106;
        }
    }

    /// <summary>
    /// Une compétence : son icône (un rond aux deux premières lettres, en attendant les images ; doré si le héros l'a,
    /// pâle si sa rangée est fermée), puis son nom, son rang et ce que fait le rang suivant. Un clic sur l'icône apprend.
    /// </summary>
    private Control SkillEntry(Hero hero, SkillType skill, bool rowOpen, Vector2 position)
    {
        int rank = hero.Skills.GetValueOrDefault(skill);
        var entry = new Control { Position = V(position.X, position.Y), Size = V(212, 80) };
        var icon = MakeButton(Texts.SkillInitials(skill), 16);
        icon.Position = Vector2.Zero;
        icon.Size = V(52, 52);
        var gold = MapRenderer.Rgb(214, 168, 74);
        var ring = Box(MapRenderer.Rgb(59, 49, 40), rank > 0 ? gold : SilverColor, 2, 2, 2, 2, radius: 26);
        foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            icon.AddThemeStyleboxOverride(state, state == "hover" ? Box(MapRenderer.Rgb(80, 66, 52), gold, 2, 2, 2, 2, radius: 26) : ring);
        icon.Modulate = rowOpen || rank > 0 ? Colors.White : new Color(1, 1, 1, 0.45f);
        var result = hero.CanLearn(skill);
        icon.Disabled = result != LearnSkillResult.Ok || !CanSpend(hero);
        icon.TooltipText = Texts.SkillIconTooltip(skill, rank, CanSpend(hero) ? result : LearnSkillResult.NoPoint);
        icon.Pressed += () =>
        {
            if (_state?.LearnSkill(hero, skill) == LearnSkillResult.Ok)
                Refresh();
        };
        entry.AddChild(icon);
        // le nom, puis l'effet du rang suivant, dans une colonne de largeur fixe (pour le retour à la ligne)
        var text = new VBoxContainer { Position = V(60, 0) };
        text.AddThemeConstantOverride("separation", 0);
        text.AddChild(MakeLabel(Texts.SkillButton(skill, rank), 14, rank > 0 ? TextColor : SoftTextColor));
        var next = MakeLabel(Texts.SkillNextLine(skill, rank), 11, SoftTextColor);
        next.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        next.CustomMinimumSize = V(152, 0);
        text.AddChild(next);
        entry.AddChild(text);
        return entry;
    }

    /// <summary>Ouvre l'onglet des compétences (les arbres : un clic y dépense un point).</summary>
    public void OpenSkillTrees() => _tabs.Select(SkillsTabIndex);

    // ------------------------------------------------------------------ Magie

    private void BuildMagic(Control page)
    {
        _spellPoints = At(MakeLabel("", 15, TextColor), 14, 8);
        _spellPoints.MouseFilter = Control.MouseFilterEnum.Stop;
        _spellPoints.TooltipText = Texts.SpellPointsDaily;
        page.AddChild(_spellPoints);
        var book = FixedButton(Texts.SpellBookButton, 14, V(170, 32));
        book.Position = V(PageSize.X - 184, 6);
        book.Pressed += () =>
        {
            if (_hero is not { } hero)
                return;
            var window = new SpellBookWindow(hero, hero.SpellPoints, notice: Texts.SpellBookOnMap);
            AddChild(window);
            window.Open();
        };
        page.AddChild(book);
        var scroll = new ScrollContainer { Position = V(14, 46), Size = V(PageSize.X - 28, PageSize.Y - 56), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        page.AddChild(scroll);
        _spells = new GridContainer { Columns = 2 };
        _spells.AddThemeConstantOverride("h_separation", (int)Px(20));
        _spells.AddThemeConstantOverride("v_separation", (int)Px(8));
        scroll.AddChild(_spells);
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

    // ------------------------------------------------------------------ ouverture et mise à jour

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
        _class.TooltipText = hero.Class is { } heroClass ? Texts.TalentText(heroClass) ?? "" : "";
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
        var morale = Morale.OfArmy(hero);
        var luck = Morale.LuckOf(hero);
        _morale.Text = $"{Texts.MoraleWord} {Texts.Signed(morale.Total)}";
        _morale.TooltipText = Texts.MoraleTooltip(morale);
        _luck.Text = $"{Texts.LuckWord} {Texts.Signed(luck.Total)}";
        _luck.TooltipText = Texts.LuckTooltip(luck);
        RefreshEquipment(hero);
        RefreshSkills(hero);
        RefreshSpells(hero);
        _army.Show(hero.Army, hero.CommandLimit, hero);
    }
}
